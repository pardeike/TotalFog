using HarmonyLib;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class DesignatorPlace
{
    public static void CanDesignateCell_Postfix(ref IntVec3 c, ref Designator __instance, ref AcceptanceReport __result)
    {
        var accepted = __result.Accepted;
        if (!accepted)
        {
            return;
        }

        var traverse = Traverse.Create(__instance);
        var cellRect = GenAdj.OccupiedRect(c, traverse.Field("placingRot").GetValue<Rot4>(),
            traverse.Property("PlacingDef").GetValue<BuildableDef>().Size);
        var value = traverse.Property("Map").GetValue<Map>();
        var mapComponentSeenFog = value.GetMapComponentSeenFog();
        if (mapComponentSeenFog == null)
        {
            return;
        }

        foreach (var c2 in cellRect)
        {
            if (mapComponentSeenFog.knownCells[value.cellIndices.CellToIndex(c2)])
            {
                continue;
            }

            __result = "CannotPlaceInUndiscovered".Translate();
            break;
        }
    }
}