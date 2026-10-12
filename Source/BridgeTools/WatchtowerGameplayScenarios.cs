using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Core;
using TotalFog.Watchtowers;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class WatchtowerGameplayScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static readonly Dictionary<string, int> drawnPawns = new();
    private static readonly Dictionary<string, object> culledPawns = new();
    private static readonly List<object> loadedTowers = new();

    private static void ObserveTowerLoad(Building building, Map map, bool respawningAfterLoad)
    {
        if (!respawningAfterLoad || building is not Building_Watchtower tower)
            return;
        loadedTowers.Add(
            new
            {
                tower.ThingID,
                occupant = tower.Occupant?.ThingID,
                corpse = tower.Occupant?.Corpse?.ThingID,
                corpseCell = tower.Occupant?.Corpse?.Position.ToString(),
                collisions = tower
                    .OccupiedRect()
                    .SelectMany(c => c.GetThingList(map))
                    .Distinct()
                    .Select(t => new
                    {
                        t.ThingID,
                        type = t.GetType().FullName,
                        category = t.def.category.ToString(),
                        cell = t.Position.ToString(),
                        wipes = GenSpawn.SpawningWipes(tower.def, t.def),
                    })
                    .ToArray(),
            }
        );
    }

    private static void ObservePawnDraw(Pawn __instance, DrawPhase phase, bool __runOriginal)
    {
        if (__runOriginal && phase == DrawPhase.Draw && drawnPawns.ContainsKey(__instance.ThingID))
            drawnPawns[__instance.ThingID]++;
    }

    private static void ObserveCull(
        NativeArray<DynamicDrawManager.ThingCullDetails> details,
        List<Thing> ___drawThings
    )
    {
        for (int i = 0; i < details.Length; i++)
            if (drawnPawns.ContainsKey(___drawThings[i].ThingID))
                culledPawns[___drawThings[i].ThingID] = new
                {
                    details[i].shouldDraw,
                    cell = details[i].cell.ToString(),
                };
    }

    [Tool(
        "totalfog/watchtower_gameplay",
        Description = "Run production tower access, interruptions, sight and native projectile controls in the paused named fixture. Capture all rotations and restore the unchanged save. Reject a connected Multiplayer session."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel,
        bool performanceSample = false
    )
    {
        if (
            string.IsNullOrEmpty(saveName)
            || string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Supply a save and plain evidence label.");
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_gameplay", evidenceLabel);
        proof.saveName = saveName;
        var owned = new List<Thing>();
        var towers = new List<Building_Watchtower>();
        var pawns = new List<Pawn>();
        var states = new List<object>();
        var performance = new List<object>();
        var savedFixtures = new List<string>();
        var renderProbe = new Harmony("brrainz.totalfog.watchtower-gameplay-probe");
        Map map = null;
        CellRect area = default;
        Pawn helper = null,
            enemy = null;
        Building_Bed bed = null;
        var oldShooting = FogSettings.TowerShootingAllowed;
        var oldHighlight = Prefs.DotHighlightDisplayMode;
        bool oldPauseOnLoad = Prefs.PauseOnLoad;
        bool? oldUltraSpeedBoost = null;
        bool staged = false;
        loadedTowers.Clear();
        try
        {
            await On(() =>
            {
                renderProbe.Patch(
                    AccessTools.Method(typeof(GenSpawn), nameof(GenSpawn.SpawnBuildingAsPossible)),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerGameplayScenarios),
                        nameof(ObserveTowerLoad)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                map = Find.CurrentMap;
                if (
                    map?.GetVisibility()?.Initialized != true
                    || !Find.TickManager.Paused
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use a paused initialized single-player fixture."
                    );
                var origin = GenRadial
                    .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                    .First(c =>
                        new CellRect(c.x, c.z, 28, 24).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !p.Roofed(map)
                            && !p.Fogged(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                area = new CellRect(origin.x, origin.z, 28, 24);
                foreach (var cell in area)
                foreach (var thing in cell.GetThingList(map).ToArray())
                    thing.Destroy(DestroyMode.Vanish);
                staged = true;
                FogSettings.TowerShootingAllowed = false;
                Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.None;
                Prefs.PauseOnLoad = true;
                Find.Selector.ClearSelection();
                Rand.PushState(731009);
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var tower = (Building_Watchtower)
                            ThingMaker.MakeThing(
                                WatchtowerDefOf.TotalFog_Watchtower,
                                i % 2 == 0
                                    ? ThingDefOf.WoodLog
                                    : DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                            );
                        owned.Add(tower);
                        tower.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(
                            tower,
                            new IntVec3(area.minX + 3 + i * 6, 0, area.minZ + 8),
                            map,
                            new Rot4(i)
                        );
                        towers.Add(tower);
                        pawns.Add(
                            AddPawn(
                                tower.Approach + tower.Rotation.FacingCell * 2,
                                Faction.OfPlayer
                            )
                        );
                    }
                    helper = AddPawn(
                        new IntVec3(area.minX + 12, 0, area.minZ + 2),
                        Faction.OfPlayer
                    );
                    enemy = AddPawn(
                        towers[0].Position + new IntVec3(0, 0, 12),
                        Faction.OfAncientsHostile
                    );
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 10000));
                }
                finally
                {
                    Rand.PopState();
                }
                Check(
                    "four native solid footprints",
                    towers.All(t =>
                        t.OccupiedRect().Count() == 9
                        && t.OccupiedRect().All(c => c.GetEdifice(map) == t && !c.Walkable(map))
                    )
                );
                Check(
                    "native breach predicates",
                    towers.All(t =>
                        BreachingUtility.ShouldBreachBuilding(t)
                        && BreachingUtility.BlocksBreaching(map, t.Position)
                    )
                );
                var wall = AddWall(towers[0].Approach);
                Check("blocked ladder refuses entry", !towers[0].CanEnter(pawns[0]));
                wall.Destroy(DestroyMode.Vanish);
                if (performanceSample)
                {
                    // The base regression save contains active combat. A quiet
                    // native control is required to measure ordinary fourth speed
                    // without overriding the engine's combat slowdown policy.
                    foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToArray())
                    {
                        if (pawn.Faction != null && pawn.HostileTo(Faction.OfPlayer))
                            pawn.SetFaction(Faction.OfPlayer);
                        if (pawn.drafter != null && pawn.Faction == Faction.OfPlayer)
                            pawn.drafter.Drafted = true;
                        pawn.stances.CancelBusyStanceHard();
                        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                    }
                    states.Add(
                        new
                        {
                            phase = "quiet-performance-control",
                            pawns = map.mapPawns.AllPawnsSpawned.Count,
                        }
                    );
                }
                renderProbe.Patch(
                    AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerGameplayScenarios),
                        nameof(ObservePawnDraw)
                    )
                );
                renderProbe.Patch(
                    AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerGameplayScenarios),
                        nameof(ObserveCull)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
            });
            if (performanceSample)
            {
                var speedMode = await ctx.Tools.CallAsync(
                    "rimworld/set_time_speed",
                    new { speed = "Paused", ultraSpeedBoost = false },
                    cancellationToken: cancellationToken
                );
                Check(
                    "ordinary player speed mode",
                    speedMode.Succeeded() && !speedMode.ReadResult<bool>("currentUltraSpeedBoost")
                );
                oldUltraSpeedBoost = speedMode.ReadResult<bool>("previousUltraSpeedBoost");
                for (int ticks = 0; ticks < 850; ticks += 100)
                    await Step(Math.Min(100, 850 - ticks));
                await On(() =>
                    Check(
                        "native combat slowdown expires before timing",
                        !AccessTools
                            .FieldRefAccess<TickManager, TimeSlower>("slower")(Find.TickManager)
                            .ForcedNormalSpeed
                    )
                );
                await Capture("ground-performance-camera", 12);
                await Measure("ground-normal", false);
                await Measure("ground-wide", true);
            }
            await On(() =>
            {
                for (int i = 0; i < 4; i++)
                    StartWatch(pawns[i], towers[i]);
            });
            await Until(
                "all four begin ascent",
                () => towers.All(t => t.Occupant != null && t.Elevation > 0 && t.Elevation < 1)
            );
            await Step(30);
            await On(() =>
            {
                Check(
                    "ascent keeps logical ladder approach",
                    towers
                        .Select(
                            (t, i) =>
                                pawns[i].Position == t.Approach
                                && t.Elevation > .1f
                                && t.Elevation < .7f
                                && pawns[i].Rotation == t.Rotation.Opposite
                        )
                        .All(v => v)
                );
                Record("ascent");
            });
            await Capture("climbing", 12);
            await Reload("ascent");
            await Capture("climbing-loaded", 12);
            await Until("all four occupy platform", () => towers.All(t => t.Watching));
            await On(() =>
            {
                Check(
                    "one spawned registered pawn per platform",
                    towers
                        .Select(
                            (t, i) =>
                                pawns[i].Spawned
                                && pawns[i].Position == t.Position
                                && map.thingGrid.ThingsListAt(t.Position).Contains(pawns[i])
                                && t.Occupant == pawns[i]
                                && t.Elevation == 1
                        )
                        .All(v => v)
                );
                Check(
                    "native DrawPos stays in ground coordinates outside rendering",
                    pawns.All(p =>
                        (p.DrawPos - p.Position.ToVector3Shifted()).MagnitudeHorizontal() < .01f
                    )
                );
                Check(
                    "all four projection offsets",
                    pawns.All(p =>
                        Math.Abs(TowerPresentation.Offset(p).z - 1.65f) < .001f
                        && Math.Abs(TowerPresentation.Offset(p).x) < .001f
                    )
                );
                Check(
                    "occupied towers cannot be deconstructed",
                    !new WorkGiver_Deconstruct().HasJobOnThing(helper, towers[0], true)
                );
                Check("other pawn cannot take occupied platform", !towers[0].CanEnter(helper));
                Find.Selector.Select(pawns[0]);
                Record("platform");
            });
            await Capture("occupied", 12);
            await Capture("occupied-wide", 35);
            if (performanceSample)
            {
                await Measure("occupied-wide", true);
                await Capture("occupied-performance-camera", 12);
                await Measure("occupied-normal", false);
                await On(() =>
                {
                    enemy.SetFaction(Faction.OfAncientsHostile);
                    Building_Watchtower.Move(enemy, towers[0].Position + IntVec3.North * 12);
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                });
            }
            var cameraRoot = await ctx.MainThread.InvokeAsync(
                () => Find.CameraDriver.MapPosition.ToVector3Shifted(),
                cancellationToken
            );
            var cameraSize = await ctx.MainThread.InvokeAsync(
                () => Find.CameraDriver.RootSize,
                cancellationToken
            );
            try
            {
                var root = new Vector3(pawns[0].Position.x + .5f, 0, pawns[0].Position.z + 14);
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    await On(() => Find.CameraDriver.SetRootPosAndSize(root, 12f));
                    await ctx.Game.FramesAsync(4, cancellationToken);
                    int minZ = await ctx.MainThread.InvokeAsync(
                        () => Find.CameraDriver.CurrentViewRect.minZ,
                        cancellationToken
                    );
                    int delta = pawns[0].Position.z + 2 - minZ;
                    if (delta == 0)
                        break;
                    root.z += delta;
                }
                await On(() =>
                {
                    var view = Find.CameraDriver.CurrentViewRect;
                    var culling = view.ExpandedBy(1);
                    Check(
                        "camera edge excludes native pawn bounds",
                        !pawns[0].OccupiedDrawRect().Overlaps(culling)
                    );
                    Check(
                        "camera edge includes raised pawn bounds",
                        TowerPresentation.CullBounds(pawns[0]).Overlaps(culling)
                    );
                    states.Add(
                        new
                        {
                            phase = "camera-edge",
                            view = view.ToString(),
                            nativeBounds = pawns[0].OccupiedDrawRect().ToString(),
                            raisedBounds = TowerPresentation.CullBounds(pawns[0]).ToString(),
                        }
                    );
                    drawnPawns[pawns[0].ThingID] = 0;
                });
                await ctx.Game.FramesAsync(6, cancellationToken);
                await On(() =>
                    Check(
                        "actual raised pawn draws beyond native camera bounds",
                        drawnPawns[pawns[0].ThingID] > 0
                    )
                );
                var edge = await ctx.Tools.CallAsync(
                    "rimworld/take_screenshot",
                    new { fileName = evidenceLabel + "-camera-edge", includeTargets = false },
                    cancellationToken: cancellationToken
                );
                proof.assertions.Add(
                    RimBridgeEvidence.ToolSucceeded("capture actual camera edge", edge)
                );
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = "camera-edge",
                        kind = "native-window",
                        path = edge.TryReadResult<string>(out var edgePath, "path") ? edgePath : "",
                        success = edge.Succeeded(),
                        details = edge.Result,
                    }
                );
            }
            finally
            {
                await ctx.MainThread.InvokeAsync(
                    () => Find.CameraDriver.SetRootPosAndSize(cameraRoot, cameraSize),
                    CancellationToken.None
                );
            }
            await On(() =>
            {
                pawns[0]
                    .jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(
                            JobDefOf.Goto,
                            towers[0].Approach + towers[0].Rotation.FacingCell * 3
                        )
                    );
            });
            await Until(
                "drafted move schedules descent",
                () => pawns[0].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
            );
            await Step(30);
            await On(() =>
            {
                Check(
                    "interruption descends in time, not teleport",
                    pawns[0].Position == towers[0].Position
                        && towers[0].Elevation > .1f
                        && towers[0].Elevation < .95f
                );
                Record("interrupted-descent");
            });
            await Reload("descent");
            await Until(
                "move resumes after descent",
                () => towers[0].Occupant == null && pawns[0].CurJobDef == JobDefOf.Goto
            );
            await On(() => StartWatch(pawns[0], towers[0]));
            await Until("lookout returns", () => towers[0].Watching);
            await On(() =>
            {
                // Exit blocking happens after occupation, not just placement.
                AddWall(towers[3].Approach);
                pawns[3].jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, helper.Position));
            });
            await Step(40);
            await On(() =>
            {
                Check(
                    "blocked exit waits on platform",
                    towers[3].Elevation == 1
                        && pawns[3].Position == towers[3].Position
                        && pawns[3].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
                );
                towers[3].Approach.GetEdifice(map).Destroy(DestroyMode.Vanish);
            });
            await Until("cleared exit resumes timed descent", () => towers[3].Occupant == null);
            await On(() => StartWatch(pawns[3], towers[3]));
            await Until(
                "partial climb available",
                () => towers[3].Elevation > .25f && towers[3].Elevation < .8f
            );
            await On(() =>
                pawns[3].jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, helper.Position))
            );
            await Step(10);
            await On(() =>
                Check(
                    "partial climb reverses through native descent",
                    pawns[3].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
                        && towers[3].Elevation > 0
                        && towers[3].Elevation < .8f
                        && pawns[3].Position == towers[3].Approach
                )
            );
            await Until("partial climb safely released", () => towers[3].Occupant == null);
            await On(() => StartWatch(pawns[3], towers[3]));
            await Until("recovery control occupied", () => towers[3].Watching);
            await On(() => HealthUtility.DamageUntilDowned(pawns[3]));
            await Step(5);
            await On(() =>
            {
                foreach (
                    var injury in pawns[3]
                        .health.hediffSet.hediffs.OfType<Hediff_Injury>()
                        .ToArray()
                )
                    pawns[3].health.RemoveHediff(injury);
                Check(
                    "downed lookout recovers on platform",
                    !pawns[3].Downed && towers[3].Occupant == pawns[3]
                );
                pawns[3].jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, helper.Position));
            });
            await Step(10);
            await On(() =>
                Check(
                    "recovered native job still descends",
                    pawns[3].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
                        && towers[3].Elevation > 0
                )
            );
            await Until("recovered lookout reaches ground", () => towers[3].Occupant == null);
            await On(() => StartWatch(pawns[3], towers[3]));
            await Until("corpse control occupied", () => towers[3].Watching);
            await On(() =>
            {
                var source = pawns[0].TryGetComp<CompFog>().FieldOfViewWatcher;
                states.Add(
                    new
                    {
                        phase = "sight-control",
                        target = enemy.Position.ToString(),
                        watching = towers[0].Watching,
                        occupant = towers[0].Occupant?.ThingID,
                        enemyJob = enemy.CurJobDef?.defName,
                        drawingDepth = TowerPresentation.DrawingDepth,
                    }
                );
                for (int x = -2; x <= 2; x++)
                    AddWall(towers[0].Position + new IntVec3(x, 0, 5));
                source.UpdateFoV(true);
                Check("elevated sight crosses freestanding wall", Sees(source, enemy.Position));
                var roofCell = towers[0].Position + new IntVec3(0, 0, 7);
                map.roofGrid.SetRoof(roofCell, RoofDefOf.RoofConstructed);
                source.UpdateFoV(true);
                Check("roof cell is not revealed", !Sees(source, roofCell));
                Check("room roof blocks beyond", !Sees(source, enemy.Position));
                map.roofGrid.SetRoof(roofCell, null);
                source.UpdateFoV(true);
                Check("roof removal refreshes elevated sight", Sees(source, enemy.Position));
                var rifle = (ThingWithComps)
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
                pawns[0].equipment.AddEquipment(rifle);
                var verb = pawns[0].TryGetAttackVerb(enemy);
                Check("shooting disabled by default", !verb.TryStartCastOn(enemy));
                FogSettings.TowerShootingAllowed = true;
                Check("native elevated target eligibility", verb.CanHitTarget(enemy));
                var projectile = (Projectile)ThingMaker.MakeThing(verb.verbProps.defaultProjectile);
                owned.Add(projectile);
                GenSpawn.Spawn(projectile, pawns[0].Position, map);
                projectile.Launch(pawns[0], pawns[0].DrawPos, enemy, enemy, ProjectileHitFlags.All);
                Record("outgoing-shot");
            });
            float before = await ctx.MainThread.InvokeAsync(
                () => enemy.health.summaryHealth.SummaryHealthPercent,
                cancellationToken
            );
            await Step(3);
            await Reload("bullet-flight");
            await Step(60);
            await On(() =>
                Check(
                    "native outgoing bullet crosses wall and damages ground pawn",
                    enemy.Dead || enemy.health.summaryHealth.SummaryHealthPercent < before
                )
            );
            await On(() =>
            {
                var verb = pawns[0].TryGetAttackVerb(enemy);
                var projectile = (Projectile)ThingMaker.MakeThing(verb.verbProps.defaultProjectile);
                owned.Add(projectile);
                GenSpawn.Spawn(projectile, enemy.Position, map);
                projectile.Launch(enemy, enemy.DrawPos, pawns[0], pawns[0], ProjectileHitFlags.All);
            });
            before = await ctx.MainThread.InvokeAsync(
                () => pawns[0].health.summaryHealth.SummaryHealthPercent,
                cancellationToken
            );
            await Step(60);
            await On(() =>
                Check(
                    "native incoming bullet crosses wall and damages lookout",
                    pawns[0].Dead || pawns[0].health.summaryHealth.SummaryHealthPercent < before
                )
            );
            await On(() =>
            {
                FogSettings.TowerShootingAllowed = false;
                // Use an untouched lookout for incapacity/rescue controls.
                HealthUtility.DamageUntilDowned(pawns[1]);
                Check(
                    "downed lookout stays rescuable at top",
                    pawns[1].Downed
                        && !pawns[1].Dead
                        && pawns[1].Position == towers[1].Position
                        && towers[1].Occupant == pawns[1]
                );
            });
            await Step(5); // Native downing replaces the watch job and releases its reservation.
            await On(() =>
            {
                var workgiver = new WorkGiver_RescueDowned();
                // Introduce this fixture's bed immediately before its rescue.
                // Earlier injuries must not let another native patient reserve
                // it before the control starts; no bed validator is bypassed.
                bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
                owned.Add(bed);
                bed.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(bed, new IntVec3(area.minX + 17, 0, area.minZ + 2), map);
                bed.Medical = true;
                states.Add(
                    new
                    {
                        phase = "rescue-eligibility",
                        helper.Spawned,
                        cell = helper.Position.ToString(),
                        helper.Downed,
                        helper.Dead,
                        towerEntry = towers[1].CanEnter(helper, pawns[1]),
                        reservePatient = helper.CanReserve(pawns[1]),
                        reserveTower = helper.CanReserve(towers[1]),
                        patientDowned = pawns[1].Downed,
                        patientDead = pawns[1].Dead,
                        bedMedical = bed.Medical,
                        bedSpawned = bed.Spawned,
                        bedForbidden = bed.IsForbidden(helper),
                        bedReachable = helper.CanReach(bed, PathEndMode.OnCell, Danger.Some),
                        bedReservable = helper.CanReserve(bed),
                        bedUsable = RestUtility.CanUseBedNow(bed, pawns[1], false),
                        bedValid = RestUtility.IsValidBedFor(bed, pawns[1], helper, false),
                        medicalRest = HealthAIUtility.ShouldSeekMedicalRest(pawns[1]),
                        bedFound = RestUtility
                            .FindBedFor(pawns[1], helper, checkSocialProperness: false)
                            ?.ThingID,
                    }
                );
                Check(
                    "native rescue discovers ladder route",
                    workgiver.HasJobOnThing(helper, pawns[1], true)
                );
                var rescue = workgiver.JobOnThing(helper, pawns[1], true);
                Check(
                    "native rescue uses tower retrieval job",
                    rescue.def == WatchtowerDefOf.TotalFog_WatchtowerRetrieve
                );
                helper.jobs.TryTakeOrderedJob(rescue);
                Check(
                    "ladder rescue reserves its native destination bed",
                    !pawns[2].CanReserve(bed)
                );
            });
            await Until(
                "rescuer partly climbed",
                () => towers[1].Rescuer == helper && towers[1].RescuerElevation > .25f
            );
            await Reload("rescuer-ascent");
            await On(() =>
                helper.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, towers[0].Approach))
            );
            await Step(10);
            await On(() =>
                Check(
                    "interrupted rescuer descends while patient remains on top",
                    helper.CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
                        && towers[1].Occupant == pawns[1]
                        && pawns[1].Position == towers[1].Position
                        && towers[1].Rescuer == helper
                        && towers[1].RescuerElevation > 0
                )
            );
            await Until("rescuer relinquishes temporary slot", () => towers[1].Rescuer == null);
            await On(() =>
                helper.jobs.TryTakeOrderedJob(
                    new WorkGiver_RescueDowned().JobOnThing(helper, pawns[1], true)
                )
            );
            await Until(
                "rescuer carrying patient on ladder",
                () =>
                    helper.carryTracker.CarriedThing == pawns[1]
                    && towers[1].Elevation > .5f
                    && towers[1].Elevation < .95f
            );
            await Reload("carrying-rescue");
            await Until("ladder rescue completes native bed handoff", () => pawns[1].InBed(), 4000);
            await On(() =>
            {
                Check(
                    "one rescued pawn in bed",
                    pawns[1].CurrentBed() == bed
                        && towers[1].Occupant == null
                        && helper.carryTracker.CarriedThing == null
                );
            });
            await On(() =>
            {
                pawns[3].Kill(null);
                Check(
                    "death retains exactly one native corpse on deck",
                    towers[3].Occupant == pawns[3]
                        && pawns[3].Corpse?.Spawned == true
                        && pawns[3].Corpse.Position == towers[3].Position
                );
                Check(
                    "corpse uses deck projection",
                    Math.Abs(TowerPresentation.Offset(pawns[3].Corpse).z - 1.02f) < .001f
                );
                var click = pawns[3].Corpse.DrawPos + TowerPresentation.Offset(pawns[3].Corpse);
                Check(
                    "projected corpse can be selected",
                    GenUI
                        .ThingsUnderMouse(
                            click,
                            .4f,
                            new TargetingParameters
                            {
                                mustBeSelectable = true,
                                canTargetCorpses = true,
                                canTargetItems = true,
                            }
                        )
                        .Contains(pawns[3].Corpse)
                );
            });
            await Reload("corpse");
            await On(() =>
            {
                var retrieve = JobMaker.MakeJob(
                    WatchtowerDefOf.TotalFog_WatchtowerRetrieve,
                    towers[3]
                );
                retrieve.targetC = pawns[3].Corpse;
                helper.jobs.TryTakeOrderedJob(retrieve);
            });
            await Until(
                "corpse retrieval finishes on ground",
                () =>
                    towers[3].Occupant == null
                    && pawns[3].Corpse.Spawned
                    && pawns[3].Corpse.Position.Walkable(map)
                    && helper.carryTracker.CarriedThing == null,
                3000
            );
            await On(() =>
            {
                var victim = pawns[2];
                towers[2].Destroy(DestroyMode.KillFinalize);
                Check(
                    "collapse ejects pawn and removes tower ownership",
                    !towers[2].Spawned
                        && TowerPresentation.TowerFor(victim) == null
                        && (victim.Dead || victim.Position.Walkable(map))
                );
                Check(
                    "collapse inflicts native injury",
                    victim.Dead || victim.health.hediffSet.hediffs.Any(h => h is Hediff_Injury)
                );
                Record("rescue-collapse");
            });

            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, faction);
                owned.Add(p);
                GenSpawn.Spawn(p, cell, map);
                if (p.drafter != null)
                    p.drafter.Drafted = true;
                return p;
            }
            async Task Measure(string label, bool wide)
            {
                var result = await PerformanceScenarios.RuntimePerformance(
                    ctx,
                    cancellationToken,
                    durationMs: 4000,
                    speed: "Ultrafast",
                    forceRequestedSpeed: false,
                    profileFog: false,
                    warmupTicks: 60,
                    wideView: wide
                );
                performance.Add(new { label, result });
                Check(
                    "native performance sample " + label,
                    (bool)result.GetType().GetProperty("success").GetValue(result)
                );
            }
            Thing AddWall(IntVec3 cell)
            {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                owned.Add(wall);
                GenSpawn.Spawn(wall, cell, map);
                return wall;
            }
            void Check(string name, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }
            void Record(string phase)
            {
                states.Add(
                    new
                    {
                        phase,
                        tick = Find.TickManager.TicksGame,
                        towers = towers
                            .Select(t => new
                            {
                                t.ThingID,
                                t.Spawned,
                                elevation = t.Elevation,
                                watching = t.Watching,
                                occupant = t.Occupant?.ThingID,
                                rescuer = t.Rescuer?.ThingID,
                                rescuerElevation = t.RescuerElevation,
                                cell = t.Position.ToString(),
                                approach = t.Spawned ? t.Approach.ToString() : null,
                                sightRange = t
                                    .Occupant?.TryGetComp<CompFog>()
                                    ?.FieldOfViewWatcher?.LastSightRange,
                                nativeDraw = t.Occupant?.DrawPos.ToString(),
                                visualOffset = t.Occupant == null
                                    ? null
                                    : TowerPresentation.Offset(t.Occupant).ToString(),
                            })
                            .ToArray(),
                    }
                );
                proof.environment.details = new
                {
                    performance,
                    productionTower = true,
                    area = area.ToString(),
                    states,
                };
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            async Task Reload(string label)
            {
                var fixture = "TotalFog_Tower_" + evidenceLabel + "_" + label;
                string path = null;
                string[] towerIds = null,
                    pawnIds = null,
                    ownedIds = null;
                string helperId = null,
                    enemyId = null,
                    bedId = null;
                float[] elevations = null,
                    rescuerElevations = null;
                int tick = 0;
                await On(() =>
                {
                    path = GenFilePaths.FilePathForSavedGame(fixture);
                    if (File.Exists(path))
                        throw new InvalidOperationException("Tower fixture save already exists.");
                    towerIds = towers.Select(t => t.ThingID).ToArray();
                    pawnIds = pawns.Select(p => p.ThingID).ToArray();
                    ownedIds = owned.Select(t => t.ThingID).ToArray();
                    helperId = helper.ThingID;
                    enemyId = enemy.ThingID;
                    bedId = bed?.ThingID;
                    elevations = towers.Select(t => t.Elevation).ToArray();
                    tick = Find.TickManager.TicksGame;
                    Record("before-save-" + label);
                });
                savedFixtures.Add(path);
                var saved = await ctx.Tools.CallAsync(
                    "rimworld/save_game",
                    new { saveName = fixture },
                    cancellationToken: cancellationToken
                );
                Check("save " + label, saved.Succeeded() && File.Exists(path));
                // Native PauseOnLoad deliberately executes one tick before
                // pausing. Compare with that same uninterrupted reference tick.
                await Step(1);
                await On(() =>
                {
                    elevations = towers.Select(t => t.Elevation).ToArray();
                    rescuerElevations = towers.Select(t => t.RescuerElevation).ToArray();
                });
                var loaded = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName = fixture,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: cancellationToken
                );
                if (!loaded.Succeeded())
                    throw new InvalidOperationException("Load tower " + label);
                await On(() =>
                {
                    states.Add(
                        new { phase = "load-collisions-" + label, towers = loadedTowers.ToArray() }
                    );
                    loadedTowers.Clear();
                    map = Find.CurrentMap;
                    var things = map.listerThings.AllThings.ToDictionary(t => t.ThingID);
                    foreach (var corpse in map.listerThings.AllThings.OfType<Corpse>())
                        things[corpse.InnerPawn.ThingID] = corpse.InnerPawn;
                    foreach (var p in map.mapPawns.AllPawnsSpawned)
                        if (p.carryTracker.CarriedThing is Thing carried)
                            things[carried.ThingID] = carried;
                    towers.Clear();
                    towers.AddRange(towerIds.Select(id => (Building_Watchtower)things[id]));
                    pawns.Clear();
                    pawns.AddRange(pawnIds.Select(id => (Pawn)things[id]));
                    owned.Clear();
                    owned.AddRange(ownedIds.Where(things.ContainsKey).Select(id => things[id]));
                    helper = (Pawn)things[helperId];
                    enemy = (Pawn)things[enemyId];
                    bed = bedId == null ? null : (Building_Bed)things[bedId];
                    Record("loaded-" + label);
                    Check(
                        "reload paused at saved tick " + label,
                        Find.TickManager.Paused && Find.TickManager.TicksGame == tick + 1
                    );
                    Check(
                        "reload retains elevation and ownership " + label,
                        towers
                            .Select(
                                (t, i) =>
                                    Math.Abs(t.Elevation - elevations[i]) < .00001f
                                    && Math.Abs(t.RescuerElevation - rescuerElevations[i]) < .00001f
                                    && (
                                        t.Occupant == null
                                        || TowerPresentation.TowerFor(t.OccupantBody) == t
                                    )
                                    && (
                                        t.Rescuer == null
                                        || TowerPresentation.TowerFor(t.Rescuer) == t
                                    )
                            )
                            .All(v => v)
                    );
                    if (label == "bullet-flight")
                    {
                        var bullet = owned.OfType<Projectile>().Single(p => p.Spawned);
                        var native = bullet.ExactPosition;
                        var drawn = native;
                        TowerCombat.DrawPrefix(bullet, ref drawn);
                        Check(
                            "reload retains raised bullet drawing without shifting collision",
                            drawn.z > native.z + .1f && bullet.ExactPosition == native
                        );
                    }
                    Record("after-load-" + label);
                });
            }
            async Task Step(int ticks)
            {
                var result = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!result.Success)
                    throw new InvalidOperationException("Tick stepping: " + result.Status);
            }
            async Task Until(string label, Func<bool> done, int limit = 1500)
            {
                for (int elapsed = 0; elapsed <= limit; elapsed += 10)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (elapsed < limit)
                        await Step(10);
                }
                await On(() => Record("failed-" + label));
                throw new InvalidOperationException("Timed out: " + label);
            }
            async Task Capture(string label, float zoom)
            {
                await On(() =>
                {
                    drawnPawns.Clear();
                    culledPawns.Clear();
                    foreach (var p in pawns)
                        drawnPawns[p.ThingID] = 0;
                });
                var frame = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ + 3,
                        width = 28,
                        height = 12,
                        rootSize = zoom,
                    },
                    cancellationToken: cancellationToken
                );
                if (!frame.Succeeded())
                    throw new InvalidOperationException("Frame " + label);
                await ctx.Game.FramesAsync(12, cancellationToken);
                // Fog fading advances on game ticks. Fast tick stepping with no
                // rendered frames followed by a pause otherwise freezes the old
                // opaque mesh, even though native bodies are correctly submitted.
                await Step(14);
                await ctx.Game.FramesAsync(12, cancellationToken);
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ + 3,
                        width = 28,
                        height = 12,
                        rootSize = zoom,
                        paddingCells = 0,
                        fileName = evidenceLabel + "-" + label,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                proof.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + label, shot));
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = label,
                        kind = "cell_rect",
                        path = shot.TryReadResult<string>(out var path, "path") ? path : "",
                        success = shot.Succeeded(),
                        details = shot.Result,
                    }
                );
                if (!shot.Succeeded())
                    throw new InvalidOperationException("Capture " + label);
                await On(() =>
                {
                    states.Add(
                        new
                        {
                            phase = "drawing-" + label,
                            draws = drawnPawns.ToArray(),
                            cull = culledPawns.ToArray(),
                        }
                    );
                    Check("native pawn bodies drawn " + label, drawnPawns.Values.All(n => n > 0));
                });
            }
        }
        catch (Exception exception)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = exception.ToString() });
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        renderProbe.UnpatchAll(renderProbe.Id);
                        drawnPawns.Clear();
                        culledPawns.Clear();
                        // Reload abandons the owned fixture as a whole. References
                        // from earlier reloads can still report Spawned on maps
                        // whose runtime grids have already been disposed.
                        FogSettings.TowerShootingAllowed = oldShooting;
                        Prefs.DotHighlightDisplayMode = oldHighlight;
                        Prefs.PauseOnLoad = oldPauseOnLoad;
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
                foreach (var fixture in savedFixtures)
                    if (File.Exists(fixture))
                        File.Delete(fixture);
            }
            finally
            {
                try
                {
                    if (oldUltraSpeedBoost.HasValue)
                    {
                        var restored = await ctx.Tools.CallAsync(
                            "rimworld/set_time_speed",
                            new { speed = "Paused", ultraSpeedBoost = oldUltraSpeedBoost.Value },
                            cancellationToken: CancellationToken.None
                        );
                        proof.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "restore prior speed mode",
                                restored.Succeeded()
                                    && restored.ReadResult<bool>("currentUltraSpeedBoost")
                                        == oldUltraSpeedBoost.Value
                            )
                        );
                    }
                }
                finally
                {
                    gate.Release();
                }
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;
    }

    private static void StartWatch(Pawn pawn, Building_Watchtower tower) =>
        pawn.jobs.TryTakeOrderedJob(
            JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
        );

    internal static bool Sees(CompSightSource source, IntVec3 cell) =>
        (
            (VisibilityMask)AccessTools.Field(typeof(CompSightSource), "current").GetValue(source)
        ).Contains(source.parent.Map.cellIndices.CellToIndex(cell));
}
