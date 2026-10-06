using HarmonyLib;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class RoofGrid
{
    public static void GetCellBool_Postfix(int index, ref Verse.TerrainGrid __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        var value = Traverse.Create(__instance).Field("map").GetValue<Map>();
        var mapComponentSeenFog = value.GetMapComponentSeenFog();
        if (mapComponentSeenFog != null)
        {
            __result = mapComponentSeenFog.knownCells[index];
        }
    }
}