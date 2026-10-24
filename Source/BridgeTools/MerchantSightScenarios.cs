using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class MerchantSightScenarios
{
    [Tool(
        "totalfog/merchant_sight",
        Description = "Check native visitor target acquisition with all enemy-fog/sight-sharing combinations, shorter player sharing, hostile controls and source removal. Uses the loaded isolated paused save and reloads it unchanged afterward."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/merchant_sight");
        proof.saveName = saveName;
        var rows = new List<object>();
        bool oldAI = FogSettings.AISmart;
        bool oldAllies = FogSettings.AllyGiveVision;
        bool oldOutside = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.BaseViewRange;
        Map map = null;
        Pawn visitor = null,
            target = null;
        Thing wall = null;
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
                    FogSettings.BaseViewRange = 24;
                    FogSettings.OnlyOutsideColony = false;
                    FogSettings.AISmart = false;
                    FogSettings.AllyGiveVision = false;
                    Refresh();
                    var origin = map.AllCells.First(c =>
                        c.x > 3
                        && c.x < map.Size.x - 32
                        && c.z > 3
                        && c.z < map.Size.z - 3
                        && new CellRect(c.x, c.z - 1, 30, 3).All(p =>
                            p.Standable(map)
                            && !p.Roofed(map)
                            && !fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).All(t => t is not Pawn)
                        )
                    );
                    visitor = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncients
                    );
                    target = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncientsHostile
                    );
                    GenSpawn.Spawn(visitor, origin, map);
                    GenSpawn.Spawn(target, origin + IntVec3.East * 2, map);
                    Check(
                        "visitor faction is neutral or allied",
                        visitor.Faction.AllyOrNeutralTo(Faction.OfPlayer)
                    );
                    var source = visitor.TryGetComp<CompFog>().FieldOfViewWatcher;
                    int full = Mathf.RoundToInt(source.CalcPawnSightRange(origin, false, false));
                    int shared = Mathf.RoundToInt(
                        source.CalcPawnSightRange(origin, false, false) * .5f
                    );
                    var far = origin + IntVec3.East * (shared + 1);
                    Check(
                        "fixture has a distinct full and shared range",
                        shared >= 1 && shared < full
                    );
                    foreach (bool ai in new[] { false, true })
                    foreach (bool allies in new[] { false, true })
                    {
                        FogSettings.AISmart = ai;
                        FogSettings.AllyGiveVision = allies;
                        Refresh();
                        bool nearOwn = fog.IsShown(visitor.Faction, target.Position);
                        bool farOwn = fog.IsShown(visitor.Faction, far);
                        bool nearPlayer = fog.IsShown(Faction.OfPlayer, target.Position);
                        bool farPlayer = fog.IsShown(Faction.OfPlayer, far);
                        bool allows = Visibility.AllowsTarget(visitor, target.Position);
                        bool canSee = AttackTargetFinder.CanSee(visitor, target);
                        bool hostileCanSee = AttackTargetFinder.CanSee(target, visitor);
                        rows.Add(
                            new
                            {
                                ai,
                                allies,
                                full,
                                shared,
                                nearOwn,
                                farOwn,
                                nearPlayer,
                                farPlayer,
                                allows,
                                canSee,
                                hostileCanSee,
                            }
                        );
                        Check($"merchant acquisition ai={ai} sharing={allies}", allows && canSee);
                        Check(
                            $"merchant own coverage ai={ai} sharing={allies}",
                            nearOwn == ai && farOwn == ai
                        );
                        Check(
                            $"player sharing stays shorter ai={ai} sharing={allies}",
                            nearPlayer == allies && !farPlayer
                        );
                        Check($"hostile acquisition ai={ai} sharing={allies}", hostileCanSee);
                    }
                    FogSettings.AISmart = true;
                    FogSettings.AllyGiveVision = true;
                    Refresh();
                    var beyond = origin + IntVec3.East * (full + 1);
                    Check(
                        "merchant cannot target beyond its own sight",
                        !Visibility.AllowsTarget(visitor, beyond)
                    );

                    // Compare complete native coverage against the two existing
                    // single-channel modes, including actual combat peeking.
                    wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                    GenSpawn.Spawn(wall, origin + IntVec3.East * 3, map);
                    foreach (bool combat in new[] { false, true })
                    {
                        if (combat)
                            visitor.jobs.StartJob(
                                JobMaker.MakeJob(JobDefOf.Wait_Combat),
                                JobCondition.InterruptForced
                            );
                        FogSettings.AISmart = false;
                        Refresh();
                        var sharedCounts = fog.Coverage.Counts(0).ToArray();
                        FogSettings.AISmart = true;
                        FogSettings.AllyGiveVision = false;
                        Refresh();
                        var ownCounts = fog.Coverage.Counts(visitor.Faction.loadID + 1).ToArray();
                        FogSettings.AllyGiveVision = true;
                        Refresh();
                        Check(
                            $"complete shared mask unchanged combat={combat}",
                            sharedCounts.SequenceEqual(fog.Coverage.Counts(0))
                        );
                        Check(
                            $"complete own mask unchanged combat={combat}",
                            ownCounts.SequenceEqual(fog.Coverage.Counts(visitor.Faction.loadID + 1))
                        );
                    }
                    visitor.jobs.EndCurrentJob(JobCondition.InterruptForced);
                    wall.Destroy();
                    Refresh();
                    FogSettings.BaseViewRange = 12;
                    Refresh();
                    Check(
                        "smaller range removes old own coverage",
                        !fog.IsShown(visitor.Faction, far)
                    );
                    Check(
                        "smaller range removes old shared coverage",
                        !fog.IsShown(Faction.OfPlayer, origin + IntVec3.East * shared)
                    );
                    FogSettings.BaseViewRange = 24;
                    visitor.Position = origin + IntVec3.East * (full + 3);
                    source.UpdateFoV();
                    Check("movement removes old own sight", !fog.IsShown(visitor.Faction, origin));
                    Check(
                        "movement removes old shared sight",
                        !fog.IsShown(Faction.OfPlayer, origin)
                    );
                    Check(
                        "movement publishes both new sight contributions",
                        fog.IsShown(visitor.Faction, visitor.Position)
                            && fog.IsShown(Faction.OfPlayer, visitor.Position)
                    );
                    visitor.SetFaction(Faction.OfPlayer);
                    Refresh();
                    Check(
                        "recruitment removes former faction sight",
                        !fog.IsShown(Faction.OfAncients, visitor.Position)
                    );
                    Check(
                        "recruitment retains player sight",
                        fog.IsShown(Faction.OfPlayer, visitor.Position)
                    );
                    visitor.SetFaction(Faction.OfAncientsHostile);
                    Refresh();
                    Check(
                        "hostility removes shared player sight",
                        !fog.IsShown(Faction.OfPlayer, visitor.Position)
                    );
                    Check(
                        "hostility retains own sight",
                        fog.IsShown(visitor.Faction, visitor.Position)
                    );
                    visitor.SetFaction(Faction.OfAncients);
                    visitor.Position = origin;
                    Refresh();
                    visitor.DeSpawn();
                    Check(
                        "despawn removes visitor faction sight",
                        !fog.IsShown(Faction.OfAncients, origin)
                    );
                    Check(
                        "despawn removes shared player sight",
                        !fog.IsShown(Faction.OfPlayer, origin)
                    );

                    void Refresh()
                    {
                        foreach (var watcher in fog.fowWatchers.ToArray())
                            watcher.UpdateFoV(true);
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
                    visitor?.Destroy();
                    target?.Destroy();
                    if (wall?.Spawned == true)
                        wall.Destroy();
                    FogSettings.AISmart = oldAI;
                    FogSettings.AllyGiveVision = oldAllies;
                    FogSettings.OnlyOutsideColony = oldOutside;
                    FogSettings.BaseViewRange = oldRange;
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    var assembly = typeof(MapVisibility).Assembly;
                    using var stream = File.OpenRead(assembly.Location);
                    using var hash = SHA256.Create();
                    proof.environment.details = new
                    {
                        rows,
                        gameplayMvid = assembly.ManifestModule.ModuleVersionId,
                        gameplayPath = assembly.Location,
                        gameplaySha256 = BitConverter
                            .ToString(hash.ComputeHash(stream))
                            .Replace("-", "")
                            .ToLowerInvariant(),
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => m.PackageId)
                            .ToArray(),
                    };
                },
                CancellationToken.None
            );
            if (map != null)
            {
                var reload = await ctx.Tools.CallAsync(
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
                    RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
                );
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;

        void Check(string name, bool passed) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, passed));
    }
}
