using System.Collections.Generic;
using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

/// <summary>
///     Manages delayed alerts that are triggered when entities become visible to the player.
///     This allows the fog of war system to defer threat alerts until the player actually sees the threat.
/// </summary>
public class PendingAlertManager : MapComponent
{
    private readonly List<PendingAlert> pendingAlerts = [];
    private readonly List<PendingLetter> pendingLetters = [];
    private readonly HashSet<Thing> trackedThings = [];

    public PendingAlertManager(Map map) : base(map)
    {
    }

    /// <summary>
    ///     Thread-local flag to indicate we're currently replaying a deferred letter.
    ///     Used to allow forced slowdown during replay while blocking it during deferral.
    /// </summary>
    public static bool IsReplayingLetter { get; set; }

    /// <summary>
    ///     Registers a pending alert for a thing. The alert will be shown when the thing becomes visible.
    /// </summary>
    public void RegisterPendingAlert(string text, LookTargets lookTargets, Thing triggeredByThing)
    {
        if (!RfowSettings.DelayAlertsUntilSeen || triggeredByThing == null)
        {
            return;
        }

        // Check if we already have a pending alert for this thing
        if (trackedThings.Contains(triggeredByThing))
        {
            return;
        }

        pendingAlerts.Add(new PendingAlert
        {
            text = text,
            lookTargets = lookTargets,
            tickCreated = Find.TickManager.TicksGame,
            triggeredByThing = triggeredByThing
        });

        trackedThings.Add(triggeredByThing);

        RealFoWModStarter.LogMessage($"Registered pending alert for {triggeredByThing.Label}: '{text}'");
    }

    /// <summary>
    ///     Registers a pending letter for a thing. The letter will be shown when the thing becomes visible.
    /// </summary>
    public void RegisterPendingLetter(Letter letter, Thing triggeredByThing, TimeSpeed storedGameSpeed)
    {
        if (!RfowSettings.DelayAlertsUntilSeen || triggeredByThing == null || letter == null)
        {
            return;
        }

        // Check if we already have a pending letter for this thing
        if (trackedThings.Contains(triggeredByThing))
        {
            return;
        }

        pendingLetters.Add(new PendingLetter
        {
            letter = letter,
            triggeredByThing = triggeredByThing,
            tickCreated = Find.TickManager.TicksGame,
            storedGameSpeed = storedGameSpeed
        });

        trackedThings.Add(triggeredByThing);

        RealFoWModStarter.LogMessage(
            $"Registered pending letter for {triggeredByThing.Label}, stored speed: {storedGameSpeed}");
    }

    /// <summary>
    ///     Checks if a thing is being tracked by a pending alert.
    /// </summary>
    public bool IsThingTracked(Thing thing)
    {
        return trackedThings.Contains(thing);
    }

    /// <summary>
    ///     Called when a tracked thing becomes visible. Fires the pending alert and cleans up.
    /// </summary>
    public void TriggerPendingAlertForThing(Thing thing)
    {
        for (var i = pendingAlerts.Count - 1; i >= 0; i--)
        {
            var alert = pendingAlerts[i];
            if (alert.triggeredByThing != thing)
            {
                continue;
            }

            // Fire the actual message/alert now
            Messages.Message(alert.text, alert.lookTargets, MessageTypeDefOf.ThreatBig);

            // Clean up
            pendingAlerts.RemoveAt(i);
            trackedThings.Remove(thing);
            break;
        }
    }

    public override void MapComponentTick()
    {
        if (pendingAlerts.Count == 0 && pendingLetters.Count == 0)
        {
            return;
        }

        // Check pending alerts
        for (var i = pendingAlerts.Count - 1; i >= 0; i--)
        {
            var alert = pendingAlerts[i];

            // Clean up if the thing is despawned or dead
            if (alert.triggeredByThing is not { Spawned: true } ||
                alert.triggeredByThing is Pawn { Dead: true })
            {
                RealFoWModStarter.LogMessage(
                    $"Cleaned up pending alert for {alert.triggeredByThing?.Label ?? "unknown"} (despawned/dead)");
                pendingAlerts.RemoveAt(i);
                trackedThings.Remove(alert.triggeredByThing);
                continue;
            }

            // Check if the thing has become visible
            if (!alert.triggeredByThing.FowIsVisible())
            {
                continue;
            }

            RealFoWModStarter.LogMessage(
                $"Firing pending alert for {alert.triggeredByThing.Label}: '{alert.text}'");

            // Fire the alert now
            Messages.Message(alert.text, alert.lookTargets, MessageTypeDefOf.ThreatBig);

            // Clean up
            pendingAlerts.RemoveAt(i);
            trackedThings.Remove(alert.triggeredByThing);
        }

        // Check pending letters
        for (var i = pendingLetters.Count - 1; i >= 0; i--)
        {
            var pendingLetter = pendingLetters[i];

            // Clean up if the thing is despawned or dead
            if (pendingLetter.triggeredByThing is not { Spawned: true } ||
                pendingLetter.triggeredByThing is Pawn { Dead: true })
            {
                RealFoWModStarter.LogMessage(
                    $"Cleaned up pending letter for {pendingLetter.triggeredByThing?.Label ?? "unknown"} (despawned/dead)");
                pendingLetters.RemoveAt(i);
                trackedThings.Remove(pendingLetter.triggeredByThing);
                continue;
            }

            // Check if the thing has become visible
            if (!pendingLetter.triggeredByThing.FowIsVisible())
            {
                continue;
            }

            RealFoWModStarter.LogMessage($"Firing pending letter for {pendingLetter.triggeredByThing.Label}");
            RealFoWModStarter.LogMessage(
                $"Before LetterStack.ReceiveLetter: speed = {Find.TickManager.CurTimeSpeed}");

            // Set flag to allow slowdown during replay
            IsReplayingLetter = true;
            try
            {
                // Manually trigger the slowdown that was deferred
                if (!pendingLetter.letter.def.forcedSlowdown && pendingLetter.letter.def == LetterDefOf.ThreatBig)
                {
                    // Only call if the letter itself wouldn't trigger it
                    // But for ThreatBig, we want to ensure it happens
                    RealFoWModStarter.LogMessage("Triggering deferred slowdown for ThreatBig letter");
                    Find.TickManager.slower.SignalForceNormalSpeedShort();
                }

                // Re-queue the letter to the LetterStack
                Find.LetterStack.ReceiveLetter(pendingLetter.letter);

                RealFoWModStarter.LogMessage(
                    $"After LetterStack.ReceiveLetter: speed = {Find.TickManager.CurTimeSpeed}");
                RealFoWModStarter.LogMessage(
                    $"Letter shown, game was at speed {Find.TickManager.CurTimeSpeed}, original was {pendingLetter.storedGameSpeed}");
            }
            finally
            {
                IsReplayingLetter = false;
            }

            // Clean up
            pendingLetters.RemoveAt(i);
            trackedThings.Remove(pendingLetter.triggeredByThing);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        // Note: We don't save pending alerts as they should be re-evaluated after load
        // This ensures alerts trigger properly after save/load cycles
        pendingAlerts.Clear();
        pendingLetters.Clear();
        trackedThings.Clear();
    }

    private sealed class PendingAlert
    {
        public LookTargets lookTargets;
        public string text;
        public int tickCreated;
        public Thing triggeredByThing;
    }

    private sealed class PendingLetter
    {
        public Letter letter;
        public TimeSpeed storedGameSpeed;
        public int tickCreated;
        public Thing triggeredByThing;
    }
}