using RimWorld;

namespace RimWorldRealFoW.Detours;

public static class Designation
{
    public static void Notify_Added_Postfix(ref Verse.Designation __instance)
    {
        if (__instance.def != DesignationDefOf.Mine || __instance.target.HasThing)
        {
            return;
        }

        var mapComponentSeenFog = __instance.designationManager.map.GetMapComponentSeenFog();
        mapComponentSeenFog?.RegisterMineDesignation(__instance);
    }

    public static void Notify_Removing_Postfix(ref Verse.Designation __instance)
    {
        if (__instance.def != DesignationDefOf.Mine || __instance.target.HasThing)
        {
            return;
        }

        var mapComponentSeenFog = __instance.designationManager.map.GetMapComponentSeenFog();
        mapComponentSeenFog?.DeregisterMineDesignation(__instance);
    }
}