using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.Planet;
using TotalFog.Notifications;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class FactionRelationScenarios
{
    [Tool(
        "totalfog/faction_notifications",
        Description = "Run native Ally, Neutral and Hostile relationship reports with an unseen pawn target. Check immediate letters under defer/discard, retained targets, ordinary hidden events and unchanged discovery. Restore options/coverage and reload the unchanged isolated paused save."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/faction_notifications");
        proof.saveName = saveName;
        bool oldDelay = FogSettings.DelayAlertsUntilSeen,
            oldPositive = FogSettings.HideEventPositive,
            oldNegative = FogSettings.HideEventNegative,
            oldNeutral = FogSettings.HideEventNeutral,
            oldOutside = FogSettings.OnlyOutsideColony;
        Map map = null;
        Pawn target = null;
        int[] counts = null,
            originalCounts = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    if (
                        map?.GetVisibility().Initialized != true
                        || !Find.TickManager.Paused
                        || Compatibility.MultiplayerIntegration.Active
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized single-player fixture."
                        );
                    var faction = Find.FactionManager.AllFactionsListForReading.First(f =>
                        !f.IsPlayer && !f.def.permanentEnemy && !f.def.hidden
                    );
                    var cell = map.AllCells.First(c =>
                        c.Standable(map) && !map.GetVisibility().IsShown(Faction.OfPlayer, c)
                    );
                    target = PawnGenerator.GeneratePawn(PawnKindDefOf.Pirate, faction);
                    GenSpawn.Spawn(target, cell, map);
                    var fog = map.GetVisibility();
                    counts = fog.GetFactionShownCells(Faction.OfPlayer);
                    originalCounts = (int[])counts.Clone();
                    var known = (bool[])fog.GetFactionKnownCells(Faction.OfPlayer).Clone();
                    Array.Clear(counts, 0, counts.Length);
                    var manager = map.GetComponent<DeferredNotifications>();
                    var rows = new List<object>();
                    FogSettings.DelayAlertsUntilSeen = true;
                    FogSettings.OnlyOutsideColony = false;
                    foreach (bool discard in new[] { false, true })
                    {
                        FogSettings.HideEventPositive =
                            FogSettings.HideEventNegative =
                            FogSettings.HideEventNeutral =
                                discard;
                        foreach (
                            var transition in new[]
                            {
                                (FactionRelationKind.Neutral, FactionRelationKind.Ally),
                                (FactionRelationKind.Ally, FactionRelationKind.Neutral),
                                (FactionRelationKind.Neutral, FactionRelationKind.Hostile),
                                (FactionRelationKind.Hostile, FactionRelationKind.Neutral),
                            }
                        )
                        {
                            faction.RelationWith(Faction.OfPlayer).kind = transition.Item2;
                            Faction.OfPlayer.RelationWith(faction).kind = transition.Item2;
                            var before = new HashSet<Letter>(
                                Find.Archive.ArchivablesListForReading.OfType<Letter>()
                            );
                            int pending = manager.PendingCount;
                            faction.Notify_RelationKindChanged(
                                Faction.OfPlayer,
                                transition.Item1,
                                true,
                                null,
                                new GlobalTargetInfo(target),
                                out bool sent
                            );
                            var added = Find
                                .Archive.ArchivablesListForReading.OfType<Letter>()
                                .Where(l => !before.Contains(l))
                                .ToArray();
                            string label =
                                transition.Item1
                                + " to "
                                + transition.Item2
                                + (discard ? " discard" : " defer");
                            Check(
                                label + " reports immediately",
                                sent && added.Length == 1 && manager.PendingCount == pending
                            );
                            Check(
                                label + " keeps the hidden pawn target",
                                added
                                    .SingleOrDefault()
                                    ?.lookTargets?.targets?.Any(t =>
                                        t.HasThing && t.Thing == target
                                    ) == true
                                    && !fog.IsShown(Faction.OfPlayer, target.Position)
                            );
                            Check(
                                label + " restores the report scope",
                                !FactionRelationNotifications.IsPlayerReport
                            );
                            rows.Add(
                                new
                                {
                                    transition = label,
                                    letters = added.Select(l => l.label.ToString()).ToArray(),
                                }
                            );
                        }
                        foreach (
                            var def in new[]
                            {
                                LetterDefOf.PositiveEvent,
                                LetterDefOf.NegativeEvent,
                                LetterDefOf.NeutralEvent,
                            }
                        )
                        {
                            int pending = manager.PendingCount;
                            var control = LetterMaker.MakeLetter(
                                "Diplomacy hidden control",
                                "Ordinary hidden event.",
                                def,
                                target
                            );
                            Find.LetterStack.ReceiveLetter(control, playSound: false);
                            Check(
                                def.defName + " remains " + (discard ? "discarded" : "deferred"),
                                !Find.Archive.ArchivablesListForReading.Contains(control)
                                    && manager.PendingCount == pending + (discard ? 0 : 1)
                            );
                        }
                    }
                    Check("relationship reports grant no current sight", counts.All(c => c == 0));
                    Check(
                        "relationship reports preserve terrain discovery",
                        known.SequenceEqual(fog.GetFactionKnownCells(Faction.OfPlayer))
                    );
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    proof.environment.details = new
                    {
                        gameplayMvid = typeof(MapVisibility)
                            .Assembly
                            .ManifestModule
                            .ModuleVersionId,
                        rows,
                    };
                },
                cancellationToken
            );
        }
        catch (Exception error)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (originalCounts != null)
                        Array.Copy(originalCounts, counts, counts.Length);
                    if (target?.Spawned == true)
                        target.Destroy(DestroyMode.Vanish);
                    FogSettings.DelayAlertsUntilSeen = oldDelay;
                    FogSettings.HideEventPositive = oldPositive;
                    FogSettings.HideEventNegative = oldNegative;
                    FogSettings.HideEventNeutral = oldNeutral;
                    FogSettings.OnlyOutsideColony = oldOutside;
                },
                CancellationToken.None
            );
            if (map != null)
            {
                var reset = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                proof.assertions.Add(
                    RimBridgeEvidence.ToolSucceeded("reload unchanged diplomacy fixture", reset)
                );
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;
        void Check(string name, bool condition) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, condition));
    }
}
