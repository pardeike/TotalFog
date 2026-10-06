using TotalFog.Presentation;
using TotalFog.Utils;
using Verse;
namespace TotalFog.Detours;
public static class DesignatorPrefix
{
    public static bool CanDesignateCell_Prefix(IntVec3 c, Designator __instance, ref AcceptanceReport __result)
    {
        var map = __instance.Map;
        if (map == null || !c.InBounds(map) || DiscoveryOverlays.IsKnown(map, map.cellIndices.CellToIndex(c))) return true;
        __result = false; return false;
    }
    public static bool CanDesignateThing_Prefix(Thing t, ref AcceptanceReport __result)
    {
        if (t.IsFogVisible()) return true;
        __result = false; return false;
    }
}
