// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Core;
using Verse;

namespace TotalFog;

/// <summary>RimWorld ownership and side effects around the game-independent coverage grid.</summary>
public class MapVisibility : MapComponent
{
    private readonly VisibilityGrid coverage;
    private readonly ListenerCell<CompVisibility>[] hiddenAt;
    private readonly Dictionary<int, List<CompSightModifier>> affectersAt = new();
    private readonly Dictionary<int, List<CompTreeOcclusion>> treesAt = new();
    private readonly Dictionary<int, Designation> miningAt = new();
    private readonly List<Building_VisionConsole> consoles = new();
    private readonly List<Building_VisionCamera> cameras = new();
    private readonly HashSet<int> dirtySections = new();
    private readonly LinkedList<CompSightSource> dirtySources = new();
    private readonly List<CompSightSource> blockerSources = new();
    private int[] fullVisibility;
    private bool discoveryDirty;
    public readonly List<CompSightSource> fowWatchers = new();
    public readonly bool[] viewBlockerCells,
        treeBlockerCells;
    private readonly int[] wallBlockers,
        treeBlockers;
    public readonly int mapSizeX;
    public bool[] knownCells;
    public bool Initialized { get; private set; }
    public VisibilityGrid Coverage => coverage;
    public bool workingCameraConsole
    {
        get
        {
            if (!FogSettings.NeedWatcher)
                return true;
            for (int i = 0; i < consoles.Count; i++)
                if (consoles[i].WorkingNow && consoles[i].Manned)
                    return true;
            return false;
        }
    }

    public MapVisibility(Map map)
        : base(map)
    {
        coverage = new VisibilityGrid(map.Size.x, map.Size.z);
        mapSizeX = coverage.Width;
        hiddenAt = new ListenerCell<CompVisibility>[coverage.CellCount];
        knownCells = coverage.Known;
        viewBlockerCells = new bool[coverage.CellCount];
        treeBlockerCells = new bool[coverage.CellCount];
        wallBlockers = new int[coverage.CellCount];
        treeBlockers = new int[coverage.CellCount];
    }

    private static int Key(Faction faction) => faction.IsPlayer ? 0 : checked(faction.loadID + 1);

    public int[] GetFactionShownCells(Faction faction) =>
        faction == null ? null
        : map.Biome.defName == "OuterSpaceBiome" ? SpaceVisibility()
        : coverage.Counts(Key(faction));

    private int[] SpaceVisibility()
    {
        if (fullVisibility != null)
            return fullVisibility;
        fullVisibility = new int[coverage.CellCount];
        for (int i = 0; i < fullVisibility.Length; i++)
            fullVisibility[i] = 1;
        return fullVisibility;
    }

    public bool IsShown(Faction faction, IntVec3 cell) => IsShown(faction, cell.x, cell.z);

    public bool IsShown(Faction faction, int x, int z) =>
        coverage.InBounds(x, z)
        && faction != null
        && (
            map.IsPlayerHome && FogSettings.OnlyOutsideColony
            || map.Biome.defName == "OuterSpaceBiome"
            || coverage.IsVisible(Key(faction), x, z)
        );

    public void RegisterCameraConsole(Building_VisionConsole console)
    {
        if (!consoles.Contains(console))
            consoles.Add(console);
    }

    public void DeregisterCameraConsole(Building_VisionConsole console) => consoles.Remove(console);

    public void RegisterSurveillanceCamera(Building_VisionCamera camera)
    {
        if (!cameras.Contains(camera))
            cameras.Add(camera);
    }

    public void DeregisterSurveillanceCamera(Building_VisionCamera camera) =>
        cameras.Remove(camera);

    public int SurveillanceCameraCount()
    {
        int count = 0;
        for (int i = 0; i < cameras.Count; i++)
            if (cameras[i].IsPowered())
                count++;
        return count;
    }

    private void Add<T>(Dictionary<int, List<T>> index, T item, int x, int z)
    {
        if (!coverage.InBounds(x, z))
            return;
        int cell = coverage.Index(x, z);
        if (!index.TryGetValue(cell, out var list))
            index.Add(cell, list = new List<T>());
        if (!list.Contains(item))
            list.Add(item);
    }

    private void Remove<T>(Dictionary<int, List<T>> index, T item, int x, int z)
    {
        if (!coverage.InBounds(x, z))
            return;
        int cell = coverage.Index(x, z);
        if (index.TryGetValue(cell, out var list) && list.Remove(item) && list.Count == 0)
            index.Remove(cell);
    }

    public void RegisterCompHideFromPlayerPosition(CompVisibility comp, int x, int z)
    {
        if (!coverage.InBounds(x, z))
            return;
        hiddenAt[coverage.Index(x, z)].Add(comp);
    }

    public void DeregisterCompHideFromPlayerPosition(CompVisibility comp, int x, int z)
    {
        if (!coverage.InBounds(x, z))
            return;
        hiddenAt[coverage.Index(x, z)].Remove(comp);
    }

    public void RegisterCompAffectVisionPosition(CompSightModifier comp, int x, int z) =>
        Add(affectersAt, comp, x, z);

    public void DeregisterCompAffectVisionPosition(CompSightModifier comp, int x, int z) =>
        Remove(affectersAt, comp, x, z);

    public IReadOnlyList<CompSightModifier> VisionAffectersAt(IntVec3 cell) =>
        coverage.InBounds(cell.x, cell.z)
        && affectersAt.TryGetValue(coverage.Index(cell.x, cell.z), out var list)
            ? list
            : Array.Empty<CompSightModifier>();

    public void RegisterTreeViewBlocker(CompTreeOcclusion comp, int x, int z) =>
        Add(treesAt, comp, x, z);

    public void DeregisterTreeViewBlocker(CompTreeOcclusion comp, int x, int z) =>
        Remove(treesAt, comp, x, z);

    public bool IsTreeViewBlocker(int idx) => treesAt.ContainsKey(idx);

    public void SetWallBlocker(IntVec3 cell, bool value) => SetBlocker(cell, value, wallBlockers);

    public void SetTreeBlocker(IntVec3 cell, bool value) => SetBlocker(cell, value, treeBlockers);

    private void SetBlocker(IntVec3 cell, bool value, int[] counts)
    {
        if (!coverage.InBounds(cell.x, cell.z))
            return;
        int index = coverage.Index(cell.x, cell.z);
        counts[index] = value ? checked(counts[index] + 1) : Math.Max(0, counts[index] - 1);
        bool wasBlocked = viewBlockerCells[index],
            wasTree = treeBlockerCells[index];
        treeBlockerCells[index] = treeBlockers[index] > 0;
        viewBlockerCells[index] = wallBlockers[index] > 0 || treeBlockerCells[index];
        if (
            !Initialized
            || wasBlocked == viewBlockerCells[index] && wasTree == treeBlockerCells[index]
        )
            return;
        for (int i = 0; i < blockerSources.Count; i++)
        {
            var watcher = blockerSources[i];
            if (!watcher.parent.Position.InHorDistOf(cell, watcher.LastSightRange + 1))
                continue;
            var node = watcher.PendingRefresh ??= new LinkedListNode<CompSightSource>(watcher);
            if (node.List == null)
                dirtySources.AddLast(node);
        }
    }

    internal void CancelRefresh(CompSightSource source)
    {
        var node = source.PendingRefresh;
        if (node?.List == dirtySources)
            dirtySources.Remove(node);
    }

    internal void ChangeBlockerSourceRange(CompSightSource source, int range)
    {
        if ((source.LastSightRange > 0) == (range > 0))
            return;
        if (range > 0)
            blockerSources.Add(source);
        else
            blockerSources.Remove(source);
    }

    internal void UnregisterSource(CompSightSource source)
    {
        CancelRefresh(source);
        if (source.LastSightRange > 0)
            blockerSources.Remove(source);
        fowWatchers.Remove(source);
    }

    public void RegisterMineDesignation(Designation des)
    {
        var cell = des.target.Cell;
        if (coverage.InBounds(cell.x, cell.z))
            miningAt[coverage.Index(cell.x, cell.z)] = des;
    }

    public void DeregisterMineDesignation(Designation des)
    {
        var cell = des.target.Cell;
        if (coverage.InBounds(cell.x, cell.z))
            miningAt.Remove(coverage.Index(cell.x, cell.z));
    }

    public override void MapComponentTick()
    {
        if (!Initialized)
            Initialize();
        // Detach before publishing sight: callbacks may request another refresh.
        // Limit this pass to its initial entry count; callbacks can enqueue fresh work.
        int remaining = dirtySources.Count;
        while (remaining-- > 0 && dirtySources.First != null)
        {
            var source = dirtySources.First.Value;
            dirtySources.RemoveFirst();
            if (source.parent.Spawned && source.parent.Map == map)
                source.UpdateFoV(true);
        }
        FlushDirty();
    }

    public override void MapComponentUpdate()
    {
        // Sight can also change through paused signals and despawns. Publish
        // queued section changes without recomputing sight or waiting for a tick.
        if (dirtySections.Count != 0 || discoveryDirty)
            FlushDirty();
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            if (!Initialized)
                Initialize();
        });
    }

    private void Initialize()
    {
        var startup = System.Diagnostics.Stopwatch.StartNew();
        TotalFogMod.LogMessage(
            $"Fog initialization begins for map {map.uniqueID}, {map.Size.x}x{map.Size.z}."
        );
        // A load callback (including Pause on load's first tick) runs before
        // MapUpdate populates sunlight and accumulated lamp glow. Sample the
        // engine's ready lighting now, including on maps that are not viewed.
        map.skyManager.SkyManagerUpdate();
        map.glowGrid.GlowGridUpdate_First();
        foreach (var designation in map.designationManager.AllDesignations)
            if (designation.def == DesignationDefOf.Mine && !designation.target.HasThing)
                RegisterMineDesignation(designation);
        // All spawned comps have completed registration before map initialization publishes sight.
        Initialized = true;
        foreach (var thing in map.listerThings.AllThings)
        {
            if (!thing.Spawned)
                continue;
            var comp = thing.TryGetComp<CompFog>();
            comp?.ComponentsPositionTracker?.updatePosition();
            comp?.FieldOfViewWatcher?.UpdateFoV(true);
            comp?.HideFromPlayer?.UpdateVisibility(true);
        }
        TotalFogMod.LogMessage(
            $"Fog sight sources initialized after {startup.ElapsedMilliseconds} ms."
        );
        if (map.IsPlayerHome && map.mapPawns.ColonistsSpawnedCount == 0)
        {
            var start = MapGenerator.PlayerStartSpot;
            FieldOfView.Compute(
                coverage.Width,
                coverage.Height,
                start.x,
                start.z,
                FogSettings.BaseViewRange,
                viewBlockerCells,
                RevealCell
            );
        }
        if (map.Biome.defName == "OuterSpaceBiome" || FogSettings.MapRevealAtStart)
            for (int i = 0; i < coverage.CellCount; i++)
                RevealCell(i);
        TotalFogMod.LogMessage(
            $"Fog initial section regeneration begins after {startup.ElapsedMilliseconds} ms."
        );
        // The engine owns drawing resources. Fog only changes print eligibility
        // and adds its overlay; it never captures GPU data or archives drawings.
        map.mapDrawer.RegenerateEverythingNow();
        dirtySections.Clear();
        TotalFogMod.LogMessage(
            $"Fog initialization finished after {startup.ElapsedMilliseconds} ms."
        );
    }

    public override void ExposeData()
    {
        base.ExposeData();
        DataExposeUtility.LookBoolArray(ref knownCells, coverage.CellCount, "revealedCells");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            coverage.LoadKnown(knownCells);
            knownCells = coverage.Known;
        }
    }

    public void RevealCell(int index)
    {
        if ((uint)index >= coverage.CellCount || knownCells[index])
            return;
        knownCells[index] = true;
        DiscoveryChanged(index);
        VisibilityChanged(index);
    }

    public void IncrementSeen(Faction faction, int[] counts, int index)
    {
        if (faction == null || (uint)index >= coverage.CellCount)
            return;
        bool discovered = !knownCells[index];
        bool changed = coverage.Add(Key(faction), index);
        if (!faction.IsPlayer || !changed)
            return;
        if (discovered)
            DiscoveryChanged(index);
        VisibilityChanged(index);
    }

    public void DecrementSeen(Faction faction, int[] counts, int index)
    {
        if (faction == null || (uint)index >= coverage.CellCount)
            return;
        if (coverage.Remove(Key(faction), index) && faction.IsPlayer)
            VisibilityChanged(index);
    }

    private void DiscoveryChanged(int index)
    {
        discoveryDirty = true;
        if (
            miningAt.TryGetValue(index, out var designation)
            && CellIndicesUtility.IndexToCell(index, mapSizeX).GetFirstMineable(map) == null
        )
            designation.Delete();
        // Optional minimap integration owns reflection and feature detection.
        Compatibility.MinimapIntegration.Reveal(index);
    }

    private void VisibilityChanged(int index)
    {
        int x = coverage.X(index),
            z = coverage.Z(index);
        foreach (
            int section in GridGeometry.AffectedSections(x, z, coverage.Width, coverage.Height, 17)
        )
            dirtySections.Add(section);
        if (!Initialized)
            return;
        ref var listeners = ref hiddenAt[index];
        for (int i = 0; i < listeners.Count; i++)
            listeners[i].UpdateVisibility(true);
    }

    private void FlushDirty()
    {
        foreach (int section in dirtySections)
        {
            int columns = (coverage.Width + 16) / 17;
            map.mapDrawer.MapMeshDirty(
                new IntVec3(section % columns * 17, 0, section / columns * 17),
                FogDefOf.RealFogOfWar
            );
        }
        dirtySections.Clear();
        if (!discoveryDirty)
            return;
        map.fertilityGrid.Drawer.SetDirty();
        map.roofGrid.Drawer.SetDirty();
        map.terrainGrid.Drawer.SetDirty();
        discoveryDirty = false;
    }
}
