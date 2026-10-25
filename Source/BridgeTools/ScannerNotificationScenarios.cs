using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Notifications;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class ScannerNotificationScenarios
{
    [Tool(
        "totalfog/scanner_notifications",
        Description = "Run native ground and long-range scanner discoveries without current sight. Check immediate letters, deposit targets, ordinary hidden-event filtering, ownership and exception cleanup. Restores options/coverage and reloads the unchanged isolated save."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/scanner_notifications");
        proof.saveName = saveName;
        bool oldDelay = FogSettings.DelayAlertsUntilSeen,
            oldPositive = FogSettings.HideEventPositive,
            oldOutside = FogSettings.OnlyOutsideColony;
        var owned = new List<Thing>();
        var rows = new List<object>();
        Map map = null;
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
                    var fog = map.GetVisibility();
                    var manager = map.GetComponent<DeferredNotifications>();
                    var worker = map.mapPawns.FreeColonistsSpawned.First();
                    var origin = map.AllCells.First(c =>
                        c.x > 10
                        && c.z > 10
                        && c.x < map.Size.x - 10
                        && c.z < map.Size.z - 10
                        && new CellRect(c.x, c.z, 7, 5).All(p =>
                            p.Standable(map) && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    var deep = Spawn<CompDeepScanner>(origin);
                    var distant = Spawn<CompLongRangeMineralScanner>(origin + new IntVec3(4, 0, 0));
                    counts = fog.GetFactionShownCells(Faction.OfPlayer);
                    originalCounts = (int[])counts.Clone();
                    var known = fog.GetFactionKnownCells(Faction.OfPlayer);
                    var originalKnown = (bool[])known.Clone();
                    Array.Clear(counts, 0, counts.Length);
                    FogSettings.DelayAlertsUntilSeen = true;
                    FogSettings.OnlyOutsideColony = false;
                    LookTargets deposit = null;
                    foreach (bool discard in new[] { false, true })
                    {
                        FogSettings.HideEventPositive = discard;
                        foreach (var scanner in new CompScanner[] { deep, distant })
                        {
                            var archived = new HashSet<Letter>(
                                Find.Archive.ArchivablesListForReading.OfType<Letter>()
                            );
                            int pending = manager.PendingCount;
                            RunDiscovery(scanner, worker);
                            var added = Find
                                .Archive.ArchivablesListForReading.OfType<Letter>()
                                .Where(l => !archived.Contains(l))
                                .ToArray();
                            string label =
                                scanner.GetType().Name + (discard ? " discard" : " defer");
                            Check(
                                label + " reports immediately",
                                added.Length > 0 && manager.PendingCount == pending
                            );
                            Check(
                                label + " restores scanner scope",
                                !ScannerNotifications.IsPlayerReport
                            );
                            if (scanner == deep)
                            {
                                deposit = added.SingleOrDefault()?.lookTargets;
                                Check(
                                    label + " keeps the hidden deposit as its target",
                                    deposit?.targets?.Any(t =>
                                        t.IsValid
                                        && !t.HasThing
                                        && t.Map == map
                                        && !fog.IsShown(Faction.OfPlayer, t.Cell)
                                    ) == true
                                );
                            }
                            rows.Add(
                                new
                                {
                                    scanner = scanner.GetType().Name,
                                    discard,
                                    letters = added.Select(l => l.label.ToString()).ToArray(),
                                    pendingBefore = pending,
                                    pendingAfter = manager.PendingCount,
                                }
                            );
                        }
                    }
                    if (deposit == null)
                        throw new InvalidOperationException(
                            "No native ground discovery letter was delivered."
                        );
                    foreach (bool discard in new[] { false, true })
                    {
                        FogSettings.HideEventPositive = discard;
                        int pending = manager.PendingCount;
                        var control = LetterMaker.MakeLetter(
                            "Scanner policy hidden control",
                            "Ordinary hidden event at the same deposit.",
                            LetterDefOf.PositiveEvent,
                            deposit
                        );
                        Find.LetterStack.ReceiveLetter(control, playSound: false);
                        Check(
                            "ordinary hidden positive remains "
                                + (discard ? "discarded" : "deferred"),
                            !Find.Archive.ArchivablesListForReading.Contains(control)
                                && manager.PendingCount == pending + (discard ? 0 : 1)
                        );
                    }
                    FogSettings.HideEventPositive = false;
                    deep.parent.SetFaction(Faction.OfAncients);
                    int beforeEnemy = manager.PendingCount;
                    RunDiscovery(deep, worker);
                    Check(
                        "another faction's scanner cannot bypass fog",
                        manager.PendingCount == beforeEnemy + 1
                    );
                    Check("unowned report restores scope", !ScannerNotifications.IsPlayerReport);

                    var unspawned = ThingMaker.MakeThing(deep.parent.def);
                    owned.Add(unspawned);
                    unspawned.SetFaction(Faction.OfPlayer);
                    bool failed = false;
                    try
                    {
                        RunDiscovery(unspawned.TryGetComp<CompDeepScanner>(), worker);
                    }
                    catch (TargetInvocationException error)
                        when (error.InnerException is NullReferenceException)
                    {
                        failed = true;
                    }
                    Check(
                        "native discovery failure preserves exception and clears scope",
                        failed && !ScannerNotifications.IsPlayerReport
                    );
                    Check("reports do not grant current sight", counts.All(c => c == 0));
                    Check(
                        "reports do not reveal surface terrain",
                        known.SequenceEqual(originalKnown)
                    );
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    proof.environment.details = new
                    {
                        gameplayMvid = typeof(MapVisibility)
                            .Assembly
                            .ManifestModule
                            .ModuleVersionId,
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => m.PackageId)
                            .ToArray(),
                        rows,
                    };

                    T Spawn<T>(IntVec3 cell)
                        where T : CompScanner
                    {
                        var def = DefDatabase<ThingDef>.AllDefs.First(d =>
                            d.comps?.Any(c => c.compClass == typeof(T)) == true
                        );
                        var thing = ThingMaker.MakeThing(def);
                        owned.Add(thing);
                        thing.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(thing, cell, map);
                        return thing.TryGetComp<T>();
                    }
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
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy(DestroyMode.Vanish);
                    FogSettings.DelayAlertsUntilSeen = oldDelay;
                    FogSettings.HideEventPositive = oldPositive;
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
                    RimBridgeEvidence.ToolSucceeded("reload unchanged scanner fixture", reset)
                );
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;

        void Check(string name, bool condition) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, condition));
    }

    private static void RunDiscovery(CompScanner scanner, Pawn worker) =>
        AccessTools
            .DeclaredMethod(scanner.GetType(), "DoFind")
            .Invoke(scanner, new object[] { worker });
}
