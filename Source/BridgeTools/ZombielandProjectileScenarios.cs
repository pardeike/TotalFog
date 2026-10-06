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

namespace TotalFog.BridgeTools;

/// <summary>Observe real projectile travel and landing without invoking Impact directly.</summary>
public sealed partial class ZombielandProjectileScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Thing watched;
    private static int drawCalls;

    [Tool(
        "totalfog/zombieland_ball_flight",
        Description = "Launch a real ZombieBall in an explored remote corridor, advance native flight and observe hidden/visible/hidden native drawing, then let it impact and repeat the drawing transitions on its newly spawned zombie. No direct Impact call or simulation suppression. Restores sight/options/probes and reloads the unchanged named save. Does not accept job target selection, pixels, explosion presentation or ordinary budgeted playback."
    )]
    public static async Task<object> BallFlight(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79,
        int frames = 12
    )
    {
        if (frames < 6 || frames > 30)
            throw new ArgumentException("Use 6..30 frames per phase.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.zombie-ball-flight-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony;
        var rows = new List<object>();
        Map map = null;
        MapVisibility fog = null;
        Projectile projectile = null;
        Pawn spitter = null,
            landedZombie = null;
        IntVec3 destination = new(x + 18, 0, z);
        CellRect corridor = new(x - 2, z - 2, 23, 5);
        bool addedSight = false,
            passed = true;
        int flightTicks = 0,
            startTick = -1;
        Vector3 previousPosition = Vector3.zero;
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
                    if (
                        !destination.InBounds(map)
                        || !destination.Standable(map)
                        || corridor.Any(c =>
                            !c.InBounds(map)
                            || map.fogGrid.IsFogged(c)
                            || fog.IsShown(Faction.OfPlayer, c)
                            || map.roofGrid.RoofAt(c)?.isThickRoof == true
                        )
                    )
                        throw new InvalidOperationException(
                            "Use an unfogged corridor outside actual player sight and thick roofs."
                        );
                    startTick = Find.TickManager.TicksGame;
                    SetSight(true);
                    SetSight(false); // Explored controls must not rely on opaque vanilla fog.
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
                    spitter = map.mapPawns.AllPawnsSpawned.Single(p =>
                        p.GetType().FullName == "ZombieLand.ZombieSpitter"
                    );
                    AccessTools.Field(spitter.GetType(), "tickCounter").SetValue(spitter, 10000);
                    Rand.PushState(717171);
                    try
                    {
                        projectile = (Projectile)
                            GenSpawn.Spawn(
                                DefDatabase<ThingDef>.GetNamed("ZombieBall"),
                                spitter.Position,
                                map
                            );
                        Vector3 origin = spitter.DrawPos + new Vector3(0, 0, .5f);
                        projectile.Launch(
                            spitter,
                            origin,
                            destination,
                            destination,
                            ProjectileHitFlags.IntendedTarget
                        );
                        flightTicks = Math.Max(
                            1,
                            Mathf.CeilToInt(
                                (origin - destination.ToVector3Shifted()).magnitude
                                    / projectile.def.projectile.SpeedTilesPerTick
                            )
                        );
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    if (flightTicks < 30)
                        throw new InvalidOperationException(
                            "Flight is too short for three native transitions."
                        );
                    previousPosition = projectile.ExactPosition;
                    watched = projectile;
                    harmony.Patch(
                        DrawMethod(projectile),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandProjectileScenarios),
                            nameof(ObserveDraw)
                        )
                    );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = x + 9, z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Flight camera setup failed.");
            foreach (bool visible in new[] { false, true, false })
            {
                await ctx.MainThread.InvokeAsync(() => SetSight(visible), cancellationToken);
                await ctx.Game.StepTicksAsync(
                    flightTicks / 5,
                    cancellationToken: cancellationToken
                );
                await ctx.MainThread.InvokeAsync(() => drawCalls = 0, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool moved =
                            projectile.Spawned
                            && !projectile.Destroyed
                            && (
                                projectile.ExactPosition - previousPosition
                            ).MagnitudeHorizontalSquared() > .01f;
                        if (projectile.Spawned)
                            previousPosition = projectile.ExactPosition;
                        bool valid =
                            moved
                            && Visibility.IsVisible(projectile) == visible
                            && map.dynamicDrawManager.DrawThings.Count(t => t == projectile) == 1
                            && (visible ? drawCalls > 0 : drawCalls == 0);
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                stage = "flight",
                                visible,
                                passed = valid,
                                moved,
                                drawCalls,
                                tick = Find.TickManager.TicksGame,
                                position = projectile.Position.ToString(),
                                exact = projectile.ExactPosition.ToString(),
                                registrations = map.dynamicDrawManager.DrawThings.Count(t =>
                                    t == projectile
                                ),
                            }
                        );
                    },
                    cancellationToken
                );
            }
            Pawn[] before = await ctx.MainThread.InvokeAsync(
                () => map.mapPawns.AllPawnsSpawned.ToArray(),
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(flightTicks + 10, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    landedZombie = map.mapPawns.AllPawnsSpawned.Single(p =>
                        !before.Contains(p) && p.GetType().FullName == "ZombieLand.Zombie"
                    );
                    watched = landedZombie;
                    harmony.Patch(
                        DrawMethod(landedZombie),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandProjectileScenarios),
                            nameof(ObserveDraw)
                        )
                    );
                },
                cancellationToken
            );
            foreach (bool visible in new[] { false, true, false })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        SetSight(visible);
                        drawCalls = 0;
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool valid =
                            !landedZombie.Dead
                            && landedZombie.Spawned
                            && Visibility.IsVisible(landedZombie) == visible
                            && map.dynamicDrawManager.DrawThings.Count(t => t == landedZombie) == 1
                            && (visible ? drawCalls > 0 : drawCalls == 0);
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                stage = "landed-zombie",
                                visible,
                                passed = valid,
                                drawCalls,
                                tick = Find.TickManager.TicksGame,
                                position = landedZombie.Position.ToString(),
                                registrations = map.dynamicDrawManager.DrawThings.Count(t =>
                                    t == landedZombie
                                ),
                            }
                        );
                    },
                    cancellationToken
                );
            }
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                    new
                    {
                        passed = passed
                            && projectile.Destroyed
                            && !projectile.Spawned
                            && !map.dynamicDrawManager.DrawThings.Contains(projectile),
                        startTick,
                        endTick = Find.TickManager.TicksGame,
                        flightTicks,
                        directImpact = false,
                        projectileDestroyed = projectile.Destroyed,
                        projectileRegistrations = map.dynamicDrawManager.DrawThings.Count(t =>
                            t == projectile
                        ),
                        landedZombie = landedZombie.ThingID,
                        zombieMvid = spitter.GetType().Assembly.ManifestModule.ModuleVersionId,
                        rows,
                    },
                cancellationToken
            );
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        watched = null;
                        if (addedSight)
                            SetSight(false);
                        FogSettings.OnlyOutsideColony = oldBypass;
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
                gate.Release();
            }
        }

        void SetSight(bool visible)
        {
            if (addedSight == visible)
                return;
            foreach (var cell in corridor)
            {
                int index = map.cellIndices.CellToIndex(cell);
                if (visible)
                    fog.IncrementSeen(Faction.OfPlayer, index);
                else
                    fog.DecrementSeen(Faction.OfPlayer, index);
            }
            addedSight = visible;
        }
    }

    private static System.Reflection.MethodInfo DrawMethod(Thing thing)
    {
        var method = AccessTools.Method(thing.GetType(), nameof(Thing.DrawAt));
        return AccessTools.DeclaredMethod(
            method.DeclaringType,
            method.Name,
            method.GetParameters().Select(p => p.ParameterType).ToArray()
        );
    }

    private static void ObserveDraw(Thing __instance, bool __runOriginal)
    {
        if (__runOriginal && __instance == watched)
            Interlocked.Increment(ref drawCalls);
    }
}
