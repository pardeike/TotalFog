// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System;
using TotalFog.Core;
using TotalFog.Notifications;
using Verse;

namespace TotalFog;

public class DeferredNotifications : MapComponent
{
    private readonly PendingQueue<DeferredNotification> queue = new();
    private readonly Func<DeferredNotification, bool> valid,
        ready;
    private readonly Action<DeferredNotification> replay;
    private int nextCheck;
    public static bool IsReplayingLetter { get; private set; }
    public int PendingCount => queue.Items.Count;

    public DeferredNotifications(Map map)
        : base(map)
    {
        valid = n => NotificationVisibility.HasLiveTarget(n.Targets);
        ready = n =>
            map.GetVisibility().Initialized
            && Find.TickManager.TicksGame >= n.earliestTick
            && (
                !FogSettings.DelayAlertsUntilSeen
                || NotificationVisibility.HasVisibleTarget(n.Targets)
            );
        replay = Replay;
    }

    public void Add(Message message, bool historical) =>
        queue.Add(new DeferredNotification { message = message, historical = historical });

    public void Add(Letter letter, string debugInfo, int delayTicks, bool playSound) =>
        queue.Add(
            new DeferredNotification
            {
                letter = letter,
                debugInfo = debugInfo,
                playSound = playSound,
                earliestTick = checked(Find.TickManager.TicksGame + Math.Max(0, delayTicks)),
            }
        );

    public override void MapComponentTick()
    {
        if (queue.Items.Count == 0 || Find.TickManager.TicksGame < nextCheck)
            return;
        nextCheck = Find.TickManager.TicksGame + 30;
        queue.Drain(valid, ready, replay);
    }

    private static void Replay(DeferredNotification notification)
    {
        IsReplayingLetter = true;
        try
        {
            if (notification.letter != null)
                Find.LetterStack.ReceiveLetter(
                    notification.letter,
                    notification.debugInfo,
                    0,
                    notification.playSound
                );
            else if (notification.message != null)
            {
                notification.message.ResetTimer();
                Verse.Messages.Message(notification.message, notification.historical);
            }
        }
        finally
        {
            IsReplayingLetter = false;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref queue.Items, "totalFogPendingNotifications", LookMode.Deep);
        queue.Items ??= new();
    }
}

public class DeferredNotification : IExposable
{
    public Message message;
    public Letter letter;
    public bool historical = true,
        playSound = true;
    public string debugInfo;
    public int earliestTick;
    public LookTargets Targets => letter?.lookTargets ?? message?.lookTargets;

    public void ExposeData()
    {
        Scribe_Deep.Look(ref message, "message");
        Scribe_Deep.Look(ref letter, "letter");
        Scribe_Values.Look(ref historical, "historical", true);
        Scribe_Values.Look(ref playSound, "playSound", true);
        Scribe_Values.Look(ref debugInfo, "debugInfo");
        Scribe_Values.Look(ref earliestTick, "earliestTick");
    }
}
