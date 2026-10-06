using System;
using System.Collections;
using System.Collections.Concurrent;
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

public sealed class FleckScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Vector3 origin;
    private static int smokeBirth,
        flashBirth,
        mainThread,
        smokeAttempts,
        smokeDraws,
        flashAttempts,
        flashDraws;
    private static readonly ConcurrentDictionary<int, byte> workers = new();

    [Tool(
        "totalfog/fleck_sight_lifecycle",
        Description = "Observe one real static flash and moving smoke through sight loss, hidden movement, reveal and natural expiry. The optional forcedParallel control exercises the engine's existing worker renderer, which is disabled by default. Native batch submissions and real collection/lifetime evidence, not pixel acceptance. Restores sight/options/probes and reloads the named fixture."
    )]
    public static async Task<object> SightLifecycle(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 215,
        int z = 79,
        int frames = 12,
        bool forcedParallel = false
    )
    {
        if (frames < 12 || frames > 30)
            throw new ArgumentException("Use 12..30 frames per phase.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.fleck-lifecycle-probe");
        Map map = null;
        MapVisibility fog = null;
        bool addedSight = false,
            passed = true,
            oldBypass = FogSettings.OnlyOutsideColony;
        var corridor = new CellRect(x - 2, z - 2, 23, 5);
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
                    if (
                        corridor.Any(c =>
                            !c.InBounds(map)
                            || map.fogGrid.IsFogged(c)
                            || fog.IsShown(Faction.OfPlayer, c)
                        )
                    )
                        throw new InvalidOperationException(
                            "Use an explored remote corridor outside player sight."
                        );
                    origin = new IntVec3(x, 0, z).ToVector3Shifted();
                    mainThread = Thread.CurrentThread.ManagedThreadId;
                    smokeBirth = Find.TickManager.TicksGame;
                    var smoke = FleckMaker.GetDataStatic(origin, map, FleckDefOf.Smoke, 2);
                    smoke.velocity = new Vector3(3, 0, 0);
                    smoke.airTimeLeft = 100;
                    map.flecks.CreateFleck(smoke);
                    SetSight(true);
                },
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(32, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    flashBirth = Find.TickManager.TicksGame;
                    map.flecks.CreateFleck(
                        FleckMaker.GetDataStatic(origin, map, FleckDefOf.ExplosionFlash, 3)
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(
                            typeof(FleckStatic),
                            nameof(FleckStatic.Draw),
                            new[] { typeof(float), typeof(DrawBatch) }
                        ),
                        postfix: new HarmonyMethod(typeof(FleckScenarios), nameof(ObserveDraw))
                    );
                    if (forcedParallel)
                        harmony.Patch(
                            AccessTools.DeclaredPropertyGetter(
                                typeof(FleckSystemBase<FleckThrown>),
                                "ParallelizedDrawing"
                            ),
                            postfix: new HarmonyMethod(
                                typeof(FleckScenarios),
                                nameof(ForceParallel)
                            )
                        );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = x + 5, z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Particle camera setup failed.");
            float hiddenStartX = 0;
            foreach (
                string state in new[]
                {
                    "visible",
                    "hidden",
                    "hidden-moved",
                    "revealed",
                    "hidden-again",
                    "expired",
                }
            )
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        SetSight(state is "visible" or "revealed");
                        smokeAttempts = smokeDraws = flashAttempts = flashDraws = 0;
                        workers.Clear();
                        if (state == "hidden")
                            hiddenStartX = Smoke(map).Single().GetPosition().x;
                    },
                    cancellationToken
                );
                if (state == "hidden-moved")
                    await ctx.Game.StepTicksAsync(30, cancellationToken: cancellationToken);
                if (state == "expired")
                    await ctx.Game.StepTicksAsync(600, cancellationToken: cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        // Count the paused frame window separately from lifetime ticks.
                        smokeAttempts = smokeDraws = flashAttempts = flashDraws = 0;
                        workers.Clear();
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        var smoke = Smoke(map).ToArray();
                        var flash = Flash(map).ToArray();
                        bool visible = state is "visible" or "revealed";
                        bool smokeOk =
                            state == "expired"
                                ? smoke.Length == 0 && smokeAttempts == 0
                                : smoke.Length == 1
                                    && smokeAttempts > 0
                                    && (visible ? smokeDraws > 0 : smokeDraws == 0)
                                    && (forcedParallel ? workers.Count > 0 : workers.Count == 0);
                        bool flashAlive = state is "visible" or "hidden";
                        bool flashOk = flashAlive
                            ? flash.Length == 1
                                && flashAttempts > 0
                                && (visible ? flashDraws > 0 : flashDraws == 0)
                            : flash.Length == 0 && flashDraws == 0;
                        bool moved =
                            state != "hidden-moved"
                            || smoke.Single().GetPosition().x > hiddenStartX + 1;
                        passed &= smokeOk && flashOk && moved;
                        rows.Add(
                            new
                            {
                                state,
                                passed = smokeOk && flashOk && moved,
                                tick = Find.TickManager.TicksGame,
                                smokeAttempts,
                                smokeDraws,
                                flashAttempts,
                                flashDraws,
                                workerCount = workers.Count,
                                flashAlive = flash.Length,
                                smoke = smoke
                                    .Select(s => new
                                    {
                                        s.baseData.ageSecs,
                                        s.baseData.ageTicks,
                                        position = s.GetPosition().ToString(),
                                        s.baseData.Alpha,
                                        ownerAssigned = s.baseData.map != null,
                                    })
                                    .ToArray(),
                            }
                        );
                    },
                    cancellationToken
                );
            }
            return new
            {
                passed,
                forcedParallel,
                pixelAcceptance = false,
                createdSmoke = 1,
                createdFlash = 1,
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
                        if (addedSight)
                            SetSight(false);
                        FogSettings.OnlyOutsideColony = oldBypass;
                        workers.Clear();
                    },
                    CancellationToken.None
                );
                var reload = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!reload.Succeeded())
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
                int i = map.cellIndices.CellToIndex(cell);
                if (visible)
                    fog.IncrementSeen(
                        Faction.OfPlayer,
                        fog.GetFactionShownCells(Faction.OfPlayer),
                        i
                    );
                else
                    fog.DecrementSeen(
                        Faction.OfPlayer,
                        fog.GetFactionShownCells(Faction.OfPlayer),
                        i
                    );
            }
            addedSight = visible;
        }
    }

    private static IEnumerable<object> NativeParticles(Map map, Type systemClass)
    {
        var systems = (IDictionary)
            AccessTools.Field(typeof(FleckManager), "systems").GetValue(map.flecks);
        var system = systems[systemClass];
        foreach (string field in new[] { "dataGametime", "dataRealtime" })
        foreach (
            object particle in (IEnumerable)
                AccessTools.Field(system.GetType(), field).GetValue(system)
        )
            yield return particle;
    }

    private static IEnumerable<FleckThrown> Smoke(Map map) =>
        NativeParticles(map, FleckDefOf.Smoke.fleckSystemClass)
            .Cast<FleckThrown>()
            .Where(s => IsOurs(s.baseData, FleckDefOf.Smoke, smokeBirth));

    private static IEnumerable<FleckStatic> Flash(Map map) =>
        NativeParticles(map, FleckDefOf.ExplosionFlash.fleckSystemClass)
            .Cast<FleckStatic>()
            .Where(s => IsOurs(s, FleckDefOf.ExplosionFlash, flashBirth));

    private static bool IsOurs(FleckStatic fleck, FleckDef def, int birth) =>
        fleck.def == def
        && fleck.setupTick == birth
        && fleck.spawnPosition.ToIntVec3() == origin.ToIntVec3();

    private static void ForceParallel(FleckSystemBase<FleckThrown> __instance, ref bool __result)
    {
        if (__instance is FleckSystemThrown)
            __result = true;
    }

    private static void ObserveDraw(ref FleckStatic __instance, bool __runOriginal)
    {
        if (__instance.Alpha <= 0)
            return;
        if (IsOurs(__instance, FleckDefOf.Smoke, smokeBirth))
        {
            Interlocked.Increment(ref smokeAttempts);
            if (__runOriginal)
                Interlocked.Increment(ref smokeDraws);
            if (Thread.CurrentThread.ManagedThreadId != mainThread)
                workers.TryAdd(Thread.CurrentThread.ManagedThreadId, 0);
        }
        if (IsOurs(__instance, FleckDefOf.ExplosionFlash, flashBirth))
        {
            Interlocked.Increment(ref flashAttempts);
            if (__runOriginal)
                Interlocked.Increment(ref flashDraws);
        }
    }
}
