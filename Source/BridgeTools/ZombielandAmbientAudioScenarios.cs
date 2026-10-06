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
    private static readonly SemaphoreSlim ambientGate = new(1, 1);

    [Tool(
        "totalfog/zombieland_ambient_audio",
        Description = "Measure native electric/tank ambient mixers and actual Unity loop volumes with near hidden and farther visible sources. Tests mute/hearing settings on existing camera loops, mixed-source aggregation, sight transitions and unchanged paused simulation. Restores options and reloads the unchanged named fixture."
    )]
    public static async Task<object> ZombielandAmbientAudio(
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
        MapComponent manager = null;
        var groups = new object[2];
        var previous = new object[2][];
        var loops = new Sustainer[2];
        var oldLoops = new Sustainer[2];
        var loopFields = new FieldInfo[2];
        var updates = new MethodInfo[2];
        var targets = new Pawn[2, 2];
        var contributed = new List<int>();
        var rows = new List<object>();
        int startTick = -1;
        bool passed = true;
        try
        {
            var cell = await ctx.MainThread.InvokeAsync(
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
                    var root = map.AllCells.First(c =>
                        new[]
                        {
                            c,
                            c + IntVec3.North * 2,
                            c + IntVec3.East * 28,
                            c + IntVec3.East * 28 + IntVec3.North * 2,
                        }.All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !map.fogGrid.IsFogged(p)
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).Count == 0
                        )
                    );
                    var managerType =
                        AccessTools.TypeByName("ZombieLand.TickManager")
                        ?? throw new InvalidOperationException("Load Zombieland first.");
                    manager = map.components.Single(c => c.GetType() == managerType);
                    var runtime = AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions");
                    var zombieType = AccessTools.TypeByName("ZombieLand.ZombieType");
                    var spawn = AccessTools.Method(runtime, "SpawnZombie");
                    for (int g = 0; g < 2; g++)
                    {
                        groups[g] = AccessTools
                            .Field(managerType, g == 0 ? "hummingZombies" : "tankZombies")
                            .GetValue(manager);
                        previous[g] = ((IEnumerable)groups[g]).Cast<object>().ToArray();
                        AccessTools.Method(groups[g].GetType(), "Clear").Invoke(groups[g], null);
                        loopFields[g] = AccessTools.Field(
                            managerType,
                            g == 0 ? "electricSustainer" : "tankSustainer"
                        );
                        oldLoops[g] = (Sustainer)loopFields[g].GetValue(manager);
                        loopFields[g].SetValue(manager, null);
                        updates[g] = AccessTools.Method(
                            managerType,
                            g == 0 ? "UpdateElectricalHumming" : "UpdateTankMovement"
                        );
                        for (int n = 0; n < 2; n++)
                        {
                            var p = root + IntVec3.North * (g * 2) + IntVec3.East * (n * 28);
                            targets[g, n] = (Pawn)
                                spawn.Invoke(
                                    null,
                                    new object[]
                                    {
                                        p,
                                        map,
                                        Enum.Parse(
                                            zombieType,
                                            g == 0 ? "Electrifier" : "TankyOperator"
                                        ),
                                        true,
                                    }
                                );
                            if (targets[g, n] == null)
                                throw new InvalidOperationException("Ambient zombie spawn failed.");
                            AccessTools
                                .Method(groups[g].GetType(), "Add")
                                .Invoke(groups[g], new object[] { targets[g, n] });
                        }
                    }
                    startTick = Find.TickManager.TicksGame;
                    return root;
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = cell.x, z = cell.z },
                cancellationToken: cancellationToken
            );
            var zoom = await ctx.Tools.CallAsync(
                "rimworld/set_camera_zoom",
                new { rootSize = 11 },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded() || !zoom.Succeeded())
                throw new InvalidOperationException("Ambient camera setup failed.");
            await ctx.Game.FramesAsync(frames, cancellationToken);
            foreach (
                string state in new[]
                {
                    "muted-at-start",
                    "unfiltered",
                    "hearing-filtered",
                    "muted-existing",
                    "muted-visible",
                    "muted-mixed",
                    "muted-hidden-again",
                    "restored",
                }
            )
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        foreach (int i in contributed)
                            fog.DecrementSeen(
                                Faction.OfPlayer,
                                fog.GetFactionShownCells(Faction.OfPlayer),
                                i
                            );
                        contributed.Clear();
                        FogSettings.MuteHiddenSounds = state.StartsWith(
                            "muted",
                            StringComparison.Ordinal
                        );
                        FogSettings.DoAudioCheck = state == "hearing-filtered";
                        for (int g = 0; g < 2; g++)
                        {
                            for (int n = 0; n < 2; n++)
                                if (state == "muted-visible" || state == "muted-mixed" && n == 1)
                                {
                                    int i = map.cellIndices.CellToIndex(targets[g, n].Position);
                                    fog.IncrementSeen(
                                        Faction.OfPlayer,
                                        fog.GetFactionShownCells(Faction.OfPlayer),
                                        i
                                    );
                                    contributed.Add(i);
                                }
                            if (loops[g] != null)
                                loops[g].info.volumeFactor = -1;
                            // These production methods have their own wall-clock cadence.
                            // A sentinel proves an actual mix ran, rather than assuming a call did work.
                            for (int attempts = 0; attempts < 5000; attempts++)
                            {
                                updates[g].Invoke(manager, null);
                                loops[g] = (Sustainer)loopFields[g].GetValue(manager);
                                if (loops[g] != null && loops[g].info.volumeFactor >= 0)
                                    break;
                            }
                            if (loops[g] == null || loops[g].info.volumeFactor < 0)
                                throw new InvalidOperationException(
                                    "Native ambient mixer did not run. Enable Zombieland special ambient cues."
                                );
                        }
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        for (int g = 0; g < 2; g++)
                        {
                            var sources = Enumerable
                                .Range(0, 2)
                                .Select(n =>
                                {
                                    var pawn = targets[g, n];
                                    float distance = (
                                        Find.CameraDriver.transform.position - pawn.DrawPos
                                    ).magnitude;
                                    float nativeVolume = GenMath.LerpDoubleClamped(
                                        g == 0 ? 12f : 24f,
                                        g == 0 ? 36f : 64f,
                                        1f,
                                        0f,
                                        distance
                                    );
                                    float factor =
                                        FogSettings.MuteHiddenSounds || FogSettings.DoAudioCheck
                                            ? SoundAudibility.GetAudibilityFactor(
                                                new TargetInfo(pawn),
                                                FogSettings.AudioSourceRange
                                            )
                                            : 1f;
                                    return new
                                    {
                                        id = pawn.ThingID,
                                        current = Visibility.IsVisible(pawn),
                                        distance,
                                        nativeVolume,
                                        factor,
                                        expectedVolume = nativeVolume * factor,
                                        pawn.Spawned,
                                        pawn.Dead,
                                    };
                                })
                                .ToArray();
                            float expected = sources.Max(s => s.expectedVolume);
                            var samples = Samples(loops[g])
                                .Select(s => new
                                {
                                    volume = s.source.volume,
                                    muted = s.source.mute,
                                    playing = s.source.isPlaying,
                                })
                                .ToArray();
                            bool valid =
                                Math.Abs(loops[g].info.volumeFactor - expected) < .001f
                                && !loops[g].Ended
                                && sources.All(s => s.Spawned && !s.Dead)
                                && samples.Length > 0
                                && (
                                    expected <= 0
                                        ? samples.All(s => s.volume == 0)
                                        : samples.Any(s => s.playing && s.volume > 0)
                                );
                            passed &= valid;
                            rows.Add(
                                new
                                {
                                    state,
                                    kind = g == 0 ? "electric" : "tank",
                                    passed = valid,
                                    expected,
                                    actual = loops[g].info.volumeFactor,
                                    ended = loops[g].Ended,
                                    cameraMakerHasMap = loops[g].info.Maker.Map != null,
                                    sources,
                                    samples,
                                }
                            );
                        }
                    },
                    cancellationToken
                );
            }
            return new
            {
                passed,
                startTick,
                endTick = await ctx.MainThread.InvokeAsync(
                    () => Find.TickManager.TicksGame,
                    cancellationToken
                ),
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
                        foreach (int i in contributed)
                            fog.DecrementSeen(
                                Faction.OfPlayer,
                                fog.GetFactionShownCells(Faction.OfPlayer),
                                i
                            );
                        for (int g = 0; g < 2; g++)
                        {
                            loops[g]?.End();
                            if (loopFields[g] != null)
                                loopFields[g].SetValue(manager, oldLoops[g]);
                            if (groups[g] != null)
                            {
                                AccessTools
                                    .Method(groups[g].GetType(), "Clear")
                                    .Invoke(groups[g], null);
                                foreach (var previousSource in previous[g])
                                    AccessTools
                                        .Method(groups[g].GetType(), "Add")
                                        .Invoke(groups[g], new[] { previousSource });
                            }
                        }
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
                    throw new InvalidOperationException("Ambient fixture restoration failed.");
            }
            finally
            {
                ambientGate.Release();
            }
        }
    }
}
