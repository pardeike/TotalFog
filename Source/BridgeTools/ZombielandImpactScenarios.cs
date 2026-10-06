using System;
using System.Collections.Generic;
using System.Linq;
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
    private static Map impactMap;
    private static IntVec3 impactCell;
    private static int flashCreations,
        flashDraws;

    [Tool(
        "totalfog/zombieland_ball_impact",
        Description = "Let five real ZombieBall flights impact without direct Impact calls. Check actual ExplosionFlash submissions at positive alpha and native BallImpact playback across hidden mute, unfiltered, hearing, visible mute and hidden-again controls while retaining newly spawned zombies. Restores options/sight/probes and reloads the named fixture. Native submissions, not pixel or natural job-selection acceptance."
    )]
    public static async Task<object> BallImpact(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79,
        int frames = 12
    )
    {
        if (frames < 12 || frames > 30)
            throw new ArgumentException("Use 12..30 frames per phase.");
        await ambientGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.zombie-ball-impact-probe");
        bool oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck,
            oldBypass = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.AudioSourceRange;
        float oldMuffling = FogSettings.VolumeMufflingModifier,
            oldAmbient = Prefs.VolumeAmbient;
        MapVisibility fog = null;
        Pawn spitter = null;
        bool addedSight = false,
            passed = true;
        var corridor = new CellRect(x - 2, z - 2, 23, 5);
        var ownedSamples = new HashSet<SampleOneShot>();
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    impactMap =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    fog = impactMap.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    FogSettings.OnlyOutsideColony = false;
                    if (
                        corridor.Any(c =>
                            !c.InBounds(impactMap)
                            || impactMap.fogGrid.IsFogged(c)
                            || fog.IsShown(Faction.OfPlayer, c)
                            || impactMap.roofGrid.RoofAt(c)?.isThickRoof == true
                        )
                    )
                        throw new InvalidOperationException(
                            "Use an unfogged remote corridor without thick roofs."
                        );
                    impactCell = new IntVec3(x + 18, 0, z);
                    if (!impactCell.Standable(impactMap))
                        throw new InvalidOperationException("Use a standable landing cell.");
                    SetSight(true);
                    SetSight(false);
                    FogSettings.AudioSourceRange = 500;
                    FogSettings.VolumeMufflingModifier = .5f;
                    Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                    harmony.Patch(
                        AccessTools.DeclaredMethod(
                            typeof(FleckManager),
                            nameof(FleckManager.CreateFleck)
                        ),
                        postfix: new HarmonyMethod(
                            typeof(AudioScenarios),
                            nameof(ObserveFlashCreation)
                        )
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(
                            typeof(FleckStatic),
                            nameof(FleckStatic.Draw),
                            new[] { typeof(float), typeof(DrawBatch) }
                        ),
                        postfix: new HarmonyMethod(typeof(AudioScenarios), nameof(ObserveFlashDraw))
                    );
                },
                cancellationToken
            );
            var spawn = await ctx.Tools.CallAsync(
                "zombieland/spawn_spitter_visual_fixture",
                new
                {
                    x,
                    z,
                    aggressive = false,
                },
                cancellationToken: cancellationToken
            );
            if (!spawn.Succeeded())
                throw new InvalidOperationException("Spitter fixture failed.");
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    spitter = impactMap.mapPawns.AllPawnsSpawned.Single(p =>
                        p.GetType().FullName == "ZombieLand.ZombieSpitter"
                    );
                    AccessTools.Field(spitter.GetType(), "tickCounter").SetValue(spitter, 10000);
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = impactCell.x, z = impactCell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Impact camera setup failed.");
            foreach (
                string state in new[]
                {
                    "hidden-muted",
                    "unfiltered",
                    "hearing",
                    "visible-muted",
                    "hidden-again",
                }
            )
            {
                Projectile ball = null;
                SampleOneShot[] samplesBefore = null,
                    newSamples = null;
                int beforePawns = 0,
                    elapsed = 0;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        StopOwnedOneShots(ownedSamples);
                        SetSight(state == "visible-muted");
                        FogSettings.MuteHiddenSounds =
                            state == "hidden-muted"
                            || state == "visible-muted"
                            || state == "hidden-again";
                        FogSettings.DoAudioCheck = state == "hearing";
                        beforePawns = impactMap.mapPawns.AllPawnsSpawned.Count;
                        samplesBefore = Find.SoundRoot.oneShotManager.PlayingOneShots.ToArray();
                        ball = (Projectile)
                            GenSpawn.Spawn(
                                DefDatabase<ThingDef>.GetNamed("ZombieBall"),
                                spitter.Position,
                                impactMap
                            );
                        ball.Launch(
                            spitter,
                            spitter.DrawPos + new Vector3(0, 0, .5f),
                            impactCell,
                            impactCell,
                            ProjectileHitFlags.IntendedTarget
                        );
                        flashCreations = flashDraws = 0;
                    },
                    cancellationToken
                );
                for (; elapsed < 160; elapsed++)
                {
                    await ctx.Game.StepTicksAsync(1, cancellationToken: cancellationToken);
                    if (await ctx.MainThread.InvokeAsync(() => ball.Destroyed, cancellationToken))
                        break;
                }
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        newSamples = Find
                            .SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                                !samplesBefore.Contains(s)
                                && s.subDef.parentDef.defName == "BallImpact"
                            )
                            .ToArray();
                        foreach (var s in newSamples)
                            ownedSamples.Add(s);
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool visible = fog.IsShown(Faction.OfPlayer, impactCell);
                        float factor = SoundAudibility.GetAudibilityFactor(
                            new TargetInfo(impactCell, impactMap)
                        );
                        var sounds = newSamples
                            .Select(s => new
                            {
                                factor = s.info.volumeFactor,
                                playing = s.source != null && s.source.isPlaying,
                                volume = s.source == null ? 0 : s.source.volume,
                            })
                            .ToArray();
                        int spawned = impactMap.mapPawns.AllPawnsSpawned.Count - beforePawns;
                        bool simulation =
                            ball.Destroyed && !ball.Spawned && spawned == 1 && elapsed < 160;
                        bool visuals =
                            flashCreations == 1 && (visible ? flashDraws > 0 : flashDraws == 0);
                        bool audio =
                            factor <= 0
                                ? sounds.Length == 0
                                : sounds.Length > 0
                                    && sounds.All(s => Math.Abs(s.factor - factor) < .001f)
                                    && sounds.Any(s => s.playing && s.volume > 0);
                        passed &= simulation && visuals && audio;
                        rows.Add(
                            new
                            {
                                state,
                                passed = simulation && visuals && audio,
                                simulation,
                                visuals,
                                audio,
                                visible,
                                spawned,
                                elapsedTicks = elapsed + 1,
                                flashCreations,
                                flashDraws,
                                factor,
                                sounds,
                                tick = Find.TickManager.TicksGame,
                            }
                        );
                    },
                    cancellationToken
                );
            }
            return new
            {
                passed,
                directImpact = false,
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
                        StopOwnedOneShots(ownedSamples);
                        if (addedSight)
                            SetSight(false);
                        impactMap = null;
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                        FogSettings.OnlyOutsideColony = oldBypass;
                        FogSettings.AudioSourceRange = oldRange;
                        FogSettings.VolumeMufflingModifier = oldMuffling;
                        Prefs.VolumeAmbient = oldAmbient;
                    },
                    CancellationToken.None
                );
                var load = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!load.Succeeded())
                    throw new InvalidOperationException("Could not restore the named fixture.");
            }
            finally
            {
                ambientGate.Release();
            }
        }

        void SetSight(bool visible)
        {
            if (addedSight == visible)
                return;
            foreach (var c in corridor)
            {
                int i = impactMap.cellIndices.CellToIndex(c);
                if (visible)
                    fog.IncrementSeen(Faction.OfPlayer, i);
                else
                    fog.DecrementSeen(Faction.OfPlayer, i);
            }
            addedSight = visible;
        }
    }

    private static void ObserveFlashCreation(
        FleckManager __instance,
        FleckCreationData fleckData,
        bool __runOriginal
    )
    {
        if (
            __runOriginal
            && __instance.parent == impactMap
            && fleckData.def.defName == "ExplosionFlash"
            && fleckData.spawnPosition.ToIntVec3() == impactCell
        )
            Interlocked.Increment(ref flashCreations);
    }

    private static void ObserveFlashDraw(ref FleckStatic __instance, bool __runOriginal)
    {
        if (
            __runOriginal
            && __instance.def.defName == "ExplosionFlash"
            && __instance.Alpha > 0
            && __instance.GetPosition().ToIntVec3() == impactCell
        )
            Interlocked.Increment(ref flashDraws);
    }
}
