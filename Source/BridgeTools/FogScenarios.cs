using System;
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

namespace TotalFog.BridgeTools;

public sealed class FogScenarios
{
    private static readonly List<string> errors = new();
    private static readonly Dictionary<string, int> phases = new();
    private static readonly Dictionary<string, object> hiddenDraws = new();
    private static bool observing;
    private static bool installed;

    // Reflection lets the identical fixture run against the inherited baseline DLL.
    private static object HiddenComp(Thing thing)
    {
        var comp = (thing as ThingWithComps)?.AllComps.FirstOrDefault(c =>
            c.GetType().FullName is "TotalFog.CompFog" or "RimWorldRealFoW.CompMainComponent"
        );
        return comp?.GetType().GetProperty("Hiddenable")?.GetValue(comp);
    }

    private static bool Hidden(Thing thing)
    {
        var comp = HiddenComp(thing);
        return comp != null && (bool)comp.GetType().GetProperty("Hidden").GetValue(comp);
    }

    private static void ObserveDraw(
        Thing __instance,
        DrawPhase phase,
        Vector3 drawLoc,
        bool __runOriginal
    )
    {
        if (!observing || !__runOriginal || __instance is not Pawn pawn)
            return;
        var componentHidden = Hidden(__instance);
        var held = !__instance.Spawned && __instance.MapHeld != null;
        // Component state follows the logical cell and may be twelve ticks old.
        // Check the actual drawing position independently of that cached flag.
        var map = pawn.MapHeld;
        if (map == null)
            return;
        var fog = map.components.FirstOrDefault(c =>
            c.GetType().FullName
                is "TotalFog.MapVisibility"
                    or "RimWorldRealFoW.MapComponentSeenFog"
        );
        var cell = drawLoc.ToIntVec3();
        bool inSight =
            fog != null
            && cell.InBounds(map)
            && (bool)
                fog.GetType()
                    .GetMethod("IsShown", new[] { typeof(Faction), typeof(IntVec3) })
                    .Invoke(fog, new object[] { Faction.OfPlayer, cell });
        bool player = pawn.Faction == Faction.OfPlayer;
        var hidden = !inSight && !player;
        var key = (hidden ? "hidden" : "visible") + (held ? "-held/" : "/") + phase;
        lock (phases)
        {
            phases[key] = phases.TryGetValue(key, out var count) ? count + 1 : 1;
            if (hidden && hiddenDraws.Count < 20 && !hiddenDraws.ContainsKey(pawn.ThingID))
            {
                hiddenDraws[pawn.ThingID] = new
                {
                    pawn.ThingID,
                    kind = pawn.kindDef.defName,
                    pawn.Dead,
                    pawn.Spawned,
                    player,
                    inSight,
                    componentHidden,
                    x = cell.x,
                    z = cell.z,
                    holder = pawn.ParentHolder?.GetType().FullName,
                };
            }
        }
    }

    private static void ObserveError(string text)
    {
        if (observing && (text.Contains("drawing") || text.Contains("Node is null")))
            lock (errors)
                if (errors.Count < 20)
                    errors.Add(text);
    }

    private static void Install()
    {
        if (installed)
            return;
        var h = new Harmony("brrainz.totalfog.scenarios");
        h.Patch(
            AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
            postfix: new HarmonyMethod(typeof(FogScenarios), nameof(ObserveDraw))
        );
        h.Patch(
            AccessTools.Method(typeof(Log), nameof(Log.Error), new[] { typeof(string) }),
            prefix: new HarmonyMethod(typeof(FogScenarios), nameof(ObserveError))
        );
        installed = true;
    }

    [Tool(
        "totalfog/anomaly_scenario",
        Description = "Spawn ghoul defenders and splitting fleshbeasts, then observe actual render phases and errors."
    )]
    public static async Task<object> AnomalyScenario(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int ticks = 600,
        bool killParents = true,
        int distance = 12,
        string kind = "Trispike"
    )
    {
        if (ticks < 1 || ticks > 3600)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        var parents = new List<Pawn>();
        await ctx.MainThread.InvokeAsync(
            () =>
            {
                Install();
                errors.Clear();
                phases.Clear();
                hiddenDraws.Clear();
                observing = true;
                var map =
                    Find.CurrentMap
                    ?? throw new InvalidOperationException("Load a test map first.");
                var origin = new IntVec3(map.Size.x / 2, 0, map.Size.z / 2);
                for (var i = 0; i < 10; i++)
                {
                    var ghoul = PawnGenerator.GeneratePawn(
                        DefDatabase<PawnKindDef>.GetNamed("Ghoul"),
                        Faction.OfPlayer
                    );
                    GenSpawn.Spawn(ghoul, CellFinder.RandomClosewalkCellNear(origin, map, 7), map);
                    var beast = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed(kind));
                    GenSpawn.Spawn(
                        beast,
                        CellFinder.RandomClosewalkCellNear(
                            origin + new IntVec3(distance, 0, 0),
                            map,
                            4
                        ),
                        map
                    );
                    parents.Add(beast);
                }
            },
            cancellationToken
        );
        try
        {
            await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = 125 + distance, z = 125 },
                cancellationToken: cancellationToken
            );
            await ctx.Game.FramesAsync(5, cancellationToken);
            var before = await ctx.MainThread.InvokeAsync(Snapshot, cancellationToken);
            if (killParents)
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        foreach (var p in parents)
                            if (!p.Dead)
                                p.Kill(null);
                    },
                    cancellationToken
                );
            var step = await ctx.Game.StepTicksAsync(
                ticks,
                new RimBridgeTickOptions { TimeoutMs = 60000 },
                cancellationToken
            );
            await ctx.Game.FramesAsync(10, cancellationToken);
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        errors = errors.ToArray(),
                        phases = new Dictionary<string, int>(phases),
                        before,
                        after = Snapshot(),
                        tick = Find.TickManager.TicksGame,
                        parentsKilled = parents.Count(p => p.Dead),
                        step,
                    },
                cancellationToken
            );
        }
        finally
        {
            observing = false;
        }
    }

    private static object[] Snapshot() =>
        Find
            .CurrentMap.mapPawns.AllPawnsSpawned.Where(p =>
                p.kindDef.defName.Contains("spike") || p.kindDef.defName == "Ghoul"
            )
            .Select(p =>
                (object)
                    new
                    {
                        id = p.ThingID,
                        kind = p.kindDef.defName,
                        hidden = Hidden(p),
                        x = p.Position.x,
                        z = p.Position.z,
                    }
            )
            .Take(80)
            .ToArray();

    [Tool(
        "totalfog/observe_loaded_combat",
        Description = "Observe an existing paused combat save without spawning pawns or forcing deaths. Advances native ticks, records actual fleshbeast deaths, hidden/visible drawing phases and render errors, then leaves the game paused."
    )]
    public static async Task<object> ObserveLoadedCombat(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int ticks = 900
    )
    {
        if (ticks < 1 || ticks > 3600)
            throw new ArgumentOutOfRangeException(nameof(ticks));
        Pawn[] initial = null;
        int startTick = -1;
        object before = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load a combat save first.");
                    if (!Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Pause before observing loaded combat."
                        );
                    Install();
                    errors.Clear();
                    phases.Clear();
                    hiddenDraws.Clear();
                    observing = true;
                    startTick = Find.TickManager.TicksGame;
                    initial = map
                        .mapPawns.AllPawnsSpawned.Where(p => p.kindDef.defName.Contains("spike"))
                        .ToArray();
                    if (!initial.Any(p => p.kindDef.defName == "Fingerspike"))
                        throw new InvalidOperationException(
                            "The loaded combat has no Fingerspikes to observe."
                        );
                    before = initial
                        .GroupBy(p => p.kindDef.defName)
                        .ToDictionary(g => g.Key, g => g.Count());
                },
                cancellationToken
            );
            var step = await ctx.Game.StepTicksAsync(
                ticks,
                new RimBridgeTickOptions { TimeoutMs = 120000 },
                cancellationToken
            );
            await ctx.Game.FramesAsync(30, cancellationToken);
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        startTick,
                        endTick = Find.TickManager.TicksGame,
                        paused = Find.TickManager.Paused,
                        before,
                        deadInitialPawns = initial
                            .Where(p => p.Dead)
                            .GroupBy(p => p.kindDef.defName)
                            .ToDictionary(g => g.Key, g => g.Count()),
                        after = Snapshot(),
                        errors = errors.ToArray(),
                        phases = new Dictionary<string, int>(phases),
                        hiddenDraws = hiddenDraws.Values.ToArray(),
                        duplicates = Find
                            .CurrentMap.dynamicDrawManager.DrawThings.GroupBy(t => t)
                            .Where(g => g.Count() > 1)
                            .Select(g => new { id = g.Key.ThingID, count = g.Count() })
                            .ToArray(),
                        step,
                    },
                cancellationToken
            );
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() => observing = false, CancellationToken.None);
        }
    }

    [Tool(
        "totalfog/pawn_visibility",
        Description = "Inspect hidden state of Anomaly fixture pawns."
    )]
    public static object PawnVisibility() =>
        new
        {
            pawns = Snapshot(),
            duplicates = Find
                .CurrentMap.dynamicDrawManager.DrawThings.GroupBy(t => t)
                .Where(g => g.Count() > 1)
                .Select(g => new { id = g.Key.ThingID, count = g.Count() })
                .ToArray(),
        };

    [Tool(
        "totalfog/visibility_respawn",
        Description = "Check that a hidden pawn respawned into view is registered exactly once."
    )]
    public static object VisibilityRespawn()
    {
        var map = Find.CurrentMap;
        var cell = map.mapPawns.FreeColonistsSpawned.First().Position;
        var pawn = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Trispike"));
        GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(cell, map, 3), map);
        var comp =
            HiddenComp(pawn) ?? throw new InvalidOperationException("Fog component missing.");
        comp.GetType().GetMethod("Hide").Invoke(comp, null);
        var hiddenCount = map.dynamicDrawManager.DrawThings.Count(t => t == pawn);
        var pos = pawn.Position;
        pawn.DeSpawn();
        GenSpawn.Spawn(pawn, pos, map);
        var count = map.dynamicDrawManager.DrawThings.Count(t => t == pawn);
        return new
        {
            id = pawn.ThingID,
            hiddenCount,
            after = new { registeredCount = count, hidden = Hidden(pawn) },
            expectedRegisteredCount = 1,
        };
    }

    [Tool(
        "totalfog/visibility_transition",
        Description = "Move a player ghoul into and out of sight of a hidden fleshbeast and inspect rendering registration."
    )]
    public static async Task<object> VisibilityTransition(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        Pawn enemy = null,
            viewer = null;
        IntVec3 old = IntVec3.Invalid;
        await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map = Find.CurrentMap;
                enemy = map.mapPawns.AllPawnsSpawned.First(p =>
                    (p.kindDef.defName is "Toughspike" or "Trispike") && Hidden(p)
                );
                viewer = map.mapPawns.AllPawnsSpawned.First(p =>
                    p.kindDef.defName == "Ghoul" && p.Faction == Faction.OfPlayer
                );
                old = viewer.Position;
                viewer.Position = CellFinder.RandomClosewalkCellNear(enemy.Position, map, 1);
            },
            cancellationToken
        );
        await ctx.Game.StepTicksAsync(60, cancellationToken: cancellationToken);
        var inSight = await ctx.MainThread.InvokeAsync(
            () =>
                new
                {
                    hidden = Hidden(enemy),
                    registered = enemy.Map.dynamicDrawManager.DrawThings.Count(t => t == enemy),
                },
            cancellationToken
        );
        await ctx.MainThread.InvokeAsync(() => viewer.Position = old, cancellationToken);
        await ctx.Game.StepTicksAsync(60, cancellationToken: cancellationToken);
        var outOfSight = await ctx.MainThread.InvokeAsync(
            () =>
                new
                {
                    hidden = Hidden(enemy),
                    registered = enemy.Map.dynamicDrawManager.DrawThings.Count(t => t == enemy),
                },
            cancellationToken
        );
        return new
        {
            id = enemy.ThingID,
            inSight,
            outOfSight,
            expected = new
            {
                inSightHidden = false,
                inSightRegistered = 1,
                outOfSightHidden = true,
                outOfSightRegistered = 1,
            },
        };
    }

    [Tool(
        "totalfog/fleshbeast_incident",
        Description = "Run the real 10000-point fleshbeast incident and observe hidden emergence."
    )]
    public static async Task<object> FleshbeastIncident(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        var existing = new HashSet<string>();
        var executed = await ctx.MainThread.InvokeAsync(
            () =>
            {
                Install();
                errors.Clear();
                phases.Clear();
                hiddenDraws.Clear();
                observing = true;
                foreach (var p in Find.CurrentMap.mapPawns.AllPawnsSpawned)
                    existing.Add(p.ThingID);
                var def = DefDatabase<IncidentDef>.GetNamed("FleshbeastAttack");
                return def.Worker.TryExecute(
                    new IncidentParms
                    {
                        target = Find.CurrentMap,
                        points = 10000,
                        forced = true,
                    }
                );
            },
            cancellationToken
        );
        try
        {
            var step = await ctx.Game.StepTicksAsync(
                900,
                new RimBridgeTickOptions { TimeoutMs = 60000 },
                cancellationToken
            );
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        executed,
                        step,
                        pawns = Snapshot(),
                        newPawns = Find
                            .CurrentMap.mapPawns.AllPawnsSpawned.Where(p =>
                                !existing.Contains(p.ThingID)
                            )
                            .GroupBy(p => new { kind = p.kindDef.defName, hidden = Hidden(p) })
                            .Select(g => new
                            {
                                g.Key.kind,
                                g.Key.hidden,
                                count = g.Count(),
                            })
                            .ToArray(),
                        phases = new Dictionary<string, int>(phases),
                        errors = errors.ToArray(),
                    },
                cancellationToken
            );
        }
        finally
        {
            observing = false;
        }
    }
}
