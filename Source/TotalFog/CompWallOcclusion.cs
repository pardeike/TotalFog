// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using Verse;

namespace TotalFog;

public class CompWallOcclusion : FogSubcomponent
{
    private Map map;
    private CellRect occupied;
    private IntVec3 position = IntVec3.Invalid;
    private Rot4 rotation = Rot4.Invalid;
    private bool blocking;
    private int nextCheck;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        map = parent.Map;
        occupied = parent.OccupiedRect();
        blocking = false;
        Refresh();
    }

    public override void ReceiveCompSignal(string signal) => Refresh();

    public override void CompTick()
    {
        int tick = Find.TickManager.TicksGame;
        if (tick < nextCheck)
            return;
        nextCheck = tick + 30;
        Refresh();
    }

    private void Refresh()
    {
        if (!parent.Spawned || parent is not Building building)
            return;
        if (map != parent.Map || position != parent.Position || rotation != parent.Rotation)
        {
            Remove();
            map = parent.Map;
            position = parent.Position;
            rotation = parent.Rotation;
            occupied = parent.OccupiedRect();
        }
        bool value = parent.def.blockLight && !building.CanBeSeenOver();
        if (value == blocking)
            return;
        blocking = value;
        foreach (var cell in occupied)
            if (cell.InBounds(map))
                map.GetVisibility().SetWallBlocker(cell, value);
    }

    public override void PostDeSpawn(Map previousMap) => Remove();

    private void Remove()
    {
        if (!blocking)
            return;
        foreach (var cell in occupied)
            if (cell.InBounds(map))
                map.GetVisibility().SetWallBlocker(cell, false);
        blocking = false;
    }
}
