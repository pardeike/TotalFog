using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace TotalFog.Compatibility;

internal static class TransportPodVisibility
{
    internal static void Install(Harmony harmony) =>
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(
                typeof(TransportersArrivalAction_LandInSpecificCell),
                nameof(TransportersArrivalAction_LandInSpecificCell.Arrived)
            ),
            prefix: new HarmonyMethod(typeof(TransportPodVisibility), nameof(Arriving))
        );

    // This is the player's chosen landing action. The generic drop helper also
    // serves enemy raids and must never transfer their cargo's hidden location.
    internal static void Arriving(List<ActiveTransporterInfo> __0)
    {
        foreach (var transporter in __0)
        foreach (var thing in transporter.innerContainer)
            thing.TryGetComp<CompFog>()?.HideFromPlayer?.RememberNextArrival(Faction.OfPlayer);
    }
}
