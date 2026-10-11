using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class PodNotificationScenarios
{
    [Tool(
        "totalfog/refugee_pod_notifications",
        Description = "Exercise the native pre-spawn refugee letter, actual drop pod transit/landing, first sight and discard mode. Also preserve an ordinary mapless notification. Uses the isolated single-player fixture and reloads it afterward."
    )]
    public static async Task<object> RefugeePods(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName = "MortalFleshbeastAttack"
    )
    {
        bool neutral = FogSettings.HideEventNeutral,
            delay = FogSettings.DelayAlertsUntilSeen,
            outside = FogSettings.OnlyOutsideColony;
        var samples = new List<object>();
        Pawn passenger = null;
        Map map = null;
        object Snapshot(string phase) =>
            new
            {
                phase,
                tick = Find.TickManager.TicksGame,
                targetID = passenger.ThingID,
                mapHeld = passenger.MapHeld?.uniqueID,
                passenger.Spawned,
                passenger.Downed,
                passenger.Destroyed,
                visible = Visibility.IsVisible(passenger),
                pending = map.GetComponent<DeferredNotifications>().PendingCount,
                archived = Find
                    .Archive.ArchivablesListForReading.OfType<Letter>()
                    .Count(l => l.lookTargets?.targets.Any(t => t.Thing == passenger) == true),
            };
        try
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
                throw new InvalidOperationException("Pod fixture load failed: " + load.Error);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    FogSettings.HideEventNeutral = false;
                    FogSettings.DelayAlertsUntilSeen = true;
                    FogSettings.OnlyOutsideColony = false;
                    var cell = map.AllCells.First(c =>
                        c.x > 4
                        && c.z > 4
                        && c.Standable(map)
                        && !map.GetVisibility().IsShown(Faction.OfPlayer, c)
                        && c.GetThingList(map).All(t => t is Plant or Filth)
                    );
                    passenger = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncientsHostile
                    );
                    HealthUtility.DamageUntilDowned(passenger);
                    new QuestNode_Root_RefugeePodCrash().SendLetter_NewTemp(null, passenger, map);
                    samples.Add(Snapshot("before-map-assignment"));
                    DropPodUtility.DropThingsNear(
                        cell,
                        map,
                        new List<Thing> { passenger },
                        110,
                        canInstaDropDuringInit: false,
                        leaveSlag: false
                    );
                    samples.Add(Snapshot("in-native-pod"));
                },
                cancellationToken
            );
            await ctx.Game.StepTicksAsync(650, cancellationToken: cancellationToken);
            bool landedHidden = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    samples.Add(Snapshot("landed-hidden"));
                    if (!passenger.Spawned || Visibility.IsVisible(passenger))
                        return false;
                    var viewer = PawnGenerator.GeneratePawn(
                        DefDatabase<PawnKindDef>.GetNamed("Ghoul"),
                        Faction.OfPlayer
                    );
                    map.fogGrid.Unfog(passenger.Position);
                    GenSpawn.Spawn(viewer, passenger.Position, map);
                    viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    return true;
                },
                cancellationToken
            );
            if (!landedHidden)
                return new
                {
                    success = false,
                    reason = "Need a landed hidden passenger.",
                    samples,
                };
            await ctx.Game.StepTicksAsync(60, cancellationToken: cancellationToken);
            return await ctx.MainThread.InvokeAsync(
                () =>
                {
                    samples.Add(Snapshot("first-sight"));
                    FogSettings.HideEventNeutral = true;
                    passenger = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncientsHostile
                    );
                    new QuestNode_Root_RefugeePodCrash().SendLetter_NewTemp(null, passenger, map);
                    samples.Add(Snapshot("discard-before-map-assignment"));
                    var label = "Total Fog mapless control " + Guid.NewGuid().ToString("N");
                    Find.LetterStack.ReceiveLetter(
                        label,
                        "World-context control",
                        LetterDefOf.NeutralEvent,
                        passenger
                    );
                    return new
                    {
                        samples,
                        ordinaryMaplessLetterPreserved = Find
                            .Archive.ArchivablesListForReading.OfType<Letter>()
                            .Any(l => l.Label == label),
                    };
                },
                cancellationToken
            );
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    FogSettings.HideEventNeutral = neutral;
                    FogSettings.DelayAlertsUntilSeen = delay;
                    FogSettings.OnlyOutsideColony = outside;
                },
                CancellationToken.None
            );
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
                throw new InvalidOperationException("Pod fixture cleanup failed: " + reset.Error);
        }
    }
}
