using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed partial class ZombielandProjectileScenarios
{
    [Tool(
        "totalfog/zombieland_ball_save_prepare",
        Description = "Create and save a real hidden ZombieBall twenty native ticks into flight, then record its uninterrupted native flight to impact at every game tick. Caller cold-loads the save and compares against the matching reference tick, since load readiness can advance the game before pausing. Refuses an existing destination save. Changes no original save; restores options/probes and leaves the reference world paused for the cold restart."
    )]
    public static async Task<object> BallSavePrepare(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string fixtureSaveName,
        int x = 212,
        int z = 79,
        int frames = 12
    )
    {
        if (
            !fixtureSaveName.StartsWith("TotalFog_BallReload_", StringComparison.Ordinal)
            || frames < 6
            || frames > 30
        )
            throw new ArgumentException("Use a new TotalFog_BallReload_ save and 6..30 frames.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.ball-save-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony;
        Map map = null;
        Projectile ball = null;
        object snapshot = null,
            presentation = null;
        var reference = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (File.Exists(GenFilePaths.FilePathForSavedGame(fixtureSaveName)))
                        throw new InvalidOperationException("The destination save already exists.");
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the base fixture first.");
                    var fog = map.GetVisibility();
                    var corridor = new CellRect(x - 2, z - 2, 23, 5);
                    FogSettings.OnlyOutsideColony = false;
                    if (
                        !Find.TickManager.Paused
                        || !fog.Initialized
                        || corridor.Any(c =>
                            !c.InBounds(map)
                            || map.fogGrid.IsFogged(c)
                            || fog.IsShown(Faction.OfPlayer, c)
                            || map.roofGrid.RoofAt(c)?.isThickRoof == true
                        )
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized map and explored remote corridor without thick roofs."
                        );
                    foreach (var c in corridor)
                    {
                        int i = map.cellIndices.CellToIndex(c);
                        fog.IncrementSeen(Faction.OfPlayer, i);
                        fog.DecrementSeen(Faction.OfPlayer, i);
                    }
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
                    var spitter = map.mapPawns.AllPawnsSpawned.Single(p =>
                        p.GetType().FullName == "ZombieLand.ZombieSpitter"
                    );
                    AccessTools.Field(spitter.GetType(), "tickCounter").SetValue(spitter, 10000);
                    var destination = new IntVec3(x + 18, 0, z);
                    if (!destination.Standable(map))
                        throw new InvalidOperationException("Use a standable landing cell.");
                    Rand.PushState(818181);
                    try
                    {
                        ball = (Projectile)
                            GenSpawn.Spawn(
                                DefDatabase<ThingDef>.GetNamed("ZombieBall"),
                                spitter.Position,
                                map
                            );
                        ball.Launch(
                            spitter,
                            spitter.DrawPos + new Vector3(0, 0, .5f),
                            destination,
                            destination,
                            ProjectileHitFlags.IntendedTarget
                        );
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    watched = ball;
                    harmony.Patch(
                        DrawMethod(ball),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandProjectileScenarios),
                            nameof(ObserveDraw)
                        )
                    );
                },
                cancellationToken
            );
            var step = await ctx.Game.StepTicksAsync(
                20,
                new RimBridgeTickOptions { TimeoutMs = 30000 },
                cancellationToken
            );
            if (!step.Success)
                throw new InvalidOperationException(
                    "Twenty flight ticks did not complete: " + step.Message
                );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = x + 9, z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Flight camera setup failed.");
            await ctx.MainThread.InvokeAsync(() => drawCalls = 0, cancellationToken);
            await ctx.Game.FramesAsync(frames, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (
                        !ball.Spawned
                        || ball.Destroyed
                        || drawCalls != 0
                        || map.dynamicDrawManager.DrawThings.Count(t => t == ball) != 1
                        || Visibility.IsVisible(ball)
                    )
                        throw new InvalidOperationException(
                            "The live flight must be hidden and registered once before saving."
                        );
                    snapshot = BallSaveState(ball);
                    presentation = new
                    {
                        hiddenDrawCalls = drawCalls,
                        registrations = 1,
                        hidden = true,
                        discovery = DiscoveryState(map),
                    };
                },
                cancellationToken
            );
            var saved = await ctx.Tools.CallAsync(
                "rimworld/save_game",
                new { saveName = fixtureSaveName },
                cancellationToken: cancellationToken
            );
            if (!saved.Succeeded())
                throw new InvalidOperationException("Could not save the live flight.");
            bool exists = await ctx.MainThread.InvokeAsync(
                () => File.Exists(GenFilePaths.FilePathForSavedGame(fixtureSaveName)),
                cancellationToken
            );
            if (!exists)
                throw new InvalidOperationException("The prepared save was not written.");
            reference.Add(snapshot);
            Pawn[] beforeImpact = await ctx.MainThread.InvokeAsync(
                () => map.mapPawns.AllPawnsSpawned.ToArray(),
                cancellationToken
            );
            for (
                int i = 0;
                i < 120
                    && !await ctx.MainThread.InvokeAsync(() => ball.Destroyed, cancellationToken);
                i++
            )
            {
                var advance = await ctx.Game.StepTicksAsync(
                    1,
                    new RimBridgeTickOptions { TimeoutMs = 30000 },
                    cancellationToken
                );
                if (!advance.Success)
                    throw new InvalidOperationException(
                        "Reference flight stepping failed: " + advance.Message
                    );
                if (!await ctx.MainThread.InvokeAsync(() => ball.Destroyed, cancellationToken))
                    reference.Add(
                        await ctx.MainThread.InvokeAsync(
                            () => BallSaveState(ball),
                            cancellationToken
                        )
                    );
            }
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                {
                    int spawned = map.mapPawns.AllPawnsSpawned.Count(p =>
                        !beforeImpact.Contains(p) && p.GetType().FullName == "ZombieLand.Zombie"
                    );
                    bool ended =
                        ball.Destroyed
                        && !ball.Spawned
                        && !map.dynamicDrawManager.DrawThings.Contains(ball);
                    return new
                    {
                        passed = ended && spawned == 1,
                        fixtureSaveName,
                        step.CompletedTicks,
                        snapshot,
                        presentation,
                        reference,
                        referenceImpactTick = Find.TickManager.TicksGame,
                        referenceSpawnedZombies = spawned,
                    };
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
                        FogSettings.OnlyOutsideColony = oldBypass;
                    },
                    CancellationToken.None
                );
            }
            finally
            {
                gate.Release();
            }
        }
    }

    [Tool(
        "totalfog/zombieland_ball_save_resume",
        Description = "After a fresh-process load of the prepared ball save, return the saved flight fields/discovery, check native hidden/visible/hidden drawing and registration, advance the real remaining flight to impact, then check the new zombie. Restores options/probes/sight and reloads the named original fixture. Caller must compare the returned initial state with the prepare result to establish serialization acceptance."
    )]
    public static async Task<object> BallSaveResume(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string originalSaveName,
        int x = 212,
        int z = 79,
        int frames = 12
    )
    {
        if (frames < 6 || frames > 30)
            throw new ArgumentException("Use 6..30 frames.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.ball-reload-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony,
            addedSight = false,
            passed = true;
        Map map = null;
        MapVisibility fog = null;
        Projectile ball = null;
        Pawn landed = null;
        object snapshot = null,
            discovery = null;
        var corridor = new CellRect(x - 2, z - 2, 23, 5);
        var rows = new List<object>();
        int impactTicks = 0;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException(
                            "Load the prepared flight in a fresh process first."
                        );
                    fog = map.GetVisibility();
                    FogSettings.OnlyOutsideColony = false;
                    if (
                        !Find.TickManager.Paused
                        || !fog.Initialized
                        || corridor.Any(c => !c.InBounds(map) || fog.IsShown(Faction.OfPlayer, c))
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized remote flight."
                        );
                    ball = map
                        .listerThings.AllThings.OfType<Projectile>()
                        .Single(p => p.def.defName == "ZombieBall");
                    snapshot = BallSaveState(ball);
                    discovery = DiscoveryState(map);
                    watched = ball;
                    harmony.Patch(
                        DrawMethod(ball),
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
                throw new InvalidOperationException("Reload camera setup failed.");
            await Observe("reloaded-flight", ball);
            Pawn[] before = await ctx.MainThread.InvokeAsync(
                () => map.mapPawns.AllPawnsSpawned.ToArray(),
                cancellationToken
            );
            for (; impactTicks < 120; impactTicks++)
            {
                var step = await ctx.Game.StepTicksAsync(
                    1,
                    new RimBridgeTickOptions { TimeoutMs = 30000 },
                    cancellationToken
                );
                if (!step.Success)
                    throw new InvalidOperationException(
                        "Reloaded flight stepping failed: " + step.Message
                    );
                if (await ctx.MainThread.InvokeAsync(() => ball.Destroyed, cancellationToken))
                    break;
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    landed = map.mapPawns.AllPawnsSpawned.Single(p =>
                        !before.Contains(p) && p.GetType().FullName == "ZombieLand.Zombie"
                    );
                    passed &=
                        ball.Destroyed
                        && !ball.Spawned
                        && impactTicks < 120
                        && !map.dynamicDrawManager.DrawThings.Contains(ball);
                    watched = landed;
                    harmony.Patch(
                        DrawMethod(landed),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandProjectileScenarios),
                            nameof(ObserveDraw)
                        )
                    );
                },
                cancellationToken
            );
            await Observe("landed-zombie", landed);
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                    new
                    {
                        passed,
                        snapshot,
                        discovery,
                        directImpact = false,
                        impactTicks = impactTicks + 1,
                        projectileDestroyed = ball.Destroyed,
                        projectileRegistrations = map.dynamicDrawManager.DrawThings.Count(t =>
                            t == ball
                        ),
                        landingCellMatches = landed.Position == new IntVec3(x + 18, 0, z),
                        zombie = landed.ThingID,
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
                var reload = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName = originalSaveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!reload.Succeeded())
                    throw new InvalidOperationException("Original fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }

        async Task Observe(string stage, Thing thing)
        {
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
                        int registrations = map.dynamicDrawManager.DrawThings.Count(t =>
                            t == thing
                        );
                        bool valid =
                            thing.Spawned
                            && !thing.Destroyed
                            && Visibility.IsVisible(thing) == visible
                            && registrations == 1
                            && (visible ? drawCalls > 0 : drawCalls == 0);
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                stage,
                                visible,
                                passed = valid,
                                registrations,
                                drawCalls,
                                tick = Find.TickManager.TicksGame,
                                position = thing.Position.ToString(),
                            }
                        );
                    },
                    cancellationToken
                );
            }
        }

        void SetSight(bool visible)
        {
            if (visible == addedSight)
                return;
            foreach (var c in corridor)
            {
                int i = map.cellIndices.CellToIndex(c);
                if (visible)
                    fog.IncrementSeen(Faction.OfPlayer, i);
                else
                    fog.DecrementSeen(Faction.OfPlayer, i);
            }
            addedSight = visible;
        }
    }

    private static object BallSaveState(Projectile ball) =>
        new
        {
            id = ball.ThingID,
            tick = Find.TickManager.TicksGame,
            ball.Spawned,
            ball.Destroyed,
            position = Cell(ball.Position),
            exact = Vector(ball.ExactPosition),
            rotation = new
            {
                ball.ExactRotation.x,
                ball.ExactRotation.y,
                ball.ExactRotation.z,
                ball.ExactRotation.w,
            },
            origin = Vector(
                (Vector3)AccessTools.Field(typeof(Projectile), "origin").GetValue(ball)
            ),
            destination = Vector(
                (Vector3)AccessTools.Field(typeof(Projectile), "destination").GetValue(ball)
            ),
            ticksToImpact = AccessTools.Field(typeof(Projectile), "ticksToImpact").GetValue(ball),
            lifetime = AccessTools.Field(typeof(Projectile), "lifetime").GetValue(ball),
            landed = AccessTools.Field(typeof(Projectile), "landed").GetValue(ball),
            desiredHitFlags = AccessTools
                .Field(typeof(Projectile), "desiredHitFlags")
                .GetValue(ball),
            preventFriendlyFire = AccessTools
                .Field(typeof(Projectile), "preventFriendlyFire")
                .GetValue(ball),
            equipmentQuality = AccessTools
                .Field(typeof(Projectile), "equipmentQuality")
                .GetValue(ball),
            rotationRate = AccessTools.Field(ball.GetType(), "rotation").GetValue(ball),
            launcherId = ball.Launcher?.ThingID,
            launcherType = ball.Launcher?.GetType().FullName,
            usedTarget = Cell(ball.usedTarget.Cell),
            intendedTarget = Cell(ball.intendedTarget.Cell),
        };

    private static object DiscoveryState(Map map)
    {
        int count = 0,
            hash = 17;
        var cells = map.GetVisibility().knownCells;
        for (int i = 0; i < cells.Length; i++)
            if (cells[i])
            {
                count++;
                hash = unchecked(hash * 31 + i);
            }
        return new { count, hash };
    }

    private static object Cell(IntVec3 cell) => new { cell.x, cell.z };

    private static object Vector(Vector3 value) =>
        new
        {
            value.x,
            value.y,
            value.z,
        };
}
