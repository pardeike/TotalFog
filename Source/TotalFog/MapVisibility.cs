// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using TotalFog.Core;
using Verse;

namespace TotalFog;

/// <summary>RimWorld ownership and side effects around the game-independent coverage grid.</summary>
public class MapVisibility : MapComponent
{
    private const int SectionSize = 17;
    private const string OuterSpaceBiome = "OuterSpaceBiome";
    private readonly VisibilityGrid coverage;
    private readonly ListenerCell<CompVisibility>[] hiddenAt;
    private readonly Dictionary<int, List<CompSightModifier>> affectersAt = new();
    private readonly Dictionary<int, List<CompTreeOcclusion>> treesAt = new();
    private readonly Dictionary<(int faction, int cell), Designation> miningAt = new();
    private readonly List<Building_VisionConsole> consoles = new();
    private readonly List<Building_VisionCamera> cameras = new();
    private readonly HashSet<int> dirtySections = new();
    private readonly LinkedList<CompSightSource> dirtySources = new();
    private readonly List<CompSightSource> blockerSources = new();
    private int[] fullVisibility;
    private bool discoveryDirty;
    private int primaryPlayerFactionId;
    private Faction primaryPlayerFaction;
    private readonly Dictionary<int, bool[]> factionDiscovery = new();
    private readonly List<Faction> otherPlayerFactions = new();
    private List<int> savedDiscoveryFactions;
    private Faction renderedFaction;
    public readonly List<CompSightSource> fowWatchers = new();
    public readonly bool[] viewBlockerCells,
        treeBlockerCells;
    private readonly int[] wallBlockers,
        treeBlockers;
    public readonly int mapSizeX;
    public bool[] knownCells;
    public bool Initialized { get; private set; }
    public VisibilityGrid Coverage => coverage;
    public bool workingCameraConsole => HasWorkingCameraConsole(Faction.OfPlayer);

    internal bool HasWorkingCameraConsole(Faction faction)
    {
        if (!FogSettings.NeedWatcher)
            return true;
        for (int i = 0; i < consoles.Count; i++)
            if (consoles[i].Faction == faction && consoles[i].WorkingNow && consoles[i].Manned)
                return true;
        return false;
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

    internal int PrimaryPlayerFactionId
    {
        get
        {
            if (primaryPlayerFactionId == 0)
                _ = PrimaryPlayerFaction;
            return primaryPlayerFactionId;
        }
    }

    internal Faction PrimaryPlayerFaction
    {
        get
        {
            if (primaryPlayerFaction != null)
                return primaryPlayerFaction;
            var factions = Find.FactionManager.AllFactionsListForReading;
            if (primaryPlayerFactionId != 0)
                primaryPlayerFaction = factions.FirstOrDefault(faction =>
                    faction.loadID == primaryPlayerFactionId
                );
            else
            {
                var owner = map.ParentFaction;
                primaryPlayerFaction =
                    owner?.IsPlayer == true
                        ? owner
                        : factions
                            .Where(faction => faction.IsPlayer)
                            .OrderBy(faction => faction.loadID)
                            .FirstOrDefault();
                primaryPlayerFactionId = primaryPlayerFaction?.loadID ?? 0;
            }
            return primaryPlayerFaction;
        }
    }

    internal IReadOnlyList<Faction> OtherPlayerFactions => otherPlayerFactions;

    private int Key(Faction faction) =>
        faction.IsPlayer && faction.loadID == PrimaryPlayerFactionId
            ? 0
            : checked(faction.loadID + 1);

    public bool[] GetFactionKnownCells(Faction faction) =>
        faction == null ? null
        : Key(faction) == 0 ? knownCells
        : factionDiscovery.TryGetValue(faction.loadID, out var known) ? known
        : null;

    public bool IsKnown(Faction faction, int index)
    {
        var known = GetFactionKnownCells(faction);
        return known != null && (uint)index < known.Length && known[index];
    }

    public int[] GetFactionShownCells(Faction faction) =>
        faction == null ? null
        : map.Biome.defName == OuterSpaceBiome ? SpaceVisibility()
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
            || map.Biome.defName == OuterSpaceBiome
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

    public int SurveillanceCameraCount() => SurveillanceCameraCount(Faction.OfPlayer);

    public int SurveillanceCameraCount(Faction faction)
    {
        int count = 0;
        for (int i = 0; i < cameras.Count; i++)
            if (cameras[i].Faction == faction && cameras[i].IsPowered())
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
            miningAt[(Key(Faction.OfPlayer), coverage.Index(cell.x, cell.z))] = des;
    }

    public void DeregisterMineDesignation(Designation des)
    {
        var cell = des.target.Cell;
        if (coverage.InBounds(cell.x, cell.z))
            miningAt.Remove((Key(Faction.OfPlayer), coverage.Index(cell.x, cell.z)));
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
        // Native multiplayer changes the viewing faction independently of the
        // map's simulation owner. Cached section geometry belongs to that view.
        if (Initialized && map == Find.CurrentMap && renderedFaction != Faction.OfPlayer)
        {
            renderedFaction = Faction.OfPlayer;
            map.mapDrawer.RegenerateEverythingNow();
            dirtySections.Clear();
        }
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
        // IsPlayerHome and ColonistsSpawnedCount use the local player faction.
        // Both clients must seed a newly generated colony's discovery for its owner.
        if (
            map.ParentFaction?.IsPlayer == true
            && !map.mapPawns.AllPawnsSpawned.Any(pawn =>
                pawn.Faction == PrimaryPlayerFaction && pawn.RaceProps.Humanlike && !pawn.IsPrisoner
            )
        )
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
        if (map.Biome.defName == OuterSpaceBiome || FogSettings.MapRevealAtStart)
            for (int i = 0; i < coverage.CellCount; i++)
                RevealCell(i);
        TotalFogMod.LogMessage(
            $"Fog initial section regeneration begins after {startup.ElapsedMilliseconds} ms."
        );
        // The engine owns drawing resources. Fog only changes print eligibility
        // and adds its overlay; it never captures GPU data or archives drawings.
        map.mapDrawer.RegenerateEverythingNow();
        renderedFaction = Faction.OfPlayer;
        dirtySections.Clear();
        TotalFogMod.LogMessage(
            $"Fog initialization finished after {startup.ElapsedMilliseconds} ms."
        );
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref primaryPlayerFactionId, "totalFogPrimaryPlayerFaction");
        DataExposeUtility.LookBoolArray(ref knownCells, coverage.CellCount, "revealedCells");
        if (Scribe.mode == LoadSaveMode.Saving)
            savedDiscoveryFactions = factionDiscovery.Keys.OrderBy(id => id).ToList();
        Scribe_Collections.Look(
            ref savedDiscoveryFactions,
            "totalFogDiscoveryFactions",
            LookMode.Value
        );
        if (savedDiscoveryFactions != null)
            foreach (int id in savedDiscoveryFactions)
            {
                factionDiscovery.TryGetValue(id, out var known);
                DataExposeUtility.LookBoolArray(
                    ref known,
                    coverage.CellCount,
                    "totalFogRevealedFaction" + id
                );
                factionDiscovery[id] = known;
            }
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            coverage.LoadKnown(knownCells);
            knownCells = coverage.Known;
            savedDiscoveryFactions = null;
        }
    }

    public void RevealCell(int index)
    {
        if ((uint)index >= coverage.CellCount || knownCells[index])
            return;
        knownCells[index] = true;
        DiscoveryChanged(index);
        VisibilityChanged(PrimaryPlayerFaction, index);
    }

    public void IncrementSeen(Faction faction, int index)
    {
        if (faction == null || (uint)index >= coverage.CellCount)
            return;
        int key = Key(faction);
        bool discovered =
            faction.IsPlayer
            && (
                key == 0
                    ? !knownCells[index]
                    : !factionDiscovery.TryGetValue(faction.loadID, out var knownBefore)
                        || !knownBefore[index]
            );
        bool changed = coverage.Add(key, index);
        if (!faction.IsPlayer || !changed)
            return;
        if (key != 0)
        {
            if (!otherPlayerFactions.Contains(faction))
                otherPlayerFactions.Add(faction);
            if (!factionDiscovery.TryGetValue(faction.loadID, out var known))
            {
                factionDiscovery.Add(faction.loadID, known = new bool[coverage.CellCount]);
                if (FogSettings.MapRevealAtStart || map.Biome.defName == OuterSpaceBiome)
                    for (int i = 0; i < known.Length; i++)
                        known[i] = true;
            }
            known[index] = true;
        }
        if (discovered)
            DiscoveryChanged(index, faction);
        VisibilityChanged(faction, index);
    }

    public void DecrementSeen(Faction faction, int index)
    {
        if (faction == null || (uint)index >= coverage.CellCount)
            return;
        if (coverage.Remove(Key(faction), index) && faction.IsPlayer)
            VisibilityChanged(faction, index);
    }

    private void DiscoveryChanged(int index, Faction faction = null)
    {
        discoveryDirty = true;
        if (
            (faction == null || faction == Faction.OfPlayer)
            && miningAt.TryGetValue(
                (Key(faction ?? PrimaryPlayerFaction), index),
                out var designation
            )
            && CellIndicesUtility.IndexToCell(index, mapSizeX).GetFirstMineable(map) == null
        )
            designation.Delete();
        // Optional minimap integration owns reflection and feature detection.
        Compatibility.MinimapIntegration.Reveal(index);
    }

    private void VisibilityChanged(Faction faction, int index)
    {
        int x = coverage.X(index),
            z = coverage.Z(index);
        foreach (
            int section in GridGeometry.AffectedSections(
                x,
                z,
                coverage.Width,
                coverage.Height,
                SectionSize
            )
        )
            dirtySections.Add(section);
        if (!Initialized)
            return;
        ref var listeners = ref hiddenAt[index];
        for (int i = 0; i < listeners.Count; i++)
        {
            if (otherPlayerFactions.Count != 0)
                listeners[i].RecordObservation(faction, this);
            listeners[i].UpdateVisibility(true);
        }
    }

    private void FlushDirty()
    {
        int columns = (coverage.Width + SectionSize - 1) / SectionSize;
        foreach (int section in dirtySections)
        {
            map.mapDrawer.MapMeshDirty(
                new IntVec3(section % columns * SectionSize, 0, section / columns * SectionSize),
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
