using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class SilentRaidScenarios
{
    [Tool(
        "totalfog/silent_raid_arrivals",
        Description = "Run actual enemy raid and manhunter arrivals with Silent Raids off/on in a paused, reloaded fixture. Checks spawning, arrival letters, no delayed warning, native arrival slowdown, ordinary slowdown outside incidents, and parameter restoration on native failure/exception. Restores options and reloads the unchanged save. No incident scheduling, combat playback or modded incident coverage."
    )]
    public static async Task<object> SilentRaidArrivals(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        bool oldSilent = FogSettings.SilentRaids,
            oldBig = FogSettings.HideThreatBig,
            oldSmall = FogSettings.HideThreatSmall,
            oldDelay = FogSettings.DelayAlertsUntilSeen;
        var rows = new List<object>();
        var baselineKinds = new Dictionary<string, string[]>();
        bool passed = true;
        object lifetime = null;
        try
        {
            foreach (string kind in new[] { "enemy", "manhunter" })
            foreach (bool enabled in new[] { false, true })
            {
                await Reload(ctx, cancellationToken, saveName);
                rows.Add(
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            var map = Find.CurrentMap;
                            if (!Find.TickManager.Paused)
                                throw new InvalidOperationException(
                                    "Arrival checks must stay paused."
                                );
                            FogSettings.SilentRaids = enabled;
                            FogSettings.HideThreatBig =
                                FogSettings.HideThreatSmall =
                                FogSettings.DelayAlertsUntilSeen =
                                    false;
                            var def =
                                kind == "enemy"
                                    ? IncidentDefOf.RaidEnemy
                                    : IncidentDefOf.ManhunterPack;
                            var parms = StorytellerUtility.DefaultParmsNow(def.category, map);
                            parms.points = 400f;
                            parms.forced = true;
                            parms.silent = false;
                            parms.sendLetter = true;
                            if (kind == "enemy")
                            {
                                parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
                                parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
                            }
                            var oldPawns = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
                            int lettersBefore = Find
                                .Archive.ArchivablesListForReading.OfType<Letter>()
                                .Count();
                            int raidsBefore = Find.StoryWatcher.statsRecord.numRaidsEnemy;
                            var pending = map.GetComponent<DeferredNotifications>();
                            int pendingBefore = pending.PendingCount;
                            var slower = Find.TickManager.slower;
                            var deadline = AccessTools.Field(
                                typeof(TimeSlower),
                                "forceNormalSpeedUntil"
                            );
                            deadline.SetValue(slower, 0);
                            bool executed;
                            Rand.PushState(7946571);
                            try
                            {
                                executed = def.Worker.TryExecute(parms);
                            }
                            finally
                            {
                                Rand.PopState();
                            }
                            var spawned = map
                                .mapPawns.AllPawnsSpawned.Where(pawn => !oldPawns.Contains(pawn))
                                .ToArray();
                            var kinds = spawned
                                .Select(pawn => pawn.kindDef.defName)
                                .OrderBy(name => name)
                                .ToArray();
                            bool sameSpawns = !enabled || baselineKinds[kind].SequenceEqual(kinds);
                            if (!enabled)
                                baselineKinds[kind] = kinds;
                            int arrivalDeadline = (int)deadline.GetValue(slower);
                            int letters =
                                Find.Archive.ArchivablesListForReading.OfType<Letter>().Count()
                                - lettersBefore;
                            int queued = pending.PendingCount - pendingBefore;
                            int raids = Find.StoryWatcher.statsRecord.numRaidsEnemy - raidsBefore;
                            int manhunters = spawned.Count(pawn =>
                                pawn.MentalStateDef == MentalStateDefOf.ManhunterPermanent
                            );
                            deadline.SetValue(slower, 0);
                            slower.SignalForceNormalSpeedShort();
                            int ordinaryDeadline = (int)deadline.GetValue(slower);
                            bool valid =
                                executed
                                && spawned.Length > 0
                                && sameSpawns
                                && !parms.silent
                                && parms.sendLetter
                                && queued == 0
                                && (
                                    enabled
                                        ? letters == 0 && arrivalDeadline == 0
                                        : letters > 0
                                            && arrivalDeadline == Find.TickManager.TicksGame + 240
                                )
                                && ordinaryDeadline == Find.TickManager.TicksGame + 240
                                && (
                                    kind == "enemy"
                                        ? raids == 1
                                        : raids == 0 && manhunters == spawned.Length
                                );
                            passed &= valid;
                            return new
                            {
                                kind,
                                enabled,
                                passed = valid,
                                executed,
                                spawned = spawned.Length,
                                kinds,
                                sameSpawns,
                                letters,
                                queued,
                                enemyRaidStats = raids,
                                manhunters,
                                parametersRestored = !parms.silent && parms.sendLetter,
                                tick = Find.TickManager.TicksGame,
                                arrivalDeadline,
                                ordinaryDeadline,
                                incidentWorker = def.Worker.GetType().FullName,
                            };
                        },
                        cancellationToken
                    )
                );
            }
            lifetime = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    FogSettings.SilentRaids = true;
                    var failedParms = new IncidentParms { target = Find.CurrentMap };
                    var failed = new FailedRaid { def = IncidentDefOf.RaidEnemy };
                    bool returnedFalse = !failed.TryExecute(failedParms);
                    var thrownParms = new IncidentParms { target = Find.CurrentMap };
                    var thrown = new ThrowingRaid { def = IncidentDefOf.RaidEnemy };
                    bool exceptionPreserved = false;
                    try
                    {
                        thrown.TryExecute(thrownParms);
                    }
                    catch (InvalidOperationException error)
                    {
                        exceptionPreserved = error.Message == "Total Fog failed incident fixture";
                    }
                    bool valid =
                        returnedFalse
                        && failed.SawSilent
                        && !failedParms.silent
                        && exceptionPreserved
                        && thrown.SawSilent
                        && !thrownParms.silent;
                    passed &= valid;
                    return new
                    {
                        passed = valid,
                        returnedFalse,
                        failedSawSilent = failed.SawSilent,
                        failedParametersRestored = !failedParms.silent,
                        exceptionPreserved,
                        throwingSawSilent = thrown.SawSilent,
                        throwingParametersRestored = !thrownParms.silent,
                    };
                },
                cancellationToken
            );
            return new
            {
                passed,
                rows,
                lifetime,
            };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    FogSettings.SilentRaids = oldSilent;
                    FogSettings.HideThreatBig = oldBig;
                    FogSettings.HideThreatSmall = oldSmall;
                    FogSettings.DelayAlertsUntilSeen = oldDelay;
                },
                CancellationToken.None
            );
            await Reload(ctx, CancellationToken.None, saveName);
        }
    }

    private static async Task Reload(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
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
            throw new InvalidOperationException(
                "Silent arrival fixture reload failed: " + load.Error
            );
    }

    private sealed class FailedRaid : IncidentWorker_RaidEnemy
    {
        public bool SawSilent;

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            SawSilent = parms.silent;
            return false;
        }
    }

    private sealed class ThrowingRaid : IncidentWorker_RaidEnemy
    {
        public bool SawSilent;

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            SawSilent = parms.silent;
            throw new InvalidOperationException("Total Fog failed incident fixture");
        }
    }
}
