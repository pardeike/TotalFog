using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static partial class GasVisibility
{
    internal static int[] ShownCells(Map map)
    {
        if (ThingVisibility.Bypass(map))
            return null;
        var fog = map.GetVisibility();
        return fog.Initialized ? fog.GetFactionShownCells(Faction.OfPlayer) : null;
    }

    internal static bool AnyVisibleGasAt(GasGrid grid, IntVec3 cell, Map map, int[] shown)
    {
        int index = map.cellIndices.CellToIndex(cell);
        return (shown == null || (uint)index < shown.Length && shown[index] != 0)
            && grid.AnyGasAt(cell);
    }
}
