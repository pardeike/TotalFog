using System.Collections.Generic;
using RimWorld;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Watchtowers;

public sealed class MapComponent_Watchtowers : MapComponent
{
    internal readonly List<Building_Watchtower> Towers = new();
    private readonly HashSet<Building_Watchtower> dirtyDrawings = new();
    private readonly Dictionary<Pawn, Building_Watchtower> occupants = new();
    private bool[] elevatedBlockers;

    public MapComponent_Watchtowers(Map map)
        : base(map) { }

    internal bool[] ElevatedBlockers
    {
        get
        {
            if (elevatedBlockers != null)
                return elevatedBlockers;
            elevatedBlockers = new bool[map.cellIndices.NumGridCells];
            foreach (var cell in map.AllCells)
                elevatedBlockers[map.cellIndices.CellToIndex(cell)] = IsTall(cell);
            return elevatedBlockers;
        }
    }

    internal Building_Watchtower For(Thing thing)
    {
        var pawn = thing as Pawn ?? (thing as Corpse)?.InnerPawn;
        return pawn != null && occupants.TryGetValue(pawn, out var tower) ? tower : null;
    }

    internal void Register(Building_Watchtower tower)
    {
        if (!Towers.Contains(tower))
            Towers.Add(tower);
        dirtyDrawings.Add(tower);
    }

    internal void Unregister(Building_Watchtower tower)
    {
        Untrack(tower);
        Towers.Remove(tower);
        dirtyDrawings.Remove(tower);
    }

    internal void Track(Building_Watchtower tower)
    {
        if (tower.Occupant != null)
            occupants[tower.Occupant] = tower;
    }

    internal void Untrack(Building_Watchtower tower)
    {
        if (tower.Occupant != null)
            occupants.Remove(tower.Occupant);
    }

    internal static bool LowWall(Building building) =>
        building != null
        && building is not Building_Watchtower
        && !building.def.building.isNaturalRock
        && (
            building is Building_Door
            || (building.def.graphicData?.linkFlags & LinkFlags.Wall) == LinkFlags.Wall
        );

    internal bool IsTall(IntVec3 cell)
    {
        if (cell.Roofed(map))
            return true;
        var building = cell.GetEdifice(map);
        if (building != null && !LowWall(building) && building.def.Fillage == FillCategory.Full)
            return true;
        return map.GetVisibility().treeBlockerCells[map.cellIndices.CellToIndex(cell)];
    }

    internal void CellChanged(IntVec3 cell)
    {
        if (!cell.InBounds(map) || Towers.Count == 0)
            return;
        if (elevatedBlockers != null)
            elevatedBlockers[map.cellIndices.CellToIndex(cell)] = IsTall(cell);
        foreach (var tower in Towers)
        {
            if (tower.Position.InHorDistOf(cell, 4))
                dirtyDrawings.Add(tower);
            if (
                tower.Occupant?.Spawned == true
                && tower.Position.InHorDistOf(cell, FogSettings.TowerMaxSightRange + 1)
            )
                tower.SightDirty = true;
        }
    }

    public override void MapComponentUpdate()
    {
        if (dirtyDrawings.Count == 0)
            return;
        foreach (var tower in dirtyDrawings)
            if (tower.Spawned)
                tower.RebuildWallJoins();
        dirtyDrawings.Clear();
    }
}
