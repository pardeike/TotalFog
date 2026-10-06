using RimWorldRealFoW.Utils;

namespace RimWorldRealFoW.Detours;

internal static class Pawn
{
    public static bool DrawGUIOverlay_Prefix(Verse.Pawn __instance)
    {
        return !(__instance.Spawned && !__instance.FowIsVisible());
    }
}