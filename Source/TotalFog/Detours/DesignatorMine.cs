using RimWorld;
using Verse;

namespace TotalFog.Detours;

public static class DesignatorMine
{
    public static void CanDesignateCell_Postfix(
        IntVec3 c,
        ref Designator __instance,
        ref AcceptanceReport __result
    )
    {
        if (__result.Accepted)
        {
            return;
        }

        var value = __instance.Map;
        if (value.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null)
        {
            return;
        }

        var mapComponentSeenFog = value.GetVisibility();
        if (
            mapComponentSeenFog != null
            && c.InBounds(value)
            && !mapComponentSeenFog.IsKnown(Faction.OfPlayer, value.cellIndices.CellToIndex(c))
        )
        {
            __result = true;
        }
    }
}
