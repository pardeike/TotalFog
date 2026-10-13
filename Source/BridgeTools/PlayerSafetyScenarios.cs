using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native owned-pawn controls, tower fog targeting and failed-rescuer recovery.</summary>
public sealed class PlayerSafetyScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/player_safety",
        Description = "Check owned-pawn selection without shared sight, native tower acquisition and Thing/cell shoot lines, and death of a rescuer before pickup followed by replacement retrieval. Uses paired scoped sight controls without advancing ticks during them; restores settings and reloads the unchanged paused single-player save."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (string.IsNullOrWhiteSpace(saveName) || string.IsNullOrWhiteSpace(evidenceLabel))
            throw new ArgumentException("Supply the loaded save name and evidence label.");
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/player_safety", evidenceLabel);
        proof.saveName = saveName;
        var rows = new List<object>();
        bool staged = false;
        bool replacementOrdered = false;
        bool oldShooting = FogSettings.TowerShootingAllowed;
        bool oldEnemyFog = FogSettings.AISmart;
        Map map = null;
        Building_Watchtower tower = null;
        Pawn patient = null,
            rescuer = null,
            replacement = null;
        try
        {
            await On(() =>
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
                var origin = map.AllCells.First(c =>
                    c.x > 8
                    && c.z > 8
                    && new CellRect(c.x, c.z, 22, 16).InBounds(map)
                    && new CellRect(c.x, c.z, 22, 16).All(p =>
                        p.Standable(map)
                        && !p.Roofed(map)
                        && p.GetThingList(map).All(t => t is Plant or Filth)
                    )
                );
                staged = true;
                foreach (var cell in new CellRect(origin.x, origin.z, 22, 16))
                {
                    foreach (var thing in cell.GetThingList(map).ToArray())
                        thing.Destroy(DestroyMode.Vanish);
                    map.fogGrid.Unfog(cell);
                }
                FogSettings.TowerShootingAllowed = true;
                FogSettings.AISmart = false;
                var pet = AddPawn(PawnKindDefOf.Muffalo, origin, Faction.OfPlayer);
                var foreign = AddPawn(PawnKindDefOf.Muffalo, origin + IntVec3.East, null);
                var fog = map.GetVisibility();
                var counts = fog.GetFactionShownCells(Faction.OfPlayer);
                int index = map.cellIndices.CellToIndex(pet.Position);
                int saved = counts[index];
                int foreignIndex = map.cellIndices.CellToIndex(foreign.Position);
                int savedForeign = counts[foreignIndex];
                counts[foreignIndex] = 0;
                counts[index] = 0;
                try
                {
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(pet);
                    pet.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
                    Check(
                        "owned animal without sight remains selected",
                        Find.Selector.IsSelected(pet)
                    );
                    Check(
                        "owned animal remains available in native UI policy",
                        Presentation.InterfaceVisibility.PawnLabelPrefix(pet)
                    );
                    Check(
                        "owned animal does not grant live effect sight",
                        !Visibility.IsVisible(pet)
                    );
                    Check(
                        "unseen foreign pawn stays unavailable",
                        !Presentation.ThingVisibility.CanInspect(foreign)
                    );
                }
                finally
                {
                    counts[index] = saved;
                    counts[foreignIndex] = savedForeign;
                    Find.Selector.ClearSelection();
                }
                tower = (Building_Watchtower)
                    ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, ThingDefOf.WoodLog);
                tower.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(tower, origin + new IntVec3(5, 0, 5), map, Rot4.South);
                patient = AddPawn(PawnKindDefOf.Colonist, tower.Approach, Faction.OfPlayer);
                rescuer = AddPawn(
                    PawnKindDefOf.Colonist,
                    tower.Approach + IntVec3.West * 2,
                    Faction.OfPlayer
                );
                replacement = AddPawn(
                    PawnKindDefOf.Colonist,
                    tower.Approach + IntVec3.East * 2,
                    Faction.OfPlayer
                );
                patient.equipment.AddEquipment(
                    (ThingWithComps)
                        ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"))
                );
                patient.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
                );
            });
            await Until(() => tower.ProvidesSight, "watcher reaches deck");
            await On(() =>
            {
                var target = AddPawn(
                    PawnKindDefOf.Colonist,
                    tower.Position + IntVec3.East * 12,
                    Faction.OfAncientsHostile
                );
                var counts = map.GetVisibility().GetFactionShownCells(Faction.OfPlayer);
                int index = map.cellIndices.CellToIndex(target.Position);
                int saved = counts[index];
                var verb = patient.equipment.Primary.GetComp<CompEquippable>().PrimaryVerb;
                try
                {
                    counts[index] = 0;
                    bool hiddenAcquire = AttackTargetFinder.CanSee(patient, target);
                    bool hiddenShot = verb.TryFindShootLineFromTo(patient.Position, target, out _);
                    bool blindCell = verb.TryFindShootLineFromTo(
                        patient.Position,
                        target.Position,
                        out _
                    );
                    counts[index] = 1;
                    bool seenAcquire = AttackTargetFinder.CanSee(patient, target);
                    bool seenShot = verb.TryFindShootLineFromTo(patient.Position, target, out _);
                    Check("tower cannot acquire unseen Thing", !hiddenAcquire);
                    Check("tower cannot track unseen Thing through shoot line", !hiddenShot);
                    Check("deliberate blind cell shot stays available", blindCell);
                    Check(
                        "seen target retains native acquisition and shoot line",
                        seenAcquire && seenShot
                    );
                    rows.Add(
                        new
                        {
                            hiddenAcquire,
                            hiddenShot,
                            blindCell,
                            seenAcquire,
                            seenShot,
                        }
                    );
                }
                finally
                {
                    counts[index] = saved;
                    target.Destroy(DestroyMode.Vanish);
                }
                HealthUtility.DamageUntilDowned(patient);
                if (patient.Dead || !patient.Downed)
                    throw new InvalidOperationException("Patient downing prerequisite failed.");
                var retrieve = JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerRetrieve, tower);
                retrieve.targetC = patient;
                rescuer.jobs.TryTakeOrderedJob(retrieve);
            });
            await Until(
                () => tower.Rescuer == rescuer && tower.RescuerElevation > .4f,
                "rescuer partway up ladder"
            );
            await On(() =>
            {
                float height = tower.RescuerElevation;
                rescuer.Kill(null);
                Check("dead rescuer relinquishes temporary slot", tower.Rescuer == null);
                Check(
                    "dead rescuer has one retrievable corpse on ground",
                    rescuer.Corpse?.Spawned == true
                        && rescuer.Corpse.Position.Walkable(map)
                        && TowerPresentation.TowerFor(rescuer.Corpse) == null
                );
                Check(
                    "original lookout remains on platform",
                    tower.Occupant == patient && tower.Elevation == 1
                );
                bool canReplace = tower.CanEnter(replacement, patient);
                Check("another rescuer can enter after failure", canReplace);
                rows.Add(
                    new
                    {
                        height,
                        rescuer = tower.Rescuer?.ThingID,
                        corpseCell = rescuer.Corpse?.Position.ToString(),
                        canReplace,
                    }
                );
                if (canReplace)
                {
                    var retrieve = JobMaker.MakeJob(
                        WatchtowerDefOf.TotalFog_WatchtowerRetrieve,
                        tower
                    );
                    retrieve.targetC = patient;
                    replacementOrdered = replacement.jobs.TryTakeOrderedJob(retrieve);
                }
            });
            if (replacementOrdered)
            {
                await Until(
                    () => tower.Occupant == null && tower.Rescuer == null && patient.Spawned,
                    "replacement completes retrieval"
                );
                await On(() =>
                    Check(
                        "replacement puts original patient on walkable ground",
                        patient.Position.Walkable(map)
                    )
                );
            }
        }
        catch (Exception error)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        FogSettings.TowerShootingAllowed = oldShooting;
                        FogSettings.AISmart = oldEnemyFog;
                        proof.environment.details = new { rows };
                    },
                    CancellationToken.None
                );
                if (staged)
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
            finally
            {
                gate.Release();
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;

        Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
        void Check(string name, bool passed) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, passed));
        Pawn AddPawn(PawnKindDef kind, IntVec3 cell, Faction faction)
        {
            var pawn = PawnGenerator.GeneratePawn(kind, faction);
            GenSpawn.Spawn(pawn, cell, map);
            if (pawn.drafter != null)
                pawn.drafter.Drafted = true;
            return pawn;
        }
        async Task Until(Func<bool> done, string label)
        {
            for (int ticks = 0; ticks < 1200; ticks++)
            {
                if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                    return;
                var receipt = await ctx.Game.StepTicksAsync(
                    1,
                    cancellationToken: cancellationToken
                );
                if (!receipt.Success)
                    throw new InvalidOperationException("Tick stepping: " + receipt.Status);
            }
            throw new InvalidOperationException("Timed out: " + label);
        }
    }
}
