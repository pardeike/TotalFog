using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using TotalFog.Presentation;
using Verse;

namespace TotalFog.Notifications;

internal static partial class ThreatAlerts
{
    // Only registered threat reports use this adapter. Keep health, needs,
    // resource and world-only alerts native, including reports without culprits.
    internal static void ReportPostfix(ref AlertReport __result)
    {
        if (!__result.active || !FogSettings.DelayAlertsUntilSeen)
            return;
        var visible = new List<GlobalTargetInfo>();
        bool hasTarget = false;
        foreach (var target in __result.AllCulprits)
        {
            if (!target.IsValid)
                continue;
            hasTarget = true;
            if (
                target.Thing is Fire && ThingVisibility.IsActionable(target.Thing, Faction.OfPlayer)
                || NotificationVisibility.HasVisibleTarget(new LookTargets(target))
            )
                visible.Add(target);
        }
        if (hasTarget)
            __result = visible.Count == 0 ? AlertReport.Inactive : AlertReport.CulpritsAre(visible);
    }
}
