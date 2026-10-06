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
using UnityEngine;
using Verse;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    [Tool(
        "totalfog/zombieland_action_audio",
        Description = "Run real tank armor, electric bullet absorption, dark-slimer damage and toxic-splasher death under fog audio options. Observes actual native one-shot samples over successive frames and checks damage/effect production while paused. Restores scoped options, destroys only created things and reloads the unchanged named fixture."
    )]
    public static async Task<object> ZombielandActionAudio(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int frames = 15
    )
    {
        if (frames < 12 || frames > 60)
            throw new ArgumentException("Use 12..60 frames per phase.");
        await ambientGate.WaitAsync(cancellationToken);
        bool oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck,
            oldBypass = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.AudioSourceRange;
        float oldMuffling = FogSettings.VolumeMufflingModifier,
            oldAmbient = Prefs.VolumeAmbient;
        Map map = null;
        MapVisibility fog = null;
        object settings = null;
        FieldInfo actionField = null;
        bool oldAction = false,
            addedSight = false,
            passed = true;
        IntVec3 cell = IntVec3.Invalid;
        int startTick = -1,
            index = -1;
        var ownedSamples = new HashSet<SampleOneShot>();
        var createdThings = new HashSet<Thing>();
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    fog = map.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    FogSettings.OnlyOutsideColony = false;
                    FogSettings.AudioSourceRange = 500;
                    FogSettings.VolumeMufflingModifier = .5f;
                    Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                    cell = map.AllCells.First(c =>
                        c.x >= 10
                        && c.z >= 10
                        && c.x < map.Size.x - 10
                        && c.z < map.Size.z - 10
                        && c.Standable(map)
                        && !map.fogGrid.IsFogged(c)
                        && !fog.IsShown(Faction.OfPlayer, c)
                        && c.GetThingList(map).Count == 0
                    );
                    index = map.cellIndices.CellToIndex(cell);
                    settings = AccessTools
                        .Field(AccessTools.TypeByName("ZombieLand.ZombieSettings"), "Values")
                        .GetValue(null);
                    actionField = AccessTools.Field(settings.GetType(), "playZombieActionSounds");
                    oldAction = (bool)actionField.GetValue(settings);
                    startTick = Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = cell.x, z = cell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Action audio camera setup failed.");
            foreach (
                string kind in new[] { "tank-armor", "electric-absorb", "tar-pop", "toxic-splash" }
            )
            foreach (
                string state in new[]
                {
                    "hidden-muted",
                    "unfiltered",
                    "hearing",
                    "visible-muted",
                    "hidden-again",
                    "cue-disabled",
                }
            )
            {
                float expectedFactor = 1,
                    damage = 0,
                    armorLoss = 0;
                int queued = 0,
                    maxQueued = 0,
                    produced = 0;
                bool simulationPassed = false;
                bool? randomIsolated = null;
                Pawn pawn = null;
                HashSet<SampleOneShot> existingSamples = null;
                var observed =
                    new Dictionary<
                        SampleOneShot,
                        (float factor, float peak, bool played, bool makerHasMap)
                    >();
                string sound =
                    kind == "tank-armor" ? "TankyTink"
                    : kind == "electric-absorb" ? "Bzzt"
                    : kind == "tar-pop" ? "TarSmokePop"
                    : "ToxicSplash";
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        StopOwnedOneShots(ownedSamples);
                        foreach (var thing in createdThings.Where(t => !t.Destroyed).ToArray())
                            thing.Destroy();
                        createdThings.Clear();
                        if (addedSight)
                        {
                            fog.DecrementSeen(
                                Faction.OfPlayer,
                                fog.GetFactionShownCells(Faction.OfPlayer),
                                index
                            );
                            addedSight = false;
                        }
                        actionField.SetValue(settings, state != "cue-disabled");
                        FogSettings.MuteHiddenSounds =
                            state == "hidden-muted"
                            || state == "visible-muted"
                            || state == "hidden-again";
                        FogSettings.DoAudioCheck = state == "hearing";
                        if (state == "visible-muted")
                        {
                            fog.IncrementSeen(
                                Faction.OfPlayer,
                                fog.GetFactionShownCells(Faction.OfPlayer),
                                index
                            );
                            addedSight = true;
                        }
                        expectedFactor =
                            state == "cue-disabled"
                                ? 0
                                : SoundAudibility.GetAudibilityFactor(new TargetInfo(cell, map));
                        var before = map.listerThings.AllThings.ToHashSet();
                        existingSamples = Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                        string type =
                            kind == "tank-armor" ? "TankyOperator"
                            : kind == "electric-absorb" ? "Electrifier"
                            : kind == "tar-pop" ? "DarkSlimer"
                            : "ToxicSplasher";
                        pawn = (Pawn)
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
                                            type
                                        ),
                                        true,
                                    }
                                );
                        if (pawn == null)
                            throw new InvalidOperationException(
                                "Action audio zombie spawn failed."
                            );
                        if (kind == "toxic-splash")
                        {
                            pawn.story.bodyType = BodyTypeDefOf.Hulk;
                            pawn.Kill(null);
                        }
                        else
                        {
                            var fields = new[]
                            {
                                "hasTankyShield",
                                "hasTankyHelmet",
                                "hasTankySuit",
                            }
                                .Select(n => AccessTools.Field(pawn.GetType(), n))
                                .ToArray();
                            float armor = fields.Sum(f => (float)f.GetValue(pawn));
                            damage = pawn.TakeDamage(
                                new DamageInfo(DamageDefOf.Bullet, 20f, 1f)
                            ).totalDamageDealt;
                            armorLoss = armor - fields.Sum(f => (float)f.GetValue(pawn));
                            queued = (
                                (IList)
                                    AccessTools.Field(pawn.GetType(), "absorbAttack").GetValue(pawn)
                            ).Count;
                            if (kind == "electric-absorb")
                            {
                                maxQueued = queued;
                                for (int shot = 0; shot < 127; shot++)
                                {
                                    damage += pawn.TakeDamage(
                                        new DamageInfo(DamageDefOf.Bullet, 20f, 1f)
                                    ).totalDamageDealt;
                                    maxQueued = Math.Max(
                                        maxQueued,
                                        (
                                            (IList)
                                                AccessTools
                                                    .Field(pawn.GetType(), "absorbAttack")
                                                    .GetValue(pawn)
                                        ).Count
                                    );
                                }
                            }
                        }
                        foreach (
                            var thing in map.listerThings.AllThings.Where(t => !before.Contains(t))
                        )
                            createdThings.Add(thing);
                        produced = createdThings.Count(t =>
                            t.def.defName == (kind == "tar-pop" ? "TarSmoke" : "StickyGoo")
                        );
                        simulationPassed =
                            kind == "tank-armor" ? damage == 0 && armorLoss > 0
                            : kind == "electric-absorb"
                                ? damage == 0
                                    && (bool)
                                        AccessTools
                                            .Property(pawn.GetType(), "IsActiveElectric")
                                            .GetValue(pawn)
                            : kind == "tar-pop" ? damage > 0 && produced > 0
                            : pawn.Dead && produced >= 6;
                        Observe();
                    },
                    cancellationToken
                );
                for (int i = 0; i < frames; i++)
                {
                    await ctx.Game.FramesAsync(1, cancellationToken);
                    await ctx.MainThread.InvokeAsync(Observe, cancellationToken);
                }
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        int queuedAfterFrames = (
                            (IList)AccessTools.Field(pawn.GetType(), "absorbAttack").GetValue(pawn)
                        ).Count;
                        if (
                            kind == "electric-absorb"
                            && AccessTools.Method(pawn.GetType(), "AbsorbRangedAttack") != null
                        )
                        {
                            randomIsolated = true;
                            var beforeProbe =
                                Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                            foreach (
                                string method in new[]
                                {
                                    "AbsorbRangedAttack",
                                    "ElectrifyAnimation",
                                }
                            )
                            {
                                Rand.PushState(8127);
                                float expectedRandom;
                                try
                                {
                                    expectedRandom = Rand.Value;
                                }
                                finally
                                {
                                    Rand.PopState();
                                }
                                Rand.PushState(8127);
                                try
                                {
                                    AccessTools
                                        .Method(pawn.GetType(), method)
                                        .Invoke(
                                            pawn,
                                            method == "AbsorbRangedAttack"
                                                ? new object[] { 90f }
                                                : null
                                        );
                                    randomIsolated &= Rand.Value == expectedRandom;
                                }
                                finally
                                {
                                    Rand.PopState();
                                }
                            }
                            foreach (
                                var sample in Find.SoundRoot.oneShotManager.PlayingOneShots.Where(
                                    s => !beforeProbe.Contains(s)
                                )
                            )
                                ownedSamples.Add(sample);
                        }
                        bool animationPassed =
                            kind != "electric-absorb"
                            || maxQueued <= 8
                                && (Visibility.IsVisible(map, cell) || maxQueued == 0);
                        bool valid =
                            simulationPassed
                            && animationPassed
                            && randomIsolated != false
                            && Find.TickManager.TicksGame == startTick
                            && (
                                expectedFactor <= 0
                                    ? observed.Count == 0
                                    : observed.Count > 0
                                        && observed.Values.All(s =>
                                            Math.Abs(s.factor - expectedFactor) < .001f
                                            && s.makerHasMap
                                        )
                                        && observed.Values.Any(s => s.played && s.peak > 0)
                            );
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                kind,
                                state,
                                passed = valid,
                                simulationPassed,
                                animationPassed,
                                randomIsolated,
                                damage,
                                armorLoss,
                                queued,
                                maxQueued,
                                queuedAfterFrames,
                                produced,
                                expectedFactor,
                                current = Visibility.IsVisible(map, cell),
                                tick = Find.TickManager.TicksGame,
                                samples = observed
                                    .Values.Select(s => new
                                    {
                                        s.factor,
                                        s.peak,
                                        s.played,
                                        s.makerHasMap,
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
                            !existingSamples.Contains(s) && s.subDef.parentDef.defName == sound
                        )
                    )
                    {
                        ownedSamples.Add(sample);
                        observed.TryGetValue(sample, out var previous);
                        observed[sample] = (
                            sample.info.volumeFactor,
                            Math.Max(
                                previous.peak,
                                sample.source == null ? 0 : sample.source.volume
                            ),
                            previous.played || sample.source?.isPlaying == true,
                            sample.info.Maker.Map != null
                        );
                    }
                }
            }
            return new
            {
                passed,
                startTick,
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
                        StopOwnedOneShots(ownedSamples);
                        foreach (var thing in createdThings.Where(t => !t.Destroyed).ToArray())
                            thing.Destroy();
                        if (addedSight)
                            fog.DecrementSeen(
                                Faction.OfPlayer,
                                fog.GetFactionShownCells(Faction.OfPlayer),
                                index
                            );
                        if (settings != null)
                            actionField.SetValue(settings, oldAction);
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                        FogSettings.OnlyOutsideColony = oldBypass;
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
                    throw new InvalidOperationException("Action audio fixture restoration failed.");
            }
            finally
            {
                ambientGate.Release();
            }
        }
    }
}
