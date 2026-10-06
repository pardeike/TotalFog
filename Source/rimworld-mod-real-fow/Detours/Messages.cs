using HarmonyLib;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class Messages
{
    public static bool Message_Prefix(string text, ref LookTargets lookTargets)
    {
        RealFoWModStarter.LogMessage($"Message_Prefix called: '{text}'");

        var value = Traverse.Create(typeof(Verse.Messages)).Method("AcceptsMessage", text, lookTargets)
            .GetValue<bool>();

        RealFoWModStarter.LogMessage($"AcceptsMessage: {value}");

        if (!value)
        {
            RealFoWModStarter.LogMessage("AcceptsMessage was false, allowing message through");
            return true;
        }

        var hasThing = lookTargets.PrimaryTarget.HasThing;
        RealFoWModStarter.LogMessage($"Has Thing: {hasThing}");

        if (!hasThing)
        {
            RealFoWModStarter.LogMessage("No thing, allowing message through");
            return true;
        }

        var thing = lookTargets.PrimaryTarget.Thing;
        RealFoWModStarter.LogMessage($"Thing: {thing?.Label ?? "null"}, Faction: {thing?.Faction?.Name ?? "none"}");

        if (thing?.Faction is { IsPlayer: true })
        {
            RealFoWModStarter.LogMessage("Thing is player faction, allowing message");
            return true;
        }

        var isSpawned = thing is { Spawned: true };
        var hideThreatBig = RfowSettings.HideThreatBig;
        var isVisible = thing.FowIsVisible();

        RealFoWModStarter.LogMessage($"Spawned: {isSpawned}, HideThreatBig: {hideThreatBig}, IsVisible: {isVisible}");

        if (thing is { Spawned: true } && RfowSettings.HideThreatBig && !thing.FowIsVisible())
        {
            RealFoWModStarter.LogMessage(
                $"Message would be hidden. DelayAlerts: {RfowSettings.DelayAlertsUntilSeen}, Map: {thing.Map != null}");

            // If DelayAlertsUntilSeen is enabled, register a pending alert instead of blocking it
            if (RfowSettings.DelayAlertsUntilSeen && thing.Map != null)
            {
                var pendingAlertManager = thing.Map.GetPendingAlertManager();
                RealFoWModStarter.LogMessage(
                    $"PendingAlertManager: {(pendingAlertManager != null ? "found" : "NULL")}");

                pendingAlertManager?.RegisterPendingAlert(text, lookTargets, thing);
            }

            RealFoWModStarter.LogMessage("Blocking message (returning false)");
            return false;
        }

        RealFoWModStarter.LogMessage("Message not hidden by FoW, allowing through normally");
        if (thing != null)
        {
            lookTargets = new LookTargets(thing.Position, thing.Map);
        }

        RealFoWModStarter.LogMessage("Returning true to allow message");

        return true;
    }
}