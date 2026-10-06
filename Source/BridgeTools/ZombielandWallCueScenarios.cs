using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Core;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    [Tool(
        "totalfog/zombieland_wall_cues",
        Description = "Run real Stumble wall-push starts with a scoped minimum of four, preserving the native wall bonus and avoiding fake horde counts. Check actual positional one-shots, hidden/visible warning delivery and exactly-once reveal under fog and Zombieland options. Uses normal-speed playback and paused audio frames; restores scoped current/timeline options and probes, then reloads the original named fixture."
    )]
    public static async Task<object> ZombielandWallCues(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79,
        int frames = 15
    )
    {
        if (frames < 12 || frames > 30)
            throw new ArgumentException("Use 12..30 audio frames.");
        await ambientGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.wall-cue-probe");
        bool oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck,
            oldBypass = FogSettings.OnlyOutsideColony,
            oldDelay = FogSettings.DelayAlertsUntilSeen;
        int oldRange = FogSettings.AudioSourceRange;
        float oldMuffling = FogSettings.VolumeMufflingModifier,
            oldAmbient = Prefs.VolumeAmbient;
        Map map = null;
        MapVisibility fog = null;
        object settings = null;
        FieldInfo cue = null,
            warning = null,
            minimum = null,
            values = null;
        var options = new List<(object group, object cue, object warning, object minimum)>();
        Dictionary<string, float> throttle = null;
        bool hadThrottle = false;
        float oldThrottle = 0;
        bool addedSight = false,
            passed = true;
        var cell = new IntVec3(x, 0, z);
        var area = new CellRect(x - 2, z - 2, 5, 5);
        var owned = new HashSet<Thing>();
        var samples = new HashSet<SampleOneShot>();
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the base fixture first.");
                    fog = map.GetVisibility();
                    FogSettings.OnlyOutsideColony = false;
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    cell = GenRadial
                        .RadialCellsAround(cell, 25, true)
                        .FirstOrDefault(candidate =>
                        {
                            var bounds = new CellRect(candidate.x - 2, candidate.z - 2, 5, 5);
                            return bounds.All(c =>
                                    c.InBounds(map)
                                    && !map.fogGrid.IsFogged(c)
                                    && !fog.IsShown(Faction.OfPlayer, c)
                                )
                                && new[]
                                {
                                    candidate,
                                    candidate + IntVec3.North,
                                    candidate + IntVec3.North * 2,
                                }.All(c => c.Standable(map))
                                && GenAdj.CardinalDirections.All(d =>
                                    (candidate + d).GetEdifice(map) == null
                                )
                                && map.roofGrid.RoofAt(candidate + IntVec3.North * 2)
                                    is not { isThickRoof: true }
                                && map.roofGrid.RoofAt(candidate + IntVec3.North * 2)
                                    != RoofDefOf.RoofRockThin;
                        });
                    if (!cell.IsValid || cell == IntVec3.Zero)
                        throw new InvalidOperationException(
                            "No explored remote wall-push strip exists near the requested cell."
                        );
                    area = new CellRect(cell.x - 2, cell.z - 2, 5, 5);
                    var settingsType = AccessTools.TypeByName("ZombieLand.ZombieSettings");
                    values = AccessTools.Field(settingsType, "Values");
                    settings = values.GetValue(null);
                    cue = AccessTools.Field(settings.GetType(), "playWallAndSabotageSounds");
                    warning = AccessTools.Field(settings.GetType(), "dangerousSituationMessage");
                    minimum = AccessTools.Field(settings.GetType(), "minimumZombiesForWallPushing");
                    var groups = new List<object> { settings };
                    if (
                        AccessTools.Field(settingsType, "ValuesOverTime").GetValue(null)
                        is IEnumerable timeline
                    )
                        foreach (var frame in timeline)
                        {
                            var group =
                                frame == null
                                    ? null
                                    : AccessTools.Field(frame.GetType(), "values").GetValue(frame);
                            if (group != null)
                                groups.Add(group);
                        }
                    foreach (var group in groups.Distinct())
                        options.Add(
                            (
                                group,
                                cue.GetValue(group),
                                warning.GetValue(group),
                                minimum.GetValue(group)
                            )
                        );
                    throttle =
                        (Dictionary<string, float>)
                            AccessTools
                                .Field(AccessTools.TypeByName("ZombieLand.Tools"), "nextExecutions")
                                .GetValue(null);
                    hadThrottle = throttle.TryGetValue("DangerousSituation", out oldThrottle);
                    FogSettings.AudioSourceRange = 500;
                    FogSettings.VolumeMufflingModifier = .5f;
                    Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.Zombie"),
                            "CustomTick"
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            "SuppressMobileFixtureCleanup"
                        )
                    );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = cell.x, z = cell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Wall cue camera setup failed.");
            foreach (
                string state in new[]
                {
                    "hidden-muted",
                    "unfiltered",
                    "hearing",
                    "visible-muted",
                    "hidden-again",
                    "sound-disabled",
                    "warning-disabled",
                    "outside-home",
                    "delay-disabled",
                }
            )
            {
                Pawn zombie = null;
                float expectedFactor = 0;
                bool initialVisible = state == "visible-muted",
                    warns = state != "warning-disabled" && state != "outside-home";
                bool delayed = state != "delay-disabled";
                var observed =
                    new Dictionary<SampleOneShot, (float factor, float peak, bool playing)>();
                HashSet<SampleOneShot> beforeSamples = null;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        StopOwnedOneShots(samples);
                        foreach (var t in owned.Where(t => !t.Destroyed).ToArray())
                            t.Destroy();
                        owned.Clear();
                        SetSight(initialVisible);
                        // Native interpolation replaces Values. Apply the scoped
                        // test options to the current object and timeline owners.
                        foreach (
                            var group in options
                                .Select(o => o.group)
                                .Append(values.GetValue(null))
                                .Distinct()
                        )
                        {
                            cue.SetValue(group, state != "sound-disabled");
                            warning.SetValue(group, state != "warning-disabled");
                            minimum.SetValue(group, 4);
                        }
                        FogSettings.MuteHiddenSounds =
                            state == "hidden-muted"
                            || state == "visible-muted"
                            || state == "hidden-again";
                        FogSettings.DoAudioCheck = state == "hearing";
                        FogSettings.DelayAlertsUntilSeen = delayed;
                        var wall = GenSpawn.Spawn(
                            ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                            cell + IntVec3.North,
                            map
                        );
                        owned.Add(wall);
                        map.areaManager.Home[wall.Position] = state != "outside-home";
                        zombie = (Pawn)
                            AccessTools
                                .Method(
                                    AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions"),
                                    "SpawnZombie"
                                )
                                .Invoke(
                                    null,
                                    new object[]
                                    {
                                        cell,
                                        map,
                                        Enum.Parse(
                                            AccessTools.TypeByName("ZombieLand.ZombieType"),
                                            "Normal"
                                        ),
                                        true,
                                    }
                                );
                        if (zombie == null)
                            throw new InvalidOperationException("Wall cue zombie spawn failed.");
                        owned.Add(zombie);
                        var type = zombie.GetType();
                        AccessTools
                            .Field(type, "state")
                            .SetValue(
                                zombie,
                                Enum.Parse(
                                    AccessTools.TypeByName("ZombieLand.ZombieState"),
                                    "Wandering"
                                )
                            );
                        AccessTools.Field(type, "wallPushProgress").SetValue(zombie, -1f);
                        AccessTools.Field(type, "wallPushCooldown").SetValue(zombie, 0);
                        AccessTools.Field(type, "lastGotoPosition").SetValue(zombie, cell);
                        throttle.Remove("DangerousSituation");
                        expectedFactor =
                            state == "sound-disabled"
                                ? 0
                                : SoundAudibility.GetAudibilityFactor(new TargetInfo(cell, map));
                        beforeSamples = Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                        zombie.jobs.StartJob(
                            JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Stumble")),
                            JobCondition.InterruptForced
                        );
                    },
                    cancellationToken
                );
                int stopTick = await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                        return Find.TickManager.TicksGame + 60;
                    },
                    cancellationToken
                );
                var start = await ctx.Game.RunUntilAsync(
                    () =>
                    {
                        Observe();
                        bool done =
                            zombie.Destroyed
                            || (float)
                                AccessTools
                                    .Field(zombie.GetType(), "wallPushProgress")
                                    .GetValue(zombie) >= 0
                            || Find.TickManager.TicksGame >= stopTick;
                        if (done)
                            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        return done;
                    },
                    new RimBridgeWaitOptions { TimeoutMs = 30000 },
                    cancellationToken
                );
                if (!start.Success)
                    throw new InvalidOperationException(
                        "Normal wall-start playback did not complete: " + start.Message
                    );
                int startSteps = await ctx.MainThread.InvokeAsync(
                    () => Find.TickManager.TicksGame - (stopTick - 60),
                    cancellationToken
                );
                for (int i = 0; i < frames; i++)
                {
                    await ctx.Game.FramesAsync(1, cancellationToken);
                    await ctx.MainThread.InvokeAsync(Observe, cancellationToken);
                }
                int queued = 0,
                    archived = 0;
                bool startedPush = false,
                    initiallyPassed = false,
                    settingsMatch = false;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        queued = Pending(zombie);
                        archived = Archived(zombie);
                        startedPush =
                            zombie.Spawned
                            && !zombie.Dead
                            && (float)
                                AccessTools
                                    .Field(zombie.GetType(), "wallPushProgress")
                                    .GetValue(zombie) >= 0;
                        var effective = values.GetValue(null);
                        settingsMatch =
                            (int)minimum.GetValue(effective) == 4
                            && (bool)cue.GetValue(effective) == (state != "sound-disabled")
                            && (bool)warning.GetValue(effective) == (state != "warning-disabled");
                        bool defer = warns && delayed && !initialVisible;
                        initiallyPassed =
                            startedPush
                            && startSteps > 0
                            && settingsMatch
                            && fog.IsShown(Faction.OfPlayer, zombie.Position) == initialVisible
                            && queued == (defer ? 1 : 0)
                            && archived == (warns && !defer ? 1 : 0)
                            && (
                                expectedFactor == 0
                                    ? observed.Count == 0
                                    : observed.Count > 0
                                        && observed.Values.All(s =>
                                            Math.Abs(s.factor - expectedFactor) < .001f
                                        )
                                        && observed.Values.Any(s => s.playing && s.peak > 0)
                            );
                        SetSight(true);
                    },
                    cancellationToken
                );
                await Step(35);
                int firstReveal = await ctx.MainThread.InvokeAsync(
                    () => Archived(zombie),
                    cancellationToken
                );
                await Step(35);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool valid =
                            initiallyPassed
                            && firstReveal == (warns ? 1 : 0)
                            && Archived(zombie) == firstReveal
                            && Pending(zombie) == 0
                            && zombie.Spawned
                            && !zombie.Dead;
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                state,
                                passed = valid,
                                initiallyPassed,
                                startedPush,
                                settingsMatch,
                                nativeStartSteps = startSteps,
                                initialVisible,
                                expectedFactor,
                                initialQueued = queued,
                                initialArchived = archived,
                                firstReveal,
                                secondReveal = Archived(zombie),
                                pendingAfterReveal = Pending(zombie),
                                samples = observed
                                    .Values.Select(s => new
                                    {
                                        s.factor,
                                        s.peak,
                                        s.playing,
                                    })
                                    .ToArray(),
                            }
                        );
                    },
                    cancellationToken
                );

                void Observe()
                {
                    foreach (
                        var sample in Find.SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                            !beforeSamples.Contains(s)
                            && s.subDef.parentDef.defName == "WallPushing"
                        )
                    )
                    {
                        samples.Add(sample);
                        observed.TryGetValue(sample, out var old);
                        observed[sample] = (
                            sample.info.volumeFactor,
                            Math.Max(old.peak, sample.source?.volume ?? 0),
                            old.playing || sample.source?.isPlaying == true
                        );
                    }
                }
            }
            return new
            {
                passed,
                normalPlayback = true,
                scopedMinimum = 4,
                fakeHordeCounts = false,
                fixtureCell = cell.ToString(),
                rows,
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        StopOwnedOneShots(samples);
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        foreach (var t in owned.Where(t => !t.Destroyed).ToArray())
                            t.Destroy();
                        if (addedSight)
                            SetSight(false);
                        foreach (var option in options)
                        {
                            cue.SetValue(option.group, option.cue);
                            warning.SetValue(option.group, option.warning);
                            minimum.SetValue(option.group, option.minimum);
                        }
                        if (settings != null)
                            values.SetValue(null, settings);
                        if (throttle != null)
                        {
                            if (hadThrottle)
                                throttle["DangerousSituation"] = oldThrottle;
                            else
                                throttle.Remove("DangerousSituation");
                        }
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                        FogSettings.OnlyOutsideColony = oldBypass;
                        FogSettings.DelayAlertsUntilSeen = oldDelay;
                        FogSettings.AudioSourceRange = oldRange;
                        FogSettings.VolumeMufflingModifier = oldMuffling;
                        Prefs.VolumeAmbient = oldAmbient;
                    },
                    CancellationToken.None
                );
                var restored = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!restored.Succeeded())
                    throw new InvalidOperationException("Wall cue fixture restoration failed.");
            }
            finally
            {
                ambientGate.Release();
            }
        }

        async Task Step(int ticks)
        {
            var result = await ctx.Game.RunForTicksAsync(
                ticks,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000 },
                cancellationToken
            );
            if (!result.Success)
                throw new InvalidOperationException(
                    "Wall cue normal playback failed: " + result.Message
                );
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight)
                return;
            foreach (var c in area)
            {
                int index = map.cellIndices.CellToIndex(c);
                if (visible)
                    fog.IncrementSeen(Faction.OfPlayer, index);
                else
                    fog.DecrementSeen(Faction.OfPlayer, index);
            }
            addedSight = visible;
        }
        int Archived(Pawn target) =>
            Find
                .Archive.ArchivablesListForReading.OfType<Letter>()
                .Count(l =>
                    l.def.defName == "DangerousSituation"
                    && l.lookTargets.targets.Any(t => t.Thing == target)
                );
        int Pending(Pawn target) =>
            (
                (PendingQueue<DeferredNotification>)
                    AccessTools
                        .Field(typeof(DeferredNotifications), "queue")
                        .GetValue(map.GetComponent<DeferredNotifications>())
            ).Items.Count(n =>
                n.letter?.def.defName == "DangerousSituation"
                && n.Targets.targets.Any(t => t.Thing == target)
            );
    }
}
