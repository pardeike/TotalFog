using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using TotalFog.Utils;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class ObservationScenarios
{
    [Tool(
        "totalfog/static_interface",
        Description = "Verify that a remembered static item loses selection and stops live UI callbacks until sight returns, while remembered render eligibility remains."
    )]
    public static async Task<object> StaticInterface(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        StaticInterfaceProbe probe = null;
        ThingWithComps proxy = null;
        MapVisibility fog = null;
        int index = -1;
        bool addedSight = false;
        object observed = null,
            hidden = null,
            revealed = null;
        bool observedThroughProxy = false,
            hiddenThroughProxy = false,
            revealedThroughProxy = false;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load a test map first.");
                    fog = map.GetComponent<MapVisibility>();
                    var cell = map.AllCells.First(c =>
                        !map.fogGrid.IsFogged(c)
                        && fog.knownCells[map.cellIndices.CellToIndex(c)]
                        && !fog.IsShown(Faction.OfPlayer, c)
                        && c.Standable(map)
                        && c.GetThingList(map).Count == 0
                    );
                    index = map.cellIndices.CellToIndex(cell);
                    var def = new ThingDef
                    {
                        defName = "TotalFogStaticInterfaceProbe",
                        label = "static interface probe",
                        thingClass = typeof(StaticInterfaceProbe),
                        category = ThingCategory.Item,
                        drawerType = DrawerType.RealtimeOnly,
                        drawGUIOverlay = true,
                        hasTooltip = true,
                        selectable = true,
                        stackLimit = 10,
                        size = new IntVec2(1, 1),
                        modContentPack = LoadedModManager.GetMod<TotalFogMod>().Content,
                    };
                    PresentationScenarios.AssignHash(def);
                    DefGenerator.AddImpliedDef(def);
                    probe = (StaticInterfaceProbe)ThingMaker.MakeThing(def);
                    GenSpawn.Spawn(probe, cell, map);
                    probe.SetForbidden(true, false);
                    var proxyCell = map.AllCells.First(c =>
                        fog.IsShown(Faction.OfPlayer, c)
                        && c.Standable(map)
                        && c.GetThingList(map).Count == 0
                    );
                    var proxyDef = new ThingDef
                    {
                        defName = "TotalFogSelectionProxyProbe",
                        label = "selection proxy probe",
                        thingClass = typeof(ThingWithComps),
                        category = ThingCategory.Item,
                        drawerType = DrawerType.None,
                        size = new IntVec2(1, 1),
                        modContentPack = def.modContentPack,
                        comps = new List<CompProperties>
                        {
                            new() { compClass = typeof(CompSelectProxy) },
                        },
                    };
                    PresentationScenarios.AssignHash(proxyDef);
                    DefGenerator.AddImpliedDef(proxyDef);
                    proxy = (ThingWithComps)ThingMaker.MakeThing(proxyDef);
                    GenSpawn.Spawn(proxy, proxyCell, map);
                    proxy.TryGetComp<CompSelectProxy>().thingToSelect = probe;
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    Find.Selector.Select(proxy, false, false);
                    observedThroughProxy = Find.Selector.IsSelected(probe);
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = probe.Position.x, z = probe.Position.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException(
                    "Could not position the test camera: " + camera.Error
                );
            var hover = await ctx.Tools.CallAsync(
                "rimworld/set_hover_target",
                new
                {
                    x = probe.Position.x,
                    z = probe.Position.z,
                    settleMs = 0,
                    durationMs = 15000,
                },
                cancellationToken: cancellationToken
            );
            if (!hover.Succeeded())
                throw new InvalidOperationException(
                    "Could not hover the test item: " + hover.Error
                );
            await ExposeMouseover(ctx, cancellationToken);
            await ctx.Game.FramesAsync(60, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Find.Selector.Select(proxy, false, false);
                    observed = InterfaceState(probe);
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                    // An offscreen simulation change must not reach custom live UI.
                    probe.stackCount = 7;
                    Find.Selector.Select(probe, false, false);
                    Find.Selector.Select(proxy, false, false);
                    hiddenThroughProxy = Find.Selector.IsSelected(probe);
                    probe.ResetCounters();
                },
                cancellationToken
            );
            await ExposeMouseover(ctx, cancellationToken);
            await ctx.Game.StepTicksAsync(13, cancellationToken: cancellationToken);
            await ctx.Game.FramesAsync(60, cancellationToken);
            var proxyHover = await ctx.Tools.CallAsync(
                "rimworld/set_hover_target",
                new
                {
                    x = proxy.Position.x,
                    z = proxy.Position.z,
                    settleMs = 0,
                    durationMs = 15000,
                },
                cancellationToken: cancellationToken
            );
            if (!proxyHover.Succeeded())
                throw new InvalidOperationException(
                    "Could not hover the visible proxy: " + proxyHover.Error
                );
            await ctx.Game.FramesAsync(60, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    hidden = InterfaceState(probe);
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    probe.ResetCounters();
                },
                cancellationToken
            );
            var revealHover = await ctx.Tools.CallAsync(
                "rimworld/set_hover_target",
                new
                {
                    x = probe.Position.x,
                    z = probe.Position.z,
                    settleMs = 0,
                    durationMs = 15000,
                },
                cancellationToken: cancellationToken
            );
            if (!revealHover.Succeeded())
                throw new InvalidOperationException(
                    "Could not hover the revealed item: " + revealHover.Error
                );
            await ctx.Game.FramesAsync(60, cancellationToken);
            revealed = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Find.Selector.Select(proxy, false, false);
                    revealedThroughProxy = Find.Selector.IsSelected(probe);
                    return InterfaceState(probe);
                },
                cancellationToken
            );
            return new
            {
                observed,
                hidden,
                revealed,
                observedThroughProxy,
                hiddenThroughProxy,
                revealedThroughProxy,
            };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (addedSight)
                        fog.DecrementSeen(Faction.OfPlayer, index);
                    if (probe != null && !probe.Destroyed)
                        probe.Destroy();
                    if (proxy != null && !proxy.Destroyed)
                        proxy.Destroy();
                },
                CancellationToken.None
            );
            await ctx.Tools.CallAsync(
                "rimworld/clear_hover_target",
                new { },
                cancellationToken: CancellationToken.None
            );
        }
    }

    private static object InterfaceState(StaticInterfaceProbe probe) =>
        new
        {
            state = State(probe),
            selected = Find.Selector.IsSelected(probe),
            probe.stackCount,
            probe.Overlays,
            probe.Tooltips,
            probe.Mouseovers,
            phases = probe.Phases.ToArray(),
            readoutEnabled = Find.MainTabsRoot.OpenTab == null,
        };

    private static async Task ExposeMouseover(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        bool open = await ctx.MainThread.InvokeAsync(
            () => Find.MainTabsRoot.OpenTab != null,
            cancellationToken
        );
        if (!open)
            return;
        var close = await ctx.Tools.CallAsync(
            "rimworld/close_main_tab",
            new { },
            cancellationToken: cancellationToken
        );
        if (!close.Succeeded())
            throw new InvalidOperationException(
                "Could not expose the mouseover readout: " + close.Error
            );
    }

    [Tool(
        "totalfog/static_observation",
        Description = "Exercise newly spawned versus remembered items in an explored unseen cell and real deferred static-target messages."
    )]
    public static async Task<object> StaticObservation(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        bool keepForSave = false
    )
    {
        ThingWithComps remembered = null,
            fresh = null;
        Map map = null;
        MapVisibility fog = null;
        IntVec3 cell = IntVec3.Invalid,
            freshCell = IntVec3.Invalid;
        int index = -1,
            freshIndex = -1;
        bool addedSight = false,
            addedFreshSight = false,
            delay = FogSettings.DelayAlertsUntilSeen;
        bool completed = false;
        object initial = null,
            observed = null,
            lostSight = null,
            newlySpawned = null;
        int pendingBefore = 0,
            pendingQueued = 0;
        string rememberedMessage = null,
            freshMessage = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load a test map first.");
                    fog = map.GetComponent<MapVisibility>();
                    var observers = map
                        .mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer)
                        .ToArray();
                    var cells = map
                        .AllCells.Where(c =>
                            !map.fogGrid.IsFogged(c)
                            && fog.knownCells[map.cellIndices.CellToIndex(c)]
                            && !fog.IsShown(Faction.OfPlayer, c)
                            && c.Standable(map)
                            && c.GetThingList(map).Count == 0
                        )
                        .OrderByDescending(c => observers.Min(p => c.DistanceToSquared(p.Position)))
                        .Take(2)
                        .ToArray();
                    cell = cells[0];
                    freshCell = cells[1];
                    index = map.cellIndices.CellToIndex(cell);
                    freshIndex = map.cellIndices.CellToIndex(freshCell);
                    remembered = (ThingWithComps)ThingMaker.MakeThing(ThingDefOf.Steel);
                    GenSpawn.Spawn(remembered, cell, map);
                    remembered.SetForbidden(true, false);
                    initial = State(remembered);
                    var counts = fog.GetFactionShownCells(Faction.OfPlayer);
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    observed = State(remembered);
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                    lostSight = State(remembered);
                    // Separate cells avoid spawn collision/move-aside semantics.
                    fresh = (ThingWithComps)ThingMaker.MakeThing(ThingDefOf.Gold);
                    GenSpawn.Spawn(fresh, freshCell, map);
                    fresh.SetForbidden(true, false);
                    newlySpawned = State(fresh);
                    rememberedMessage = "Total Fog remembered item " + remembered.ThingID;
                    freshMessage = "Total Fog new item " + fresh.ThingID;
                    var manager = map.GetComponent<DeferredNotifications>();
                    pendingBefore = manager.PendingCount;
                    FogSettings.DelayAlertsUntilSeen = true;
                    Messages.Message(
                        new Message(
                            rememberedMessage,
                            MessageTypeDefOf.NeutralEvent,
                            new LookTargets(remembered)
                        ),
                        true
                    );
                    Messages.Message(
                        new Message(
                            freshMessage,
                            MessageTypeDefOf.NeutralEvent,
                            new LookTargets(fresh)
                        ),
                        true
                    );
                    pendingQueued = manager.PendingCount;
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    fog.IncrementSeen(Faction.OfPlayer, freshIndex);
                    addedFreshSight = true;
                },
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(35, cancellationToken: cancellationToken);
            var result = await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        x = cell.x,
                        z = cell.z,
                        freshX = freshCell.x,
                        freshZ = freshCell.z,
                        known = fog.knownCells[index],
                        initial,
                        observed,
                        lostSight,
                        newlySpawned,
                        pendingBefore,
                        pendingQueued,
                        pendingReplayed = map.GetComponent<DeferredNotifications>().PendingCount,
                        rememberedAfterReveal = State(remembered),
                        freshAfterReveal = State(fresh),
                        rememberedArchived = Find
                            .Archive.ArchivablesListForReading.OfType<Message>()
                            .Count(m => m.text == rememberedMessage),
                        freshArchived = Find
                            .Archive.ArchivablesListForReading.OfType<Message>()
                            .Count(m => m.text == freshMessage),
                        keptForSave = keepForSave,
                    },
                cancellationToken
            );
            completed = true;
            return result;
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (addedSight)
                        fog.DecrementSeen(Faction.OfPlayer, index);
                    if (addedFreshSight)
                        fog.DecrementSeen(Faction.OfPlayer, freshIndex);
                    if (!keepForSave || !completed)
                    {
                        if (remembered != null && !remembered.Destroyed)
                            remembered.Destroy();
                        if (fresh != null && !fresh.Destroyed)
                            fresh.Destroy();
                    }
                    FogSettings.DelayAlertsUntilSeen = delay;
                },
                CancellationToken.None
            );
        }
    }

    [Tool(
        "totalfog/owned_observation",
        Description = "Verify an owned wall does not create its own sight or observation, then becomes remembered only after real coverage. Includes periodic sight-source reconciliation."
    )]
    public static async Task<object> OwnedObservation(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        ThingWithComps wall = null;
        MapVisibility fog = null;
        int index = -1;
        bool addedSight = false;
        object initial = null,
            periodic = null,
            observed = null,
            remembered = null;
        object Inspect()
        {
            var comp = wall.TryGetComp<CompFog>();
            return new
            {
                state = State(wall),
                owned = wall.Faction == Faction.OfPlayer,
                coverageCount = fog.GetFactionShownCells(Faction.OfPlayer)[index],
                sightRange = comp.FieldOfViewWatcher.LastSightRange,
                currentInformation = (bool)
                    AccessTools
                        .Method(
                            typeof(CompFog).Assembly.GetType(
                                "TotalFog.Presentation.ThingVisibility"
                            ),
                            "IsVisible"
                        )
                        .Invoke(null, new object[] { wall, false }),
            };
        }
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load a test map first.");
                    fog = map.GetComponent<MapVisibility>();
                    var observers = map
                        .mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer)
                        .ToArray();
                    var cell = map
                        .AllCells.Where(c =>
                            !map.fogGrid.IsFogged(c)
                            && fog.knownCells[map.cellIndices.CellToIndex(c)]
                            && !fog.IsShown(Faction.OfPlayer, c)
                            && c.Standable(map)
                            && c.GetThingList(map).Count == 0
                        )
                        .OrderByDescending(c =>
                            observers.Length == 0
                                ? 0
                                : observers.Min(p => c.DistanceToSquared(p.Position))
                        )
                        .First();
                    index = map.cellIndices.CellToIndex(cell);
                    wall = (ThingWithComps)
                        ThingMaker.MakeThing(
                            ThingDefOf.Wall,
                            DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                        );
                    wall.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(wall, cell, map);
                    initial = Inspect();
                },
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(35, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    periodic = Inspect();
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    observed = Inspect();
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                    remembered = Inspect();
                },
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(35, cancellationToken: cancellationToken);
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        initial,
                        periodic,
                        observed,
                        remembered,
                        final = Inspect(),
                    },
                cancellationToken
            );
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (addedSight)
                        fog.DecrementSeen(Faction.OfPlayer, index);
                    if (wall != null && !wall.Destroyed)
                        wall.Destroy();
                },
                CancellationToken.None
            );
        }
    }

    [Tool(
        "totalfog/static_observation_state",
        Description = "Inspect saved observation state by exact fixture item IDs, or clean up those items after persistence testing."
    )]
    public static object StaticObservationState(
        string rememberedID,
        string freshID,
        bool cleanup = false
    )
    {
        var things = Find.CurrentMap.listerThings.AllThings;
        var remembered = things.OfType<ThingWithComps>().First(t => t.ThingID == rememberedID);
        var fresh = things.OfType<ThingWithComps>().First(t => t.ThingID == freshID);
        var result = new
        {
            remembered = State(remembered),
            fresh = State(fresh),
            cleanup,
        };
        if (cleanup)
        {
            remembered.Destroy();
            fresh.Destroy();
        }
        return result;
    }

    private static object State(ThingWithComps thing)
    {
        var comp = thing.TryGetComp<CompFog>();
        return new
        {
            id = thing.ThingID,
            x = thing.PositionHeld.x,
            z = thing.PositionHeld.z,
            thing.Spawned,
            thing.Destroyed,
            visible = thing.IsFogVisible(),
            hidden = comp.Hiddenable.Hidden,
            seen = comp.HideFromPlayer.SeenByPlayer,
            inSight = thing
                .Map.GetComponent<MapVisibility>()
                .IsShown(Faction.OfPlayer, thing.PositionHeld),
        };
    }
}

public sealed class StaticInterfaceProbe : ThingWithComps
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
            return "static interface probe " + stackCount;
        }
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false) =>
        Phases[(int)phase]++;

    public override void DrawGUIOverlay() => Overlays++;

    public override TipSignal GetTooltip()
    {
        Tooltips++;
        return new TipSignal("static interface probe " + stackCount);
    }

    public void ResetCounters()
    {
        Overlays = Tooltips = Mouseovers = 0;
        Array.Clear(Phases, 0, Phases.Length);
    }
}
