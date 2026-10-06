using Verse;
namespace TotalFog.Presentation;

internal static class DiscoveryOverlays
{
    // All three engine grids own the same private Map field. Harmony supplies
    // it directly, without per-cell reflection or an incompatible instance type.
    public static void OverlayPostfix(int index, Map ___map, ref bool __result)
    {
        if (__result) __result = IsKnown(___map, index);
    }
    internal static bool IsKnown(Map map, int index)
    {
        if (map == null || ThingVisibility.Bypass(map)) return true;
        var fog = map.GetVisibility();
        return !fog.Initialized || (uint)index < fog.knownCells.Length && fog.knownCells[index];
    }
}
