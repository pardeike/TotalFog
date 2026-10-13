using System;
using System.Linq;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class HaulingScenarios
{
    [Tool(
        "totalfog/hauling_fixture",
        Description = "Stage a plain vanilla chunk, a colonist and a matching stockpile; lose actual pawn sight, probe native reservations/jobs, remove the haul order, or start the native haul job. Reload the unchanged fixture afterward."
    )]
    public static object Fixture(
        string operation = "setup",
        string targetID = "",
        string pawnID = ""
    )
    {
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a fixture first.");
        var fog = map.GetVisibility();
        if (!Find.TickManager.Paused || !fog.Initialized)
            throw new InvalidOperationException("Use a paused initialized map.");
        Thing target;
        Pawn pawn;
        if (operation is "setup" or "setup-construction")
        {
            var cell = map.AllCells.First(c =>
                c.x > 4
                && c.z > 4
                && c.x < map.Size.x - 8
                && c.z < map.Size.z - 8
                && !fog.IsShown(Faction.OfPlayer, c)
                && new CellRect(c.x, c.z, 6, 3).All(p =>
                    p.Standable(map)
                    && p.GetZone(map) == null
                    && p.GetThingList(map).All(t => t is Plant or Filth)
                )
            );
            foreach (var p in new CellRect(cell.x, cell.z, 6, 3))
                map.fogGrid.Unfog(p);
            target = GenSpawn.Spawn(
                ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChunkMarble")),
                cell,
                map
            );
            for (int attempt = 0; ; attempt++)
            {
                pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (
                    !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
                )
                    break;
                if (attempt == 19)
                    throw new InvalidOperationException(
                        "Could not generate a capable construction/hauling fixture pawn."
                    );
            }
            GenSpawn.Spawn(pawn, cell + new IntVec3(2, 0, 0), map);
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 3000;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
            var stockpile = new Zone_Stockpile(
                StorageSettingsPreset.DefaultStockpile,
                map.zoneManager
            );
            stockpile.settings.filter.SetDisallowAll();
            stockpile.settings.filter.SetAllow(target.def, true);
            stockpile.settings.Priority = StoragePriority.Critical;
            map.zoneManager.RegisterZone(stockpile);
            stockpile.AddCell(cell + new IntVec3(4, 0, 0));
            if (operation == "setup-construction")
            {
                GenConstruct.PlaceBlueprintForBuild(
                    ThingDefOf.Wall,
                    cell,
                    map,
                    Rot4.North,
                    Faction.OfPlayer,
                    ThingDefOf.WoodLog
                );
                pawn.skills.GetSkill(SkillDefOf.Construction).Level = 10;
                pawn.workSettings.EnableAndInitialize();
                pawn.workSettings.SetPriority(WorkTypeDefOf.Construction, 1);
                pawn.workSettings.SetPriority(WorkTypeDefOf.Hauling, 1);
                var wood = ThingMaker.MakeThing(ThingDefOf.WoodLog);
                wood.stackCount = 20;
                GenSpawn.Spawn(wood, pawn.Position + new IntVec3(0, 0, 1), map);
            }
            else
                map.designationManager.AddDesignation(
                    new Designation(target, DesignationDefOf.Haul)
                );
        }
        else
        {
            target = map.listerThings.AllThings.First(t => t.ThingID == targetID);
            pawn = map.mapPawns.AllPawnsSpawned.First(p => p.ThingID == pawnID);
            if (operation == "lose-sight")
            {
                var source = pawn.TryGetComp<CompFog>().FieldOfViewWatcher;
                float range = source.CalcPawnSightRange(pawn.Position, false, false);
                var destination = map.AllCells.First(c =>
                    c.Standable(map)
                    && !fog.IsShown(Faction.OfPlayer, c)
                    && c.DistanceTo(target.Position) > range + 10
                    && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly)
                );
                map.fogGrid.Unfog(destination);
                pawn.pather.StopDead();
                pawn.Position = destination;
                source.UpdateFoV(true);
            }
            else if (operation == "remove-order")
                map.designationManager.TryRemoveDesignationOn(target, DesignationDefOf.Haul);
            else if (operation == "restore-order")
                map.designationManager.AddDesignation(
                    new Designation(target, DesignationDefOf.Haul)
                );
            else if (operation == "start-construction")
            {
                var plan = target
                    .Position.GetThingList(map)
                    .First(t => t.def.IsBlueprint && t.Faction == pawn.Faction);
                var job =
                    new WorkGiver_ConstructDeliverResourcesToBlueprints().JobOnThing(pawn, plan)
                    ?? throw new InvalidOperationException(
                        "The native construction job is unavailable."
                    );
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            else if (operation == "start")
            {
                var job =
                    new WorkGiver_HaulGeneral().JobOnThing(pawn, target, true)
                    ?? throw new InvalidOperationException("The native haul job is unavailable.");
                pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            }
            else if (operation != "probe")
                throw new ArgumentException(
                    "Use setup, lose-sight, probe, remove-order, restore-order or start.",
                    nameof(operation)
                );
        }
        var construction = target
            .Position.GetThingList(map)
            .FirstOrDefault(t => t.def.IsBlueprint || t.def.IsFrame);
        var constructionJob =
            construction == null
                ? null
                : new WorkGiver_ConstructDeliverResourcesToBlueprints().JobOnThing(
                    pawn,
                    construction
                );
        var blockingJob =
            construction == null ? null : GenConstruct.HandleBlockingThingJob(construction, pawn);
        JobFailReason.Clear();
        var nextJob = new WorkGiver_HaulGeneral().JobOnThing(pawn, target, true);
        return new
        {
            tick = Find.TickManager.TicksGame,
            targetID = target.ThingID,
            pawnID = pawn.ThingID,
            chunkClass = target.GetType().FullName,
            constructionID = construction?.ThingID,
            constructionJob = constructionJob?.def.defName,
            blockingJob = blockingJob?.def.defName,
            blockingTarget = blockingJob?.targetA.Thing?.ThingID,
            chunkPosition = target.Position.ToString(),
            pawnPosition = pawn.Position.ToString(),
            visible = Visibility.IsVisible(target),
            known = fog.IsKnown(Faction.OfPlayer, map.cellIndices.CellToIndex(target.Position)),
            designated = map.designationManager.DesignationOn(target, DesignationDefOf.Haul)
                != null,
            canReserve = pawn.CanReserve(target),
            canReserveAndReach = pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly),
            job = nextJob?.def.defName,
            destination = nextJob?.targetB.Cell.ToString(),
            currentJob = pawn.CurJob?.def.defName,
            storage = StoreUtility.CurrentStoragePriorityOf(target, false).ToString(),
            reason = JobFailReason.HaveReason ? JobFailReason.Reason : null,
        };
    }

    [Tool(
        "totalfog/hauling_probe",
        Description = "Read chunk observation, faction sight, storage and native haul-job eligibility near a cell. No jobs are started and no objects are moved."
    )]
    public static object Probe(int x = 100, int z = 126, int radius = 6)
    {
        if (radius < 0 || radius > 15)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a save first.");
        var fog = map.GetVisibility();
        var center = new IntVec3(x, 0, z);
        var pawns = map.mapPawns.FreeColonistsSpawned.ToArray();
        return new
        {
            tick = Find.TickManager.TicksGame,
            initialized = fog.Initialized,
            chunks = map
                .listerThings.AllThings.Where(t =>
                    t.def.IsWithinCategory(ThingCategoryDefOf.StoneChunks)
                    && t.Position.DistanceTo(center) <= radius
                )
                .Select(t => new
                {
                    id = t.ThingID,
                    x = t.Position.x,
                    z = t.Position.z,
                    visible = Visibility.IsVisible(t),
                    known = fog.IsKnown(Faction.OfPlayer, map.cellIndices.CellToIndex(t.Position)),
                    observed = t.TryGetComp<CompFog>()?.HideFromPlayer.SeenByPlayer,
                    storage = StoreUtility.CurrentStoragePriorityOf(t, false).ToString(),
                    pawns = pawns
                        .Select(p =>
                        {
                            JobFailReason.Clear();
                            var job = new WorkGiver_HaulGeneral().JobOnThing(p, t, true);
                            return new
                            {
                                id = p.ThingID,
                                name = p.LabelShort,
                                job = job?.def.defName,
                                destination = job?.targetB.Cell.ToString(),
                                reason = JobFailReason.HaveReason ? JobFailReason.Reason : null,
                            };
                        })
                        .ToArray(),
                })
                .ToArray(),
        };
    }
}
