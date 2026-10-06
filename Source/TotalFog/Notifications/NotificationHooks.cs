using RimWorld;
using TotalFog.Core;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Notifications;

internal static class NotificationHooks
{
    public static bool MessagePrefix(Message msg, bool historical)
    {
        if (msg == null || DeferredNotifications.IsReplayingLetter)
            return true;
        bool hidden = !NotificationVisibility.HasVisibleTarget(msg.lookTargets);
        bool suppress =
            hidden && msg.def == MessageTypeDefOf.ThreatBig && FogSettings.HideThreatBig;
        var decision = NotificationPolicy.Decide(
            hidden,
            suppress,
            FogSettings.DelayAlertsUntilSeen,
            false
        );
        if (decision == NotificationDecision.Show)
            return true;
        if (decision == NotificationDecision.Defer)
        {
            var manager = NotificationVisibility
                .MapFor(msg.lookTargets)
                ?.GetDeferredNotifications();
            if (manager == null)
                return true;
            manager.Add(msg, historical);
        }
        return false;
    }

    public static bool LetterPrefix(Letter let, string debugInfo, int delayTicks, bool playSound)
    {
        if (let == null || DeferredNotifications.IsReplayingLetter)
            return true;
        var decision = NotificationPolicy.Decide(
            !NotificationVisibility.HasVisibleTarget(let.lookTargets),
            NotificationVisibility.Suppressed(let.def),
            FogSettings.DelayAlertsUntilSeen,
            false
        );
        if (decision == NotificationDecision.Show)
            return true;
        if (decision == NotificationDecision.Defer)
        {
            var manager = NotificationVisibility
                .MapFor(let.lookTargets)
                ?.GetDeferredNotifications();
            if (manager == null)
                return true;
            manager.Add(let, debugInfo, delayTicks, playSound);
        }
        return false;
    }
}
