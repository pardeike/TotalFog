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
        Description = "Stage a chunk or animal corpse and test native hauling, storage and bill ingredient searches after sight loss. Also supports construction, cleaning and replacement controls. Reload the unchanged fixture afterward."
    )]
    public static object Fixture(
        string operation = "setup",
        string targetID = "",
        string pawnID = "",
        string itemKind = "chunk",
        string billGiverID = ""
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
            if (itemKind == "corpse")
            {
                var animal = PawnGenerator.GeneratePawn(
                    DefDatabase<PawnKindDef>.GetNamed("Muffalo")
                );
                GenSpawn.Spawn(animal, cell, map);
                animal.Kill(null);
                target = animal.Corpse;
            }
            else if (itemKind == "chunk")
                target = GenSpawn.Spawn(
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ChunkMarble")),
                    cell,
                    map
                );
            else
                throw new ArgumentException("Use chunk or corpse.", nameof(itemKind));
            if (target is ThingWithComps)
                target.SetForbidden(false);
            for (int attempt = 0; ; attempt++)
            {
                pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                if (
                    !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.PlantCutting)
                    && !pawn.WorkTypeIsDisabled(WorkTypeDefOf.Cleaning)
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
                pawn.workSettings.SetPriority(WorkTypeDefOf.PlantCutting, 1);
                pawn.workSettings.SetPriority(WorkTypeDefOf.Cleaning, 1);
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
            if (operation == "store")
            {
                var stockpile = target.Position.GetZone(map) as Zone_Stockpile;
                if (stockpile == null)
                {
                    stockpile = new Zone_Stockpile(
                        StorageSettingsPreset.DefaultStockpile,
                        map.zoneManager
                    );
                    map.zoneManager.RegisterZone(stockpile);
                    stockpile.AddCell(target.Position);
                }
                stockpile.settings.filter.SetDisallowAll();
                stockpile.settings.filter.SetAllow(target.def, true);
                stockpile.settings.Priority = StoragePriority.Critical;
                map.designationManager.TryRemoveDesignationOn(target, DesignationDefOf.Haul);
            }
            else if (operation == "setup-bill")
            {
                var table = (Building_WorkTable)
                    ThingMaker.MakeThing(
                        DefDatabase<ThingDef>.GetNamed(
                            target is Corpse ? "TableButcher" : "TableStonecutter"
                        ),
                        ThingDefOf.WoodLog
                    );
                var location = GenRadial
                    .RadialCellsAround(pawn.Position, 8, false)
                    .First(c =>
                        new CellRect(c.x - 2, c.z - 2, 5, 5).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                GenSpawn.Spawn(table, location, map);
                table.SetFaction(pawn.Faction);
                foreach (var p in table.OccupiedRect())
                    map.areaManager.Home[p] = true;
                var recipe = DefDatabase<RecipeDef>.GetNamed(
                    target is Corpse ? "ButcherCorpseFlesh" : "Make_StoneBlocksAny"
                );
                var bill = recipe.MakeNewBill();
                bill.ingredientFilter.SetDisallowAll();
                bill.ingredientFilter.SetAllow(target.def, true);
                table.billStack.AddBill(bill);
                billGiverID = table.ThingID;
            }
            else if (operation == "lose-sight")
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
            else if (operation == "restore-sight")
            {
                pawn.pather.StopDead();
                pawn.Position = GenRadial
                    .RadialCellsAround(target.Position, 4f, true)
                    .First(c => c.InBounds(map) && c.Standable(map));
                map.fogGrid.Unfog(pawn.Position);
                pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                target.TryGetComp<CompFog>()?.HideFromPlayer.UpdateVisibility(true);
            }
            else if (operation == "remove-order")
                map.designationManager.TryRemoveDesignationOn(target, DesignationDefOf.Haul);
            else if (operation == "restore-order")
                map.designationManager.AddDesignation(
                    new Designation(target, DesignationDefOf.Haul)
                );
            else if (
                operation
                is "replace-plant"
                    or "replace-building"
                    or "replace-filth"
                    or "replace-item"
            )
            {
                if (Visibility.IsVisible(target))
                    throw new InvalidOperationException(
                        "Lose sight before replacing the obstruction."
                    );
                var cell = target.Position;
                foreach (
                    var plan in cell.GetThingList(map)
                        .Where(t => t.def.IsBlueprint || t.def.IsFrame)
                        .ToArray()
                )
                    plan.Destroy();
                target.Destroy();
                if (operation == "replace-filth")
                {
                    map.terrainGrid.SetTerrain(cell, DefDatabase<TerrainDef>.GetNamed("Concrete"));
                    FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt);
                    target = cell.GetThingList(map).OfType<Filth>().Single();
                    map.areaManager.Home[cell] = true;
                }
                else if (operation == "replace-item")
                    target = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), cell, map);
                else
                {
                    target = GenSpawn.Spawn(
                        ThingMaker.MakeThing(
                            operation == "replace-plant"
                                ? DefDatabase<ThingDef>.GetNamed("Plant_TreeOak")
                                : ThingDefOf.Wall,
                            operation == "replace-building" ? ThingDefOf.WoodLog : null
                        ),
                        cell,
                        map
                    );
                    if (target is Plant plant)
                        plant.Growth = 1f;
                    GenConstruct.PlaceBlueprintForBuild(
                        operation == "replace-building" ? ThingDefOf.Door : ThingDefOf.Wall,
                        cell,
                        map,
                        Rot4.North,
                        pawn.Faction,
                        ThingDefOf.WoodLog
                    );
                }
            }
            else if (operation == "remove-plan")
            {
                foreach (
                    var plan in target
                        .Position.GetThingList(map)
                        .Where(t => t.def.IsBlueprint || t.def.IsFrame)
                        .ToArray()
                )
                    plan.Destroy();
            }
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
                    "Use setup, setup-construction, setup-bill, store, lose-sight, restore-sight, probe, replace-plant, replace-building, replace-filth, replace-item, remove-plan, remove-order, restore-order, start-construction or start.",
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
        var storageJob = target.def.EverHaulable
            ? Verse.AI.HaulAIUtility.HaulToStorageJob(pawn, target, false)
            : null;
        var billGiver = map
            .listerThings.AllThings.OfType<Building_WorkTable>()
            .FirstOrDefault(t => t.ThingID == billGiverID);
        var ingredients = new System.Collections.Generic.List<ThingCount>();
        bool? ingredientsFound =
            billGiver == null
                ? null
                : (bool)
                    HarmonyLib
                        .AccessTools.Method(typeof(WorkGiver_DoBill), "TryFindBestBillIngredients")
                        .Invoke(
                            null,
                            new object[]
                            {
                                billGiver.billStack.Bills[0],
                                pawn,
                                billGiver,
                                ingredients,
                                null,
                            }
                        );
        return new
        {
            billGiverID,
            ingredientsFound,
            ingredientIDs = ingredients.Select(t => t.Thing.ThingID).ToArray(),
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
            observed = target.TryGetComp<CompFog>()?.HideFromPlayer.WasSeenBy(pawn.Faction),
            cleaningJob = target is Filth
                ? new WorkGiver_CleanFilth().JobOnThing(pawn, target)?.def.defName
                : null,
            cleaningEligible = target is Filth
                && new WorkGiver_CleanFilth().HasJobOnThing(pawn, target),
            filthAge = (target as Filth)?.TicksSinceThickened,
            designated = map.designationManager.DesignationOn(target, DesignationDefOf.Haul)
                != null,
            canReserve = pawn.CanReserve(target),
            canReserveAndReach = pawn.CanReserveAndReach(target, PathEndMode.Touch, Danger.Deadly),
            job = nextJob?.def.defName,
            storageJob = storageJob?.def.defName,
            storageDestination = storageJob?.targetB.Cell.ToString(),
            destination = nextJob?.targetB.Cell.ToString(),
            currentJob = pawn.CurJob?.def.defName,
            storage = StoreUtility.CurrentStoragePriorityOf(target, false).ToString(),
            reason = JobFailReason.HaveReason ? JobFailReason.Reason : null,
        };
    }

    [Tool(
        "totalfog/map_search_fixture",
        Description = "Run the native map-search acceptance and population paths for a staged target, optionally move it into the fixture pawn's inventory or change that holder's faction. Reload the unchanged save afterward."
    )]
    public static object Search(string targetID, string pawnID, string operation = "probe")
    {
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a fixture first.");
        if (!Find.TickManager.Paused)
            throw new InvalidOperationException("Pause before querying search.");
        var pawn = map.mapPawns.AllPawnsSpawned.First(p => p.ThingID == pawnID);
        var target = map
            .listerThings.AllThings.Concat(pawn.inventory.innerContainer)
            .First(t => t.ThingID == targetID);
        if (operation == "hold")
        {
            target.DeSpawn();
            if (!pawn.inventory.innerContainer.TryAdd(target))
                throw new InvalidOperationException("Fixture inventory rejected the target.");
        }
        else if (operation == "hostile-holder")
        {
            pawn.SetFaction(Faction.OfAncientsHostile);
            var wait = JobMaker.MakeJob(JobDefOf.Wait);
            wait.expiryInterval = 3000;
            pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
            pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
        }
        else if (operation != "probe")
            throw new ArgumentException("Use probe, hold or hostile-holder.", nameof(operation));
        var dialog = new Dialog_MapSearch(map);
        var quickSearch = (QuickSearchWidget)
            HarmonyLib
                .AccessTools.Field(typeof(Dialog_Search<Thing>), "quickSearchWidget")
                .GetValue(dialog);
        quickSearch.filter.Text = target.def.label;
        bool eligible = (bool)
            HarmonyLib
                .AccessTools.Method(typeof(Dialog_MapSearch), "CanAddThing")
                .Invoke(dialog, new object[] { target });
        HarmonyLib
            .AccessTools.Method(typeof(Dialog_MapSearch), "TryAddElement")
            .Invoke(dialog, new object[] { target });
        var results = (System.Collections.Generic.HashSet<Thing>)
            HarmonyLib
                .AccessTools.Field(typeof(Dialog_Search<Thing>), "searchResultsSet")
                .GetValue(dialog);
        return new
        {
            targetID,
            pawnID,
            nativeEligible = eligible,
            nativeResultContainsTarget = results.Contains(target),
            policyAllows = Presentation.MapSearchVisibility.AllowsResult(target),
            canInspect = Presentation.ThingVisibility.CanInspect(target),
            currentSight = Visibility.IsVisible(target),
            observed = target.TryGetComp<CompFog>()?.HideFromPlayer.WasSeenBy(Faction.OfPlayer),
            held = !target.Spawned,
            holderFaction = pawn.Faction?.def.defName,
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
