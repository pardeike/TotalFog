using RimWorld;
using TotalFog.Presentation;
using Verse;

namespace TotalFog.Detours;

internal static class DoorOrders
{
    // The area order is player intent. It does not open the live door inspector.
    // Only a previously observed player door can extend vanilla's item-only tool.
    public static bool CanDesignateThing_Prefix(
        Designator __instance,
        Thing t,
        ref AcceptanceReport __result
    )
    {
        if (
            t is Building_Door
            && t.Spawned
            && t.Faction == Faction.OfPlayer
            && t.TryGetComp<CompFog>()?.HideFromPlayer?.WasSeenBy(Faction.OfPlayer) == true
            && !t.PositionHeld.Fogged(t.MapHeld)
        )
        {
            var forbiddable = t.TryGetComp<CompForbiddable>();
            __result =
                forbiddable != null && forbiddable.Forbidden == (__instance is Designator_Unforbid);
            return false;
        }
        if (ThingVisibility.IsVisible(t))
            return true;
        __result = false;
        return false;
    }
}
