using HarmonyLib;
using RimWorld;

namespace TotalFog.Notifications;

internal static partial class FactionRelationNotifications
{
    internal static void Install(Harmony harmony) =>
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(Faction), nameof(Faction.Notify_RelationKindChanged)),
            prefix: new HarmonyMethod(typeof(FactionRelationNotifications), nameof(Prefix)),
            finalizer: new HarmonyMethod(typeof(FactionRelationNotifications), nameof(Finalizer))
        );
}
