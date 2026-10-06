using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class NotificationScenarios
{
    [Tool(
        "totalfog/notification_observability",
        Description = "Verify colony health, global, visible-threat, discarded hidden-threat and deferred hidden-threat letters in the native pipeline. Reloads the fixture afterward and restores settings."
    )]
    public static async Task<object> NotificationObservability(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName = "MortalFleshbeastAttack"
    )
    {
        var load = await ctx.Tools.CallAsync(
            "rimworld/load_game_ready",
            new
            {
                saveName,
                readiness = "visual",
                pauseIfNeeded = true,
                ignoreModCompatibility = true,
            },
            cancellationToken: cancellationToken
        );
        if (!load.Succeeded())
            return new
            {
                success = false,
                stage = "load",
                load.Error,
            };
        try
        {
            return await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map = Find.CurrentMap;
                    var fog = map.GetComponent<MapVisibility>();
                    var manager = map.GetComponent<DeferredNotifications>();
                    var own = map.mapPawns.AllPawnsSpawned.First(p =>
                        p.Faction == Faction.OfPlayer && !p.Dead
                    );
                    var enemy = map.mapPawns.AllPawnsSpawned.First(p =>
                        p.Faction != Faction.OfPlayer
                        && !p.Dead
                        && !fog.IsShown(Faction.OfPlayer, p.Position)
                    );
                    var counts = fog.Coverage.Counts(0);
                    int ownIndex = map.cellIndices.CellToIndex(own.Position),
                        enemyIndex = map.cellIndices.CellToIndex(enemy.Position);
                    int ownCount = counts[ownIndex],
                        enemyCount = counts[enemyIndex],
                        pending = manager.PendingCount;
                    bool hideNegative = FogSettings.HideEventNegative,
                        hideBig = FogSettings.HideThreatBig,
                        delay = FogSettings.DelayAlertsUntilSeen,
                        outside = FogSettings.OnlyOutsideColony;
                    string token = "Total Fog observability " + Guid.NewGuid().ToString("N") + " ";
                    try
                    {
                        FogSettings.HideEventNegative =
                            FogSettings.HideThreatBig =
                            FogSettings.DelayAlertsUntilSeen =
                                true;
                        FogSettings.OnlyOutsideColony = false;
                        counts[ownIndex] = counts[enemyIndex] = 0;
                        void Send(string label, LetterDef def, LookTargets targets) =>
                            Find.LetterStack.ReceiveLetter(
                                LetterMaker.MakeLetter(
                                    token + label,
                                    "Total Fog notification fixture",
                                    def,
                                    targets
                                ),
                                playSound: false
                            );
                        Send("colony-health", LetterDefOf.NegativeEvent, new LookTargets(own));
                        Send("global-condition", LetterDefOf.NegativeEvent, new LookTargets());
                        Send("hidden-discarded", LetterDefOf.ThreatBig, new LookTargets(enemy));
                        counts[enemyIndex] = 1;
                        Send("visible-threat", LetterDefOf.ThreatBig, new LookTargets(enemy));
                        counts[enemyIndex] = 0;
                        FogSettings.HideThreatBig = false;
                        Send("hidden-deferred", LetterDefOf.ThreatBig, new LookTargets(enemy));
                        bool Archived(string label) =>
                            Find
                                .Archive.ArchivablesListForReading.OfType<Letter>()
                                .Any(l => l.Label == token + label);
                        bool health = Archived("colony-health"),
                            global = Archived("global-condition"),
                            visible = Archived("visible-threat"),
                            discarded = Archived("hidden-discarded"),
                            deferred = Archived("hidden-deferred");
                        return new
                        {
                            success = health
                                && global
                                && visible
                                && !discarded
                                && !deferred
                                && manager.PendingCount == pending + 1,
                            colonyHealthReported = health,
                            globalConditionReported = global,
                            visibleThreatReported = visible,
                            hiddenDiscardedReported = discarded,
                            hiddenDeferredReported = deferred,
                            queued = manager.PendingCount - pending,
                        };
                    }
                    finally
                    {
                        counts[ownIndex] = ownCount;
                        counts[enemyIndex] = enemyCount;
                        FogSettings.HideEventNegative = hideNegative;
                        FogSettings.HideThreatBig = hideBig;
                        FogSettings.DelayAlertsUntilSeen = delay;
                        FogSettings.OnlyOutsideColony = outside;
                    }
                },
                cancellationToken
            );
        }
        finally
        {
            var reset = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName,
                    readiness = "visual",
                    pauseIfNeeded = true,
                    ignoreModCompatibility = true,
                },
                cancellationToken: CancellationToken.None
            );
            if (!reset.Succeeded())
                throw new InvalidOperationException(
                    "Notification fixture cleanup reload failed: " + reset.Error
                );
        }
    }

    [Tool(
        "totalfog/notification_scenario",
        Description = "Queue, inspect, or reveal real hidden-target notifications. The target ID survives save/load."
    )]
    public static async Task<object> NotificationScenario(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string operation = "probe",
        string targetID = ""
    )
    {
        var result = await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map =
                    Find.CurrentMap
                    ?? throw new InvalidOperationException("Load a test map first.");
                var fog = map.GetComponent<MapVisibility>();
                var manager = map.GetComponent<DeferredNotifications>();
                var target = string.IsNullOrEmpty(targetID)
                    ? map.mapPawns.AllPawnsSpawned.First(p =>
                        p.Faction != Faction.OfPlayer
                        && !p.Dead
                        && !fog.IsShown(Faction.OfPlayer, p.Position)
                    )
                    : map.mapPawns.AllPawnsSpawned.First(p => p.ThingID == targetID);
                if (operation == "queue")
                {
                    FogSettings.DelayAlertsUntilSeen = true;
                    Messages.Message(
                        new Message(
                            "Total Fog persistence message " + target.ThingID,
                            MessageTypeDefOf.NeutralEvent,
                            new LookTargets(target)
                        ),
                        true
                    );
                    var letter = LetterMaker.MakeLetter(
                        "Total Fog persistence letter",
                        "Hidden target " + target.ThingID,
                        LetterDefOf.NeutralEvent,
                        new LookTargets(target)
                    );
                    Find.LetterStack.ReceiveLetter(
                        letter,
                        "Total Fog persistence fixture",
                        0,
                        false
                    );
                }
                else if (operation == "reveal")
                {
                    var viewer = PawnGenerator.GeneratePawn(
                        DefDatabase<PawnKindDef>.GetNamed("Ghoul"),
                        Faction.OfPlayer
                    );
                    map.fogGrid.Unfog(target.Position);
                    GenSpawn.Spawn(viewer, target.Position, map);
                    viewer.drafter.Drafted = true;
                    viewer.jobs.StartJob(
                        JobMaker.MakeJob(JobDefOf.Wait_Combat),
                        JobCondition.InterruptForced
                    );
                    viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                }
                else if (operation != "probe")
                    throw new ArgumentException("Use queue, probe, or reveal.", nameof(operation));
                var archive = Find.Archive.ArchivablesListForReading;
                return new
                {
                    targetID = target.ThingID,
                    pending = manager.PendingCount,
                    visible = fog.IsShown(Faction.OfPlayer, target.Position),
                    delayEnabled = FogSettings.DelayAlertsUntilSeen,
                    initialized = fog.Initialized,
                    x = target.Position.x,
                    z = target.Position.z,
                    archivedMessages = archive
                        .OfType<Message>()
                        .Count(m => m.text == "Total Fog persistence message " + target.ThingID),
                    archivedLetters = archive
                        .OfType<Letter>()
                        .Count(l =>
                            l.Label == "Total Fog persistence letter"
                            && l.lookTargets.PrimaryTarget.Thing == target
                        ),
                };
            },
            cancellationToken
        );
        if (operation == "reveal")
            await ctx.Game.StepTicksAsync(60, cancellationToken: cancellationToken);
        return result;
    }

    [Tool(
        "totalfog/audio_policy",
        Description = "Check the actual music getter and hidden-source audio setting combinations, restoring the original options."
    )]
    public static object AudioPolicy()
    {
        var music = Find.MusicManagerPlay;
        bool combat = FogSettings.SuppressCombatMusic,
            mute = FogSettings.MuteHiddenSounds,
            check = FogSettings.DoAudioCheck;
        bool danger = music.OverrideDangerMode;
        try
        {
            music.OverrideDangerMode = true;
            FogSettings.SuppressCombatMusic = true;
            bool suppressed = music.DangerMusicMode;
            FogSettings.SuppressCombatMusic = false;
            bool vanilla = music.DangerMusicMode;
            var map = Find.CurrentMap;
            var fog = map.GetComponent<MapVisibility>();
            var hidden = map.mapPawns.AllPawnsSpawned.First(p =>
                p.Faction != Faction.OfPlayer
                && !p.Dead
                && !fog.IsShown(Faction.OfPlayer, p.Position)
            );
            FogSettings.DoAudioCheck = false;
            FogSettings.MuteHiddenSounds = true;
            float mutedWithoutHearing = SoundAudibility.GetAudibilityFactor(
                hidden,
                FogSettings.AudioSourceRange
            );
            FogSettings.DoAudioCheck = true;
            float mutedWithHearing = SoundAudibility.GetAudibilityFactor(
                hidden,
                FogSettings.AudioSourceRange
            );
            return new
            {
                suppressed,
                vanilla,
                mutedWithoutHearing,
                mutedWithHearing,
                expected = new
                {
                    suppressed = false,
                    vanilla = true,
                    mutedWithoutHearing = 0,
                    mutedWithHearing = 0,
                },
            };
        }
        finally
        {
            music.OverrideDangerMode = danger;
            FogSettings.SuppressCombatMusic = combat;
            FogSettings.MuteHiddenSounds = mute;
            FogSettings.DoAudioCheck = check;
        }
    }
}
