using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Watchtowers;

public sealed class MapComponent_Watchtowers : MapComponent
{
    private static System.WeakReference<Game> countedGame;
    private readonly Game ownerGame;
    internal static int SpawnedTowerCount { get; private set; }
    internal readonly List<Building_Watchtower> Towers = new();
    private readonly HashSet<Building_Watchtower> dirtyDrawings = new();
    private readonly Dictionary<Pawn, Building_Watchtower> occupants = new();
    private bool[] elevatedBlockers;

    public MapComponent_Watchtowers(Map map)
        : base(map)
    {
        ownerGame = Current.Game;
        if (countedGame == null || !countedGame.TryGetTarget(out var game) || game != Current.Game)
        {
            countedGame = new System.WeakReference<Game>(Current.Game);
            SpawnedTowerCount = 0;
        }
    }

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

    internal void InvalidateSight()
    {
        elevatedBlockers = null;
        foreach (var tower in Towers)
            tower.SightDirty = true;
    }

    internal Building_Watchtower For(Thing thing)
    {
        var pawn = thing as Pawn ?? (thing as Corpse)?.InnerPawn;
        return pawn != null && occupants.TryGetValue(pawn, out var tower) ? tower : null;
    }

    internal void Register(Building_Watchtower tower)
    {
        if (!Towers.Contains(tower))
        {
            Towers.Add(tower);
            SpawnedTowerCount++;
        }
        dirtyDrawings.Add(tower);
        map.GetVisibility().DirtyTowerFog(tower);
    }

    internal void Unregister(Building_Watchtower tower)
    {
        if (tower.Occupant != null)
            Untrack(tower.Occupant);
        if (tower.Rescuer != null)
            Untrack(tower.Rescuer);
        if (Towers.Remove(tower) && countedGame.TryGetTarget(out var game) && game == ownerGame)
            SpawnedTowerCount--;
        dirtyDrawings.Remove(tower);
        map.GetVisibility().DirtyTowerFog(tower);
    }

    public override void MapRemoved()
    {
        if (countedGame.TryGetTarget(out var game) && game == ownerGame)
            SpawnedTowerCount -= Towers.Count;
        Towers.Clear();
        occupants.Clear();
        dirtyDrawings.Clear();
        base.MapRemoved();
    }

    internal void Track(Building_Watchtower tower)
    {
        if (tower.Occupant != null)
            occupants[tower.Occupant] = tower;
        if (tower.Rescuer != null)
            occupants[tower.Rescuer] = tower;
    }

    internal void Untrack(Pawn pawn) => occupants.Remove(pawn);

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
        DrawingChanged(cell);
        bool changed = false;
        if (elevatedBlockers != null)
        {
            int index = map.cellIndices.CellToIndex(cell);
            bool tall = IsTall(cell);
            changed = elevatedBlockers[index] != tall;
            elevatedBlockers[index] = tall;
        }
        if (!changed)
            return;
        foreach (var tower in Towers)
        {
            if (
                tower.Occupant?.Spawned == true
                && tower.Position.InHorDistOf(cell, FogSettings.TowerMaxSightRange + 1)
            )
            {
                tower.SightDirty = true;
                var source = tower.Occupant.TryGetComp<CompFog>()?.FieldOfViewWatcher;
                if (source != null)
                    map.GetVisibility().QueueRefresh(source);
            }
        }
    }

    internal void DrawingChanged(IntVec3 cell)
    {
        foreach (var tower in Towers)
            if (tower.Position.InHorDistOf(cell, 4))
                dirtyDrawings.Add(tower);
    }

    public override void MapComponentUpdate()
    {
        if (dirtyDrawings.Count == 0)
            return;
        foreach (var tower in dirtyDrawings)
            if (tower.Spawned)
                try
                {
                    tower.RebuildWallJoins();
                }
                catch (Exception error)
                {
                    // Keep the previous drawing and retry only on a new native
                    // neighbour invalidation, not every frame after a bad wall.
                    Log.Error("Total Fog: watchtower wall drawing failed: " + error);
                }
        dirtyDrawings.Clear();
    }
}
