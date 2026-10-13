using HarmonyLib;
using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static class MapSearchVisibility
{
    internal static bool AllowsResult(Thing thing)
    {
        if (thing == null)
            return false;
        if (DebugSettings.searchIgnoresRestrictions)
            return true;
        if (thing.SpawnedParentOrMe is Pawn holder && holder != thing)
            return ThingVisibility.CanInspect(holder)
                && (holder.Faction == Faction.OfPlayer || ThingVisibility.IsVisible(thing));
        return ThingVisibility.CanInspect(thing) || ThingVisibility.IsVisible(thing);
    }

    [HarmonyPriority(Priority.Last)]
    public static void VisibilityPostfix(Thing thing, ref bool __result)
    {
        if (__result)
            __result = AllowsResult(thing);
    }

    // Search navigation follows the same memory policy as map drawing.
    // Native selection still requires current sight before opening live UI.
    [HarmonyPriority(Priority.Last)]
    public static bool LocationPrefix(Thing element) => AllowsResult(element);
}
