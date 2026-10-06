using RimWorld;
using TotalFog.Presentation;
using Verse;
namespace TotalFog.Detours;
public static class DesignatorPlace
{
    public static void CanDesignateCell_Postfix(IntVec3 c, Designator_Place __instance, Rot4 ___placingRot, ref AcceptanceReport __result)
    {
        if (!__result.Accepted || __instance.Map == null) return;
        var map = __instance.Map;
        foreach (var cell in GenAdj.OccupiedRect(c, ___placingRot, __instance.PlacingDef.Size))
        {
            if (cell.InBounds(map) && DiscoveryOverlays.IsKnown(map, map.cellIndices.CellToIndex(cell))) continue;
            __result = "CannotPlaceInUndiscovered".Translate(); return;
        }
    }
}
