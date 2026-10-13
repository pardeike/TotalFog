using HarmonyLib;
using TotalFog.Utils;

namespace TotalFog.Detours;

internal static class Pawn
{
    [HarmonyPriority(Priority.Last)]
    public static bool DrawGUIOverlay_Prefix(Verse.Pawn __instance)
    {
        return !(__instance.Spawned && !__instance.IsFogVisible());
    }
}
