using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TotalFog.BridgeTools;

/// <summary>Native blueprint/frame, construction and breach-job boundary controls.</summary>
public sealed class WatchtowerBuildingScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/watchtower_building",
        Description = "Complete a real tower frame with a native construction job, observe adjacent wall cache invalidation and run the native breaching job giver with a staged target. Materials are prefilled; this is not hauling or autonomous raid coverage. Restore the unchanged paused named fixture."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (
            string.IsNullOrEmpty(saveName)
            || string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Supply a save and plain evidence label.");
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_building", evidenceLabel);
        proof.saveName = saveName;
        var states = new List<object>();
        var oldPause = Prefs.PauseOnLoad;
        bool staged = false;
        Map map = null;
        IntVec3 origin = default;
        Pawn worker = null,
            breacher = null;
        Frame frame = null;
        Building_Watchtower tower = null;
        try
        {
            await On(() =>
            {
                map = Find.CurrentMap;
                if (
                    map?.GetVisibility()?.Initialized != true
                    || !Find.TickManager.Paused
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use a paused initialized single-player fixture."
                    );
                origin = GenRadial
                    .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                    .First(c =>
                        new CellRect(c.x - 6, c.z - 6, 13, 13).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !p.Roofed(map)
                            && !p.Fogged(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                foreach (var cell in new CellRect(origin.x - 6, origin.z - 6, 13, 13))
                foreach (var thing in cell.GetThingList(map).ToArray())
                    thing.Destroy(DestroyMode.Vanish);
                staged = true;
                Prefs.PauseOnLoad = true;
                Rand.PushState(941012);
                try
                {
                    worker = AddPawn(origin + IntVec3.South * 3, Faction.OfPlayer);
                    worker.skills.GetSkill(SkillDefOf.Construction).Level = 20;
                    breacher = AddPawn(origin + IntVec3.East * 5, Faction.OfAncientsHostile);
                }
                finally
                {
                    Rand.PopState();
                }
                Check(
                    "worker supports native construction",
                    !worker.WorkTypeIsDisabled(WorkTypeDefOf.Construction)
                );
                worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                breacher.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                var blueprint = GenConstruct.PlaceBlueprintForBuild(
                    WatchtowerDefOf.TotalFog_Watchtower,
                    origin,
                    map,
                    Rot4.South,
                    Faction.OfPlayer,
                    ThingDefOf.WoodLog
                );
                Check(
                    "native blueprint owns full rotated footprint",
                    blueprint.OccupiedRect().Count() == 9 && blueprint.Rotation == Rot4.South
                );
                Check(
                    "native blueprint becomes frame",
                    blueprint.TryReplaceWithSolidThing(worker, out var created, out var ended)
                        && !ended
                        && created is Frame
                );
                frame = (Frame)created;
                foreach (var cost in frame.TotalMaterialCost())
                {
                    var material = ThingMaker.MakeThing(cost.thingDef);
                    material.stackCount = cost.count;
                    Check(
                        "native frame accepts material " + cost.thingDef.defName,
                        frame.resourceContainer.TryAdd(material)
                    );
                }
                Check(
                    "frame has full native material requirement",
                    frame.IsCompleted() && frame.ThingCountNeeded(ThingDefOf.WoodLog) == 0
                );
                var job = new WorkGiver_ConstructFinishFrames().JobOnThing(worker, frame, true);
                Check(
                    "native work giver supplies construction job",
                    job?.def == JobDefOf.FinishFrame
                );
                worker.jobs.TryTakeOrderedJob(job);
                states.Add(
                    new
                    {
                        phase = "frame",
                        frame.WorkToBuild,
                        resources = frame.resourceContainer.TotalStackCount,
                    }
                );
            });
            await Until(() => frame.workDone > 0, "native worker starts frame", 250);
            await On(() =>
                Check(
                    "native worker paths to adjacent construction cell",
                    !frame.OccupiedRect().Contains(worker.Position)
                )
            );
            await Until(
                () => origin.GetEdifice(map) is Building_Watchtower || frame.Destroyed,
                "native frame completes",
                3500
            );
            await On(() =>
            {
                tower = origin.GetEdifice(map) as Building_Watchtower;
                Check(
                    "native job completes player tower",
                    tower?.Spawned == true
                        && tower.Faction == Faction.OfPlayer
                        && tower.Stuff == ThingDefOf.WoodLog
                );
                Check(
                    "completed frame registers all solid cells",
                    tower.OccupiedRect().All(c => c.GetEdifice(map) == tower && !c.Walkable(map))
                );
                Check(
                    "completed tower preserves ladder rotation",
                    tower.Rotation == Rot4.South && tower.Approach == origin + IntVec3.South * 2
                );
                Check(
                    "realtime tower has no native mesh link drawer",
                    tower.def.drawerType == DrawerType.RealtimeOnly && !tower.def.graphicData.Linked
                );
                Check(
                    "completed tower registers native wall links across its footprint",
                    tower
                        .OccupiedRect()
                        .All(c => (map.linkGrid.LinkFlagsAt(c) & LinkFlags.Wall) != 0)
                );
                worker.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
            });
            var walls = new List<Thing>();
            await On(() =>
            {
                foreach (var direction in new[] { IntVec3.East, IntVec3.West })
                {
                    var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                    wall.SetFaction(Faction.OfPlayer);
                    walls.Add(GenSpawn.Spawn(wall, origin + direction * 2, map));
                    Check(
                        "adjacent wall links into realtime tower " + direction,
                        wall.Graphic is Graphic_Linked linked
                            && linked.ShouldLinkWith(wall.Position - direction, wall)
                    );
                }
            });
            await ctx.Game.FramesAsync(5, cancellationToken);
            await On(() =>
                Check("native wall spawns rebuild both cached joins", tower.WallJoinCount == 2)
            );
            await On(() => walls[0].Destroy(DestroyMode.Vanish));
            await ctx.Game.FramesAsync(5, cancellationToken);
            await On(() =>
                Check("native wall removal clears one cached join", tower.WallJoinCount == 1)
            );
            await On(() => walls[1].Destroy(DestroyMode.Vanish));
            await ctx.Game.FramesAsync(5, cancellationToken);
            int before = await ctx.MainThread.InvokeAsync(() => tower.HitPoints, cancellationToken);
            await On(() =>
            {
                Check(
                    "all removed walls restore plain cached drawing",
                    tower.WallJoinCount == 0 && tower.CachedDrawCalls == 2
                );
                breacher.equipment.AddEquipment(
                    (ThingWithComps)
                        ThingMaker.MakeThing(
                            DefDatabase<ThingDef>.GetNamed("MeleeWeapon_BreachAxe"),
                            ThingDefOf.Steel
                        )
                );
                // Native breach lookup requires a lord. A stationary lord keeps
                // this a supplied-target job control, not autonomous raid proof.
                LordMaker.MakeNewLord(
                    Faction.OfAncientsHostile,
                    new LordJob_DefendPoint(breacher.Position),
                    map,
                    new[] { breacher }
                );
                breacher.mindState.duty = new PawnDuty(
                    DefDatabase<DutyDef>.AllDefsListForReading.First(d =>
                        d.defName.Contains("Breaching")
                    ),
                    origin + IntVec3.West * 15
                );
                breacher.mindState.breachingTarget = new BreachingTargetData(
                    tower,
                    origin + IntVec3.East * 2
                );
                var give = AccessTools.MethodDelegate<Func<JobGiver_AIBreaching, Pawn, Job>>(
                    AccessTools.Method(typeof(JobGiver_AIBreaching), "TryGiveJob")
                );
                var job = give(new JobGiver_AIBreaching(), breacher);
                Check(
                    "native breaching giver chooses structure attack",
                    job?.def == JobDefOf.AttackMelee
                        && job.targetA.Thing == tower
                        && job.verbToUse != null
                );
                breacher.jobs.StartJob(job);
                states.Add(
                    new
                    {
                        phase = "breach-job",
                        job = job.def.defName,
                        target = job.targetA.Thing.ThingID,
                        firingCell = job.targetB.Cell.ToString(),
                    }
                );
            });
            await Until(
                () => tower.HitPoints < before || tower.Destroyed,
                "native breacher damages structure",
                600
            );
            await On(() =>
            {
                Check(
                    "native breach job actually damages tower",
                    tower.HitPoints < before || tower.Destroyed
                );
                states.Add(
                    new
                    {
                        phase = "breach-impact",
                        before,
                        tower.HitPoints,
                        tower.Destroyed,
                    }
                );
                breacher.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                if (tower.Spawned)
                    tower.Destroy(DestroyMode.Vanish);
                Check(
                    "removed tower clears native wall links across its footprint",
                    tower
                        .OccupiedRect()
                        .All(c => (map.linkGrid.LinkFlagsAt(c) & LinkFlags.Wall) == 0)
                );
            });

            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, faction);
                GenSpawn.Spawn(p, cell, map);
                return p;
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            void Check(string name, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }
            async Task Until(Func<bool> done, string label, int limit)
            {
                for (int ticks = 0; ticks <= limit; ticks += 100)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (ticks < limit)
                    {
                        var r = await ctx.Game.StepTicksAsync(
                            Math.Min(100, limit - ticks),
                            cancellationToken: cancellationToken
                        );
                        if (!r.Success)
                            throw new InvalidOperationException("Tick stepping: " + r.Status);
                    }
                }
                throw new InvalidOperationException("Timed out: " + label);
            }
        }
        catch (Exception e)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = e.ToString() });
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        proof.environment.details = new
                        {
                            states,
                            gameplayMvid = typeof(Building_Watchtower)
                                .Assembly
                                .ManifestModule
                                .ModuleVersionId,
                        };
                        Prefs.PauseOnLoad = oldPause;
                    },
                    CancellationToken.None
                );
                if (staged)
                {
                    var r = await ctx.Tools.CallAsync(
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
                        RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", r)
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
    }
}
