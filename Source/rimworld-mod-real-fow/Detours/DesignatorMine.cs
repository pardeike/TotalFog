using HarmonyLib;
using RimWorld;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class DesignatorMine
{
    public static void CanDesignateCell_Postfix(IntVec3 c, ref Designator __instance, ref AcceptanceReport __result)
    {
        if (__result.Accepted)
        {
            return;
        }

        var value = Traverse.Create(__instance).Property("Map").GetValue<Map>();
        if (value.designationManager.DesignationAt(c, DesignationDefOf.Mine) != null)
        {
            return;
        }

        var mapComponentSeenFog = value.GetMapComponentSeenFog();
        if (mapComponentSeenFog != null && c.InBounds(value) &&
            !mapComponentSeenFog.knownCells[value.cellIndices.CellToIndex(c)])
        {
            __result = true;
        }
    }
}