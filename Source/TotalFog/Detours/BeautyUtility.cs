using Verse;

namespace TotalFog.Detours;

public static class BeautyUtility
{
    public static void FillBeautyRelevantCells_Postfix(Map map)
    {
        // Map.GetComponent scans the component list. Resolve it once for the
        // entire sample set rather than once per sampled cell.
        var fog = map?.GetVisibility();
        RimWorld.BeautyUtility.beautyRelevantCells.RemoveAll(c =>
            !Presentation.CellVisibility.IsCurrent(map, c, fog));
    }
}
