using HarmonyLib;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class DesignatorPrefix
{
    public static bool CanDesignateCell_Prefix(ref IntVec3 c, ref Designator __instance, ref AcceptanceReport __result)
    {
        var value = Traverse.Create(__instance).Property("Map").GetValue<Map>();
        var mapComponentSeenFog = value.GetMapComponentSeenFog();
        bool result;
        if (mapComponentSeenFog != null && c.InBounds(value) &&
            !mapComponentSeenFog.knownCells[value.cellIndices.CellToIndex(c)])
        {
            __result = false;
            result = false;
        }
        else
        {
            result = true;
        }

        return result;
    }

    public static bool CanDesignateThing_Prefix(ref Thing t, ref AcceptanceReport __result)
    {
        var compHiddenable = t.TryGetCompHiddenable();
        bool result;
        if (compHiddenable is { Hidden: true })
        {
            __result = false;
            result = false;
        }
        else
        {
            result = true;
        }

        return result;
    }
}