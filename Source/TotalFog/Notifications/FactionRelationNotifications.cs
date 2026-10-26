using RimWorld;
using RimWorld.Planet;
using Verse;

namespace TotalFog.Notifications;

internal static partial class FactionRelationNotifications
{
    internal struct ReportState
    {
        internal bool Active;
        internal Faction Faction;
        internal GlobalTargetInfo Target;
    }

    private static ReportState report;
    internal static bool IsPlayerReport => report.Active;

    public static void Prefix(
        Faction __instance,
        Faction other,
        bool canSendLetter,
        GlobalTargetInfo lookTarget,
        out ReportState? __state
    )
    {
        __state = report;
        if (canSendLetter && other != null && other == Faction.OfPlayer)
            report = new ReportState
            {
                Active = true,
                Faction = __instance,
                Target = lookTarget,
            };
    }

    public static void Finalizer(ReportState? __state)
    {
        if (__state.HasValue)
            report = __state.Value;
    }

    internal static bool IsReport(LookTargets targets, Faction relatedFaction)
    {
        if (!report.Active || relatedFaction != report.Faction || targets?.targets?.Count != 1)
            return false;
        var target = targets.targets[0];
        // Relation changes can send quest signals before their own letter. Do not
        // exempt notifications about another target from those callbacks.
        return report.Target.Thing != null
            ? target.HasThing && target.Thing == report.Target.Thing
            : !target.HasThing
                && target.Map == report.Target.Map
                && target.Cell == report.Target.Cell;
    }
}
