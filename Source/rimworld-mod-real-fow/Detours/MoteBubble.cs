using RimWorldRealFoW.Utils;

namespace RimWorldRealFoW.Detours;

public static class MoteBubble
{
    public static bool DrawAt_Prefix(RimWorld.MoteBubble __instance)
    {
        return !(__instance.link1.Linked && __instance.link1.Target != null && __instance.link1.Target.Thing != null) ||
               __instance.link1.Target.Thing.FowIsVisible();
    }
}