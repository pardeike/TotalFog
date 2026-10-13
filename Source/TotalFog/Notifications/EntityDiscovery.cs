using HarmonyLib;
using RimWorld;
using TotalFog.Presentation;
using Verse;

namespace TotalFog.Notifications;

internal static class EntityDiscovery
{
    [HarmonyPriority(Priority.Last)]
    public static bool DiscoverPrefix(EntityCodexEntryDef entry, Thing discoveredThing) =>
        entry.startDiscovered || CanDiscover(discoveredThing);

    private static bool CanDiscover(Thing thing)
    {
        var map = thing?.MapHeld;
        if (map == null)
            return true;
        var fog = map.GetVisibility();
        if (!fog.Initialized)
            return false;
        // The native codex is shared game state. Use every player observer,
        // rather than the local client's selected faction.
        if (VisibleTo(thing, fog.PrimaryPlayerFaction, fog))
            return true;
        foreach (var faction in fog.OtherPlayerFactions)
            if (VisibleTo(thing, faction, fog))
                return true;
        return false;
    }

    private static bool VisibleTo(Thing thing, Faction faction, MapVisibility fog) =>
        faction != null
        && ThingVisibility.IsVisible(
            thing,
            allowMemory: false,
            registeredVisibility: fog,
            observerFaction: faction
        );

    internal static void Observed(Thing thing)
    {
        if (
            ModsConfig.AnomalyActive
            && (
                thing.def.entityCodexEntry != null
                || thing is Pawn { IsMutant: true } or UnnaturalCorpse
            )
            && AnomalyUtility.ShouldNotifyCodex(thing, EntityDiscoveryType.Unfog, out var entries)
        )
            Find.EntityCodex.SetDiscovered(entries, thing.def, thing);
    }
}
