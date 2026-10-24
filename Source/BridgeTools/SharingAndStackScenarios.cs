using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Presentation;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TotalFog.BridgeTools;

public sealed class SharingAndStackScenarios
{
    [Tool(
        "totalfog/sharing_and_stacks",
        Description = "Check native prisoner target acquisition, sharing settings, partial-stack knowledge, arrival consumption and unknown cargo in an isolated paused fixture, then restore settings and reload it unchanged."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/sharing_and_stacks");
        proof.saveName = saveName;
        var rows = new List<object>();
        bool oldAI = FogSettings.AISmart,
            oldAllies = FogSettings.AllyGiveVision,
            oldPrisoners = FogSettings.PrisonerGiveVision,
            oldOutside = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.BaseViewRange;
        var owned = new List<Thing>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map = Find.CurrentMap;
                    if (
                        map?.GetVisibility().Initialized != true
                        || !Find.TickManager.Paused
                        || Compatibility.MultiplayerIntegration.Active
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized single-player fixture."
                        );
                    var fog = map.GetVisibility();
                    FogSettings.AISmart = true;
                    FogSettings.AllyGiveVision = false;
                    FogSettings.PrisonerGiveVision = false;
                    FogSettings.OnlyOutsideColony = false;
                    FogSettings.BaseViewRange = 24;
                    Refresh();
                    var origin = map.AllCells.First(c =>
                        c.x > 5
                        && c.z > 5
                        && c.x < map.Size.x - 30
                        && c.z < map.Size.z - 5
                        && new CellRect(c.x - 1, c.z - 1, 8, 3).All(p =>
                            p.Standable(map)
                            && !p.Roofed(map)
                            && !fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).Count == 0
                        )
                    );
                    var prisoner = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncientsHostile
                    );
                    var guard = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfPlayer
                    );
                    owned.Add(prisoner);
                    owned.Add(guard);
                    GenSpawn.Spawn(prisoner, origin, map);
                    GenSpawn.Spawn(guard, origin + IntVec3.East * 2, map);
                    prisoner.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                    LordMaker.MakeNewLord(
                        prisoner.Faction,
                        new LordJob_PrisonBreak(
                            origin,
                            new IntVec3(map.Size.x - 1, 0, origin.z),
                            -1
                        ),
                        map,
                        new[] { prisoner }
                    );
                    Check(
                        "native prison-break fixture retains prisoner status",
                        prisoner.IsPrisonerOfColony && PrisonBreakUtility.IsPrisonBreaking(prisoner)
                    );
                    foreach (bool sharing in new[] { false, true })
                    {
                        FogSettings.PrisonerGiveVision = sharing;
                        Refresh();
                        bool ownSight = fog.IsShown(prisoner.Faction, guard.Position);
                        bool canSee = AttackTargetFinder.CanSee(prisoner, guard);
                        rows.Add(
                            new
                            {
                                phase = "prisoner-sharing",
                                sharing,
                                ownSight,
                                canSee,
                                prisoner.IsPrisonerOfColony,
                            }
                        );
                        Check(
                            $"prisoner retains target acquisition sharing={sharing}",
                            ownSight && canSee
                        );
                    }

                    var cargo = ThingMaker.MakeThing(ThingDefOf.MedicineIndustrial);
                    cargo.stackCount = 20;
                    owned.Add(cargo);
                    GenSpawn.Spawn(cargo, origin + IntVec3.East, map);
                    cargo.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
                    Check(
                        "parent stack is observed before split",
                        cargo.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer)
                    );
                    cargo.DeSpawn();
                    var part = cargo.SplitOff(5);
                    owned.Add(part);
                    bool partSeen =
                        part.TryGetComp<CompFog>()?.HideFromPlayer?.WasSeenBy(Faction.OfPlayer)
                        == true;
                    rows.Add(
                        new
                        {
                            phase = "native-partial-pickup",
                            remaining = cargo.stackCount,
                            split = part.stackCount,
                            partSeen,
                            partType = part.GetType().FullName,
                            hasFogComponent = part.TryGetComp<CompFog>() != null,
                        }
                    );
                    Check("native partial pickup retains known units", partSeen);
                    guard.Destroy();
                    prisoner
                        .GetLord()
                        ?.Notify_PawnLost(prisoner, PawnLostCondition.ForcedToJoinOtherLord);
                    prisoner.SetFaction(Faction.OfAncients);
                    prisoner.guest.SetGuestStatus(Faction.OfPlayer, GuestStatus.Prisoner);
                    var enemy = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncientsHostile
                    );
                    owned.Add(enemy);
                    GenSpawn.Spawn(enemy, origin + IntVec3.East * 2, map);
                    int sharedRange = UnityEngine.Mathf.RoundToInt(
                        prisoner
                            .TryGetComp<CompFog>()
                            .FieldOfViewWatcher.CalcPawnSightRange(origin, false, false) * .2f
                    );
                    Check("fixture has a one-fifth sharing boundary", sharedRange >= 2);
                    foreach (bool ai in new[] { false, true })
                    foreach (bool sharing in new[] { false, true })
                    foreach (bool allies in new[] { false, true })
                    {
                        FogSettings.AISmart = ai;
                        FogSettings.PrisonerGiveVision = sharing;
                        FogSettings.AllyGiveVision = allies;
                        Refresh();
                        bool own = fog.IsShown(prisoner.Faction, enemy.Position);
                        bool shared = fog.IsShown(Faction.OfPlayer, enemy.Position);
                        bool farShared = fog.IsShown(
                            Faction.OfPlayer,
                            origin + IntVec3.East * (sharedRange + 1)
                        );
                        bool canSee = AttackTargetFinder.CanSee(prisoner, enemy);
                        rows.Add(
                            new
                            {
                                phase = "prisoner-settings-matrix",
                                ai,
                                sharing,
                                allies,
                                own,
                                shared,
                                farShared,
                                canSee,
                            }
                        );
                        Check(
                            $"prisoner settings ai={ai} sharing={sharing} allies={allies}",
                            own == ai && shared == sharing && !farShared && canSee
                        );
                    }
                    FogSettings.AISmart = true;
                    FogSettings.PrisonerGiveVision = false;
                    FogSettings.AllyGiveVision = true;
                    Refresh();

                    var landing = map.AllCells.First(c =>
                        c.x > 5
                        && c.z > 5
                        && c.x < map.Size.x - 5
                        && c.z < map.Size.z - 5
                        && c.DistanceTo(origin) > 40
                        && new CellRect(c.x, c.z, 4, 2).All(p =>
                            p.Standable(map)
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).Count == 0
                        )
                    );
                    var info = new ActiveTransporterInfo();
                    info.innerContainer.TryAdd(part);
                    Compatibility.TransportPodVisibility.Arriving(
                        new List<ActiveTransporterInfo> { info }
                    );
                    info.innerContainer.Remove(part);
                    GenSpawn.Spawn(part, landing, map);
                    bool splitArrivalKnown = part.TryGetComp<CompFog>()
                        .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer);
                    info.innerContainer.TryAdd(cargo);
                    Compatibility.TransportPodVisibility.Arriving(
                        new List<ActiveTransporterInfo> { info }
                    );
                    info.innerContainer.Remove(cargo);
                    GenSpawn.Spawn(cargo, landing + IntVec3.East, map);
                    bool parentArrivalKnown = cargo
                        .TryGetComp<CompFog>()
                        .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer);
                    rows.Add(
                        new
                        {
                            phase = "player-arrival-after-partial-pickup",
                            splitArrivalKnown,
                            parentArrivalKnown,
                        }
                    );
                    Check("unsplit remainder arrival keeps knowledge", parentArrivalKnown);
                    Check("split cargo arrival keeps knowledge", splitArrivalKnown);

                    // Copy the one-use permission when native placement splits
                    // a stack after the player arrival action has run.
                    var afterMarker = cargo.SplitOff(4);
                    owned.Add(afterMarker);
                    Check(
                        "native split retains observation history",
                        afterMarker
                            .TryGetComp<CompFog>()
                            ?.HideFromPlayer?.WasSeenBy(Faction.OfPlayer) == true
                    );
                    info.innerContainer.TryAdd(afterMarker);
                    Compatibility.TransportPodVisibility.Arriving(
                        new List<ActiveTransporterInfo> { info }
                    );
                    var markedPart = afterMarker.SplitOff(2);
                    owned.Add(markedPart);
                    GenSpawn.Spawn(markedPart, landing + IntVec3.North, map);
                    Check(
                        "split after arrival action retains permission",
                        markedPart
                            .TryGetComp<CompFog>()
                            .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
                    );
                    markedPart.DeSpawn();
                    var hiddenMove = landing + IntVec3.East * 3;
                    map.fogGrid.Unfog(hiddenMove);
                    fog.RevealCell(map.cellIndices.CellToIndex(hiddenMove));
                    GenSpawn.Spawn(markedPart, hiddenMove, map);
                    Check(
                        "copied arrival permission is consumed",
                        !ThingVisibility.CanInspect(markedPart)
                            && !markedPart
                                .TryGetComp<CompFog>()
                                .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
                    );

                    var unknown = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
                    unknown.stackCount = 4;
                    owned.Add(unknown);
                    var unknownPart = unknown.SplitOff(2);
                    owned.Add(unknownPart);
                    info.innerContainer.TryAdd(unknownPart);
                    Compatibility.TransportPodVisibility.Arriving(
                        new List<ActiveTransporterInfo> { info }
                    );
                    info.innerContainer.Remove(unknownPart);
                    var unknownCell = landing + IntVec3.East * 2;
                    map.fogGrid.Unfog(unknownCell);
                    fog.RevealCell(map.cellIndices.CellToIndex(unknownCell));
                    GenSpawn.Spawn(unknownPart, unknownCell, map);
                    Check(
                        "unknown split cargo stays unknown on explored terrain",
                        !unknownPart
                            .TryGetComp<CompFog>()
                            .HideFromPlayer.WasSeenBy(Faction.OfPlayer)
                            && !ThingVisibility.CanInspect(unknownPart)
                    );
                    var whole = afterMarker.SplitOff(afterMarker.stackCount);
                    Check(
                        "whole-stack pickup preserves native identity",
                        ReferenceEquals(whole, afterMarker)
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
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy();
                    FogSettings.AISmart = oldAI;
                    FogSettings.AllyGiveVision = oldAllies;
                    FogSettings.PrisonerGiveVision = oldPrisoners;
                    FogSettings.OnlyOutsideColony = oldOutside;
                    FogSettings.BaseViewRange = oldRange;
                    var assembly = typeof(MapVisibility).Assembly;
                    using var stream = File.OpenRead(assembly.Location);
                    using var hash = SHA256.Create();
                    proof.environment.details = new
                    {
                        rows,
                        gameplayMvid = assembly.ManifestModule.ModuleVersionId,
                        gameplaySha256 = BitConverter
                            .ToString(hash.ComputeHash(stream))
                            .Replace("-", "")
                            .ToLowerInvariant(),
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => m.PackageId)
                            .ToArray(),
                    };
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                },
                CancellationToken.None
            );
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
        RimBridgeEvidence.Complete(proof);
        return proof;

        void Check(string label, bool passed) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(label, passed));
    }
}
