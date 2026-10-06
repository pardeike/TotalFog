using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class PresentationScenarios
{
    private static readonly SemaphoreSlim gasGate = new(1, 1);
    private static Thing gasProbe;
    private static int gasDraws;

    [Tool(
        "totalfog/gas_presentation",
        Description = "Spawn a real named Gas definition on an explored hidden cell and observe native DrawAt through hidden/visible/hidden/revealed cell-sight transitions. Then advance actual game ticks through its unmodified expiry. Call with _rimBridgeTimeoutMs=120000 to allow frame-stepped natural lifetimes. Restores sight/settings and reloads the unchanged named save. Does not test gas production/spread or cross-cell pixel clipping."
    )]
    public static async Task<object> GasPresentation(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string defName,
        int frames = 12
    )
    {
        if (frames < 3 || frames > 60)
            throw new ArgumentException("Use 3..60 frames.");
        await gasGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.gas-presentation-probe");
        Map map = null;
        MapVisibility fog = null;
        int index = -1,
            startTick = -1,
            expiry = -1;
        bool addedSight = false,
            oldBypass = FogSettings.OnlyOutsideColony;
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
                    var def = DefDatabase<ThingDef>.GetNamed(defName);
                    if (
                        !typeof(Gas).IsAssignableFrom(def.thingClass)
                        || def.drawerType != DrawerType.RealtimeOnly
                    )
                        throw new InvalidOperationException("Use a real realtime Gas definition.");
                    var cell = map.AllCells.First(c =>
                        c.Standable(map)
                        && !map.fogGrid.IsFogged(c)
                        && !fog.IsShown(Faction.OfPlayer, c)
                        && c.GetThingList(map).Count == 0
                    );
                    index = map.cellIndices.CellToIndex(cell);
                    gasProbe = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
                    startTick = Find.TickManager.TicksGame;
                    expiry = ((Gas)gasProbe).destroyTick;
                    if (expiry <= startTick || expiry - startTick > 4000)
                        throw new InvalidOperationException(
                            "Use a gas with an unmodified lifetime of 1..4000 ticks."
                        );
                    harmony.Patch(
                        AccessTools.Method(def.thingClass, nameof(Thing.DrawAt)),
                        postfix: new HarmonyMethod(
                            typeof(PresentationScenarios),
                            nameof(ObserveGasDraw)
                        )
                    );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = gasProbe.Position.x, z = gasProbe.Position.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Gas camera framing failed.");
            foreach (bool shown in new[] { false, true, false, true })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (shown)
                            fog.IncrementSeen(Faction.OfPlayer, index);
                        else if (addedSight)
                            fog.DecrementSeen(Faction.OfPlayer, index);
                        addedSight = shown;
                        gasDraws = 0;
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                rows.Add(
                    await ctx.MainThread.InvokeAsync<object>(
                        () =>
                            new
                            {
                                shown,
                                draws = gasDraws,
                                tick = Find.TickManager.TicksGame,
                                spawned = gasProbe.Spawned,
                                destroyTick = ((Gas)gasProbe).destroyTick,
                                current = Visibility.IsVisible(map, gasProbe.Position),
                                registered = map.dynamicDrawManager.DrawThings.Count(t =>
                                    t == gasProbe
                                ),
                            },
                        cancellationToken
                    )
                );
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                },
                cancellationToken
            );
            var tickStep = await ctx.Game.StepTicksAsync(
                expiry - startTick + 16,
                new RimBridgeTickOptions { TimeoutMs = 90000 },
                cancellationToken
            );
            if (!tickStep.Success)
                throw new InvalidOperationException(
                    $"Gas lifetime stepping failed: {tickStep.Status}, "
                        + $"{tickStep.CompletedTicks}/{tickStep.RequestedTicks} ticks. {tickStep.Message}"
                );
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                    new
                    {
                        defName,
                        rows,
                        startTick,
                        expiry,
                        tickStep,
                        endTick = Find.TickManager.TicksGame,
                        expiredWhileHidden = gasProbe.Destroyed && !gasProbe.Spawned,
                        expiryCellStillHidden = !fog.IsShown(
                            Faction.OfPlayer,
                            map.cellIndices.IndexToCell(index)
                        ),
                        drawRegistrationsAfterExpiry = map.dynamicDrawManager.DrawThings.Count(t =>
                            t == gasProbe
                        ),
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
                        if (addedSight)
                            fog.DecrementSeen(Faction.OfPlayer, index);
                        FogSettings.OnlyOutsideColony = oldBypass;
                        gasProbe = null;
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
                    throw new InvalidOperationException("Gas fixture restoration failed.");
            }
            finally
            {
                gasGate.Release();
            }
        }
    }

    private static void ObserveGasDraw(Thing __instance)
    {
        if (__instance == gasProbe)
            gasDraws++;
    }

    [Tool(
        "totalfog/custom_drawable",
        Description = "Exercise all draw phases and virtual overlays of a custom non-pawn Thing, plus a late-created comp-bearing definition."
    )]
    public static async Task<object> CustomDrawable(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        var probes = new List<ProbeThing>();
        object late = null;
        var hoverResults = new List<object>();
        IntVec3 visible = IntVec3.Invalid,
            hidden = IntVec3.Invalid;
        await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map = Find.CurrentMap;
                var fog = map.GetComponent<MapVisibility>();
                visible = map
                    .mapPawns.AllPawnsSpawned.First(p =>
                        p.Faction == Faction.OfPlayer
                        && p.TryGetComp<CompFog>()?.FieldOfViewWatcher?.LastSightRange > 1
                    )
                    .Position;
                hidden = map
                    .AllCells.Where(c =>
                        !map.fogGrid.IsFogged(c) && !fog.IsShown(Faction.OfPlayer, c)
                    )
                    .OrderBy(c => c.DistanceToSquared(visible))
                    .First();
                var def = new ThingDef
                {
                    defName = "TotalFogProbeDrawable",
                    label = "visibility probe",
                    thingClass = typeof(ProbeThing),
                    category = ThingCategory.Projectile,
                    drawerType = DrawerType.RealtimeOnly,
                    drawGUIOverlay = true,
                    hasTooltip = true,
                    size = new IntVec2(1, 1),
                    modContentPack = LoadedModManager.GetMod<TotalFogMod>().Content,
                };
                AssignHash(def);
                DefGenerator.AddImpliedDef(def);
                foreach (var cell in new[] { visible, hidden })
                {
                    var probe = (ProbeThing)ThingMaker.MakeThing(def);
                    GenSpawn.Spawn(probe, cell, map);
                    probes.Add(probe);
                }
                var itemDef = new ThingDef
                {
                    defName = "TotalFogProbeLateItem",
                    label = "late definition probe",
                    thingClass = typeof(ThingWithComps),
                    category = ThingCategory.Item,
                    drawerType = DrawerType.None,
                    modContentPack = def.modContentPack,
                    size = new IntVec2(1, 1),
                };
                AssignHash(itemDef);
                DefGenerator.AddImpliedDef(itemDef);
                var item1 = (ThingWithComps)ThingMaker.MakeThing(itemDef);
                var item2 = (ThingWithComps)ThingMaker.MakeThing(itemDef);
                late = new
                {
                    firstComps = item1.AllComps.Count(c => c is CompFog),
                    secondComps = item2.AllComps.Count(c => c is CompFog),
                    definitionEntries = itemDef.comps.Count(c => c.compClass == typeof(CompFog)),
                };
            },
            cancellationToken
        );
        try
        {
            await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = (visible.x + hidden.x) / 2, z = (visible.z + hidden.z) / 2 },
                cancellationToken: cancellationToken
            );
            await ctx.Game.FramesAsync(15, cancellationToken);
            foreach (var probe in probes)
            {
                var hover = await ctx.Tools.CallAsync(
                    "rimworld/set_hover_target",
                    new
                    {
                        x = probe.Position.x,
                        z = probe.Position.z,
                        settleMs = 0,
                        durationMs = 5000,
                    },
                    cancellationToken: cancellationToken
                );
                hoverResults.Add(
                    new
                    {
                        hover.Success,
                        hover.Result,
                        hover.Error,
                    }
                );
                await ctx.Game.FramesAsync(60, cancellationToken);
            }
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        late,
                        hoverResults,
                        visible = new { visible.x, visible.z },
                        hidden = new { hidden.x, hidden.z },
                        probes = probes
                            .Select(p => new
                            {
                                id = p.ThingID,
                                x = p.Position.x,
                                z = p.Position.z,
                                phases = p.Phases,
                                overlays = p.Overlays,
                                tooltips = p.Tooltips,
                                mouseovers = p.Mouseovers,
                            })
                            .ToArray(),
                    },
                cancellationToken
            );
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    foreach (var probe in probes)
                        if (!probe.Destroyed)
                            probe.Destroy();
                },
                cancellationToken
            );
        }
    }

    internal static void AssignHash(ThingDef def)
    {
        var used = new HashSet<ushort>(DefDatabase<ThingDef>.AllDefs.Select(d => d.shortHash));
        for (int value = ushort.MaxValue; value > 0; value--)
            if (!used.Contains((ushort)value))
            {
                def.shortHash = (ushort)value;
                return;
            }
        throw new InvalidOperationException("No available test definition hash.");
    }
}

public sealed class ProbeThing : Thing
{
    public readonly int[] Phases = new int[3];
    public int Overlays,
        Tooltips,
        Mouseovers;
    public override string LabelMouseover
    {
        get
        {
            Mouseovers++;
            return "visibility probe";
        }
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        int index = (int)phase;
        if ((uint)index < Phases.Length)
            Phases[index]++;
    }

    public override void DrawGUIOverlay() => Overlays++;

    public override TipSignal GetTooltip()
    {
        Tooltips++;
        return new TipSignal("visibility probe");
    }
}
