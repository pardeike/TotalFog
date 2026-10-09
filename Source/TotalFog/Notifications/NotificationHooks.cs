using RimWorld;
using TotalFog.Compatibility;
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
        // MP can produce non-historical command feedback on only the issuing
        // client. It must not enter a shared, saved notification queue.
        if (hidden && !historical && MultiplayerIntegration.Active)
            return false;
        bool suppress = NotificationVisibility.Suppressed(msg.def);
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
