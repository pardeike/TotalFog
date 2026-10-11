using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class WatchtowerAccessScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static ThingDef towerDef;
    private static JobDef accessJob;

    [Tool(
        "totalfog/watchtower_access",
        Description = "Prototype one solid 3x3 edifice with native reservations and timed ladder jobs. Check all four rotations, normal entry/exit, blocked entry, interruption cleanup and retrieval of a downed occupant followed by native bed rescue. Reload the unchanged named save. No shipping tower, elevated sight, combat, collapse damage or save/load acceptance."
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
            throw new ArgumentException("Supply a save name and a plain evidence label.");
        await gate.WaitAsync(cancellationToken);
        var manifest = RimBridgeEvidence.CreateManifest(
            "totalfog/watchtower_access",
            evidenceLabel
        );
        manifest.saveName = saveName;
        var harmony = new Harmony("brrainz.totalfog.watchtower-access-prototype");
        var owned = new List<Thing>();
        var towers = new List<WatchtowerAccessPrototype>();
        var pawns = new List<Pawn>();
        var states = new List<object>();
        Map map = null;
        MapVisibility fog = null;
        CellRect area = default;
        Pawn helper = null;
        Building_Bed bed = null;
        bool staged = false;
        int startTick = 0;
        bool oldFade = SectionLayerFog.PrefEnableFade;
        var oldHighlight = Prefs.DotHighlightDisplayMode;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    fog = map?.GetVisibility();
                    if (fog?.Initialized != true || !Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Load a paused initialized fixture first."
                        );
                    var origin = GenRadial
                        .RadialCellsAround(
                            new IntVec3(map.Size.x - 45, 0, map.Size.z / 3),
                            65,
                            true
                        )
                        .First(c =>
                            new CellRect(c.x, c.z, 24, 16).All(p =>
                                p.InBounds(map)
                                && p.Standable(map)
                                && !p.Roofed(map)
                                && !map.fogGrid.IsFogged(p)
                                && p.GetThingList(map).All(t => t is Plant or Filth)
                            )
                        );
                    area = new CellRect(origin.x, origin.z, 24, 16);
                    EnsureDefinitions();
                    startTick = Find.TickManager.TicksGame;
                    foreach (var cell in area)
                    {
                        foreach (var thing in cell.GetThingList(map).ToArray())
                            thing.Destroy(DestroyMode.Vanish);
                        fog.IncrementSeen(Faction.OfPlayer, map.cellIndices.CellToIndex(cell));
                    }
                    staged = true;
                    SectionLayerFog.PrefEnableFade = false;
                    Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.None;
                    Find.Selector.ClearSelection();
                    harmony.Patch(
                        AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos)),
                        postfix: new HarmonyMethod(
                            typeof(WatchtowerAccessScenarios),
                            nameof(Project)
                        )
                    );
                    Rand.PushState(763120);
                    try
                    {
                        for (int rotation = 0; rotation < 4; rotation++)
                        {
                            var tower = (WatchtowerAccessPrototype)
                                ThingMaker.MakeThing(
                                    towerDef,
                                    rotation % 2 == 0
                                        ? ThingDefOf.WoodLog
                                        : DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                                );
                            owned.Add(tower);
                            tower.SetFaction(Faction.OfPlayer);
                            GenSpawn.Spawn(
                                tower,
                                new IntVec3(area.minX + 3 + rotation * 6, 0, area.minZ + 8),
                                map,
                                new Rot4(rotation)
                            );
                            towers.Add(tower);
                            pawns.Add(AddPawn(tower.Approach + tower.Rotation.FacingCell * 2));
                        }
                        helper = AddPawn(new IntVec3(area.minX + 12, 0, area.minZ + 2));
                        bed = (Building_Bed)
                            ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
                        owned.Add(bed);
                        bed.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(bed, new IntVec3(area.minX + 17, 0, area.minZ + 2), map);
                        bed.Medical = true;
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    foreach (var thing in owned)
                        thing.TryGetComp<CompFog>()?.HideFromPlayer?.UpdateVisibility(true);
                    Check(
                        "all four solid 3x3 footprints",
                        towers.All(t =>
                            t.OccupiedRect().Count() == 9
                            && t.OccupiedRect().All(c => c.GetEdifice(map) == t && !c.Walkable(map))
                        )
                    );
                    Check(
                        "ordinary center pathfinding remains blocked",
                        towers
                            .Select(
                                (t, i) =>
                                    !pawns[i]
                                        .CanReach(t.Position, PathEndMode.OnCell, Danger.Deadly)
                            )
                            .All(v => v)
                    );
                    Check(
                        "all four ladder approaches are reachable",
                        towers.Select((t, i) => t.CanEnter(pawns[i])).All(v => v)
                    );
                    Check(
                        "native breaching predicates accept solid towers",
                        towers.All(t =>
                            BreachingUtility.BlocksBreaching(map, t.Position)
                            && BreachingUtility.ShouldBreachBuilding(t)
                        )
                    );
                    var obstruction = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                    owned.Add(obstruction);
                    GenSpawn.Spawn(obstruction, towers[0].Approach, map);
                    Check("blocked ladder rejects entry", !towers[0].CanEnter(pawns[0]));
                    obstruction.Destroy(DestroyMode.Vanish);
                    for (int i = 0; i < 4; i++)
                        Start(pawns[i], towers[i], 180);
                },
                cancellationToken
            );

            await Until(
                "all climbers start ascent",
                () => pawns.All(p => Driver(p)?.Phase == "ascent")
            );
            await Step(30);
            await Record(
                "climbing",
                () =>
                    pawns
                        .Select(
                            (p, i) =>
                                Driver(p)?.Phase == "ascent"
                                && Driver(p).Progress > 0
                                && Driver(p).Progress < 1
                                && p.Position == towers[i].Approach
                                && p.Rotation == towers[i].Rotation.Opposite
                                && Vector3.Distance(p.DrawPos, p.Drawer.DrawPos) > .1f
                                && Mathf.Abs(
                                    p.DrawPos.x
                                        - p.Position.ToVector3Shifted().x
                                        - Driver(p).DrawingOffset.x
                                ) < .03f
                                && Mathf.Abs(
                                    p.DrawPos.z
                                        - p.Position.ToVector3Shifted().z
                                        - Driver(p).DrawingOffset.z
                                ) < .03f
                        )
                        .All(v => v)
            );
            await Capture("climbing");
            await Until(
                "all climbers occupy the top",
                () => pawns.All(p => Driver(p)?.Phase == "watch")
            );
            await Record(
                "occupied",
                () =>
                    pawns
                        .Select(
                            (p, i) =>
                                p.Spawned
                                && p.Map == map
                                && p.Position == towers[i].Position
                                && towers[i].Occupant == p
                                && map.thingGrid.ThingsListAt(p.Position).Contains(p)
                                && map.mapPawns.AllPawnsSpawned.Contains(p)
                        )
                        .All(v => v)
            );
            await ctx.MainThread.InvokeAsync(
                () =>
                    Check(
                        "second watcher cannot reserve an occupied tower",
                        towers.All(t => !helper.CanReserve(t))
                    ),
                cancellationToken
            );
            await Capture("occupied");
            await Until(
                "all watchers start descent",
                () => pawns.All(p => Driver(p)?.Phase == "descent")
            );
            await Step(30);
            await Record(
                "descending",
                () =>
                    pawns
                        .Select(
                            (p, i) =>
                                Driver(p)?.Phase == "descent"
                                && Driver(p).Progress > 0
                                && Driver(p).Progress < 1
                                && p.Position == towers[i].Position
                                && p.Rotation == towers[i].Rotation
                        )
                        .All(v => v)
            );
            await Until("all watchers finish descent", () => pawns.All(p => Driver(p) == null));
            await Record(
                "ground",
                () =>
                    pawns
                        .Select(
                            (p, i) =>
                                p.Spawned
                                && p.Position == towers[i].Approach
                                && towers[i].Occupant == null
                                && p.Position.Walkable(map)
                                && helper.CanReserve(towers[i])
                        )
                        .All(v => v)
            );

            await ctx.MainThread.InvokeAsync(
                () => Start(pawns[0], towers[0], 600),
                cancellationToken
            );
            await Step(30);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Check(
                        "interruption control starts on the ladder",
                        Driver(pawns[0])?.Phase == "ascent"
                    );
                    pawns[0].jobs.EndCurrentJob(JobCondition.InterruptForced);
                    Check(
                        "interrupted climb releases its reservation",
                        pawns[0].Position == towers[0].Approach
                            && towers[0].Occupant == null
                            && helper.CanReserve(towers[0])
                    );
                    Start(pawns[0], towers[0], 600);
                },
                cancellationToken
            );
            await Until(
                "retrieval victim reaches the top",
                () => Driver(pawns[0])?.Phase == "watch"
            );
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    HealthUtility.DamageUntilDowned(pawns[0]);
                    Check(
                        "downed occupant stays spawned at the top",
                        pawns[0].Downed
                            && !pawns[0].Dead
                            && pawns[0].Spawned
                            && pawns[0].Position == towers[0].Position
                            && towers[0].Occupant == pawns[0]
                    );
                    Check(
                        "native rescue approach is blocked without ladder access",
                        !helper.CanReach(pawns[0], PathEndMode.ClosestTouch, Danger.Deadly)
                    );
                    Start(helper, towers[0], 0, pawns[0]);
                },
                cancellationToken
            );
            await Until(
                "rescuer carries the downed occupant",
                () => helper.carryTracker.CarriedThing == pawns[0]
            );
            await Record(
                "retrieving",
                () =>
                    helper.Position == towers[0].Position
                    && !pawns[0].Spawned
                    && pawns[0].ParentHolder == helper.carryTracker
            );
            await Until("rescuer finishes ladder descent", () => Driver(helper) == null);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Check(
                        "retrieved pawn returns to accessible ground",
                        pawns[0].Spawned
                            && !pawns[0].Dead
                            && pawns[0].Downed
                            && pawns[0].Position == towers[0].Approach
                            && helper.Position == towers[0].Approach
                            && towers[0].Occupant == null
                    );
                    var rescue = JobMaker.MakeJob(JobDefOf.Rescue, pawns[0], bed);
                    rescue.count = 1;
                    helper.jobs.TryTakeOrderedJob(rescue);
                },
                cancellationToken
            );
            await Until("native rescue delivers the pawn to bed", () => pawns[0].InBed(), 3000);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Check(
                        "native bed rescue completed",
                        pawns[0].Spawned
                            && pawns[0].CurrentBed() == bed
                            && helper.carryTracker.CarriedThing == null
                    );
                    manifest.environment.gameVersion = VersionControl.CurrentVersionString;
                    manifest.environment.mapId = map.GetUniqueLoadID();
                    manifest.environment.details = new
                    {
                        prototypeOnly = true,
                        startTick,
                        endTick = Find.TickManager.TicksGame,
                        climbTicks = JobDriver_WatchtowerAccessPrototype.ClimbTicks,
                        states,
                        footprint = "one impassable 3x3 edifice per tower",
                        navigationPatches = 0,
                        limitations = new[]
                        {
                            "interruption cleanup is immediate, not timed descent",
                            "no blocked-exit policy",
                            "no autonomous rescue discovery",
                            "no save/load proof",
                            "breach predicates, not a natural breacher raid",
                            "no elevated sight or combat",
                        },
                    };
                },
                cancellationToken
            );

            Pawn AddPawn(IntVec3 cell)
            {
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                owned.Add(pawn);
                GenSpawn.Spawn(pawn, cell, map);
                pawn.drafter.Drafted = true;
                return pawn;
            }

            void Check(string name, bool value)
            {
                manifest.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }

            async Task Step(int ticks)
            {
                var step = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!step.Success)
                    throw new InvalidOperationException(
                        "Native tick stepping failed: " + step.Status
                    );
            }

            async Task Until(string name, Func<bool> complete, int maxTicks = 1000)
            {
                for (int elapsed = 0; elapsed <= maxTicks; elapsed += 10)
                {
                    if (await ctx.MainThread.InvokeAsync(complete, cancellationToken))
                        return;
                    if (elapsed < maxTicks)
                        await Step(10);
                }
                await Record("failed: " + name, () => false);
                throw new InvalidOperationException("Timed out: " + name);
            }

            async Task Record(string name, Func<bool> check)
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        states.Add(
                            new
                            {
                                name,
                                tick = Find.TickManager.TicksGame,
                                actors = pawns
                                    .Concat(new[] { helper })
                                    .Select(p => new
                                    {
                                        p.ThingID,
                                        p.Spawned,
                                        p.Downed,
                                        cell = p.Position.ToString(),
                                        draw = p.DrawPos.ToString(),
                                        job = p.CurJobDef?.defName,
                                        phase = Driver(p)?.Phase,
                                        progress = Driver(p)?.Progress,
                                        carried = p.carryTracker.CarriedThing?.ThingID,
                                        parentHolder = p.ParentHolder?.GetType().FullName,
                                        registeredAtCell = p.Spawned
                                            && p.Map.thingGrid.ThingsListAt(p.Position).Contains(p),
                                        centerOccupant = p.Spawned
                                        && p.Position.GetEdifice(p.Map)
                                            is WatchtowerAccessPrototype tower
                                            ? tower.Occupant?.ThingID
                                            : null,
                                    })
                                    .ToArray(),
                            }
                        );
                        manifest.environment.details = new { startTick, states };
                        Check(name, check());
                    },
                    cancellationToken
                );
            }

            async Task Capture(string name)
            {
                var rect = new
                {
                    x = area.minX,
                    z = area.minZ + 3,
                    width = 24,
                    height = 11,
                    rootSize = 11,
                };
                var frame = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    rect,
                    cancellationToken: cancellationToken
                );
                if (!frame.Succeeded())
                    throw new InvalidOperationException("Framing failed: " + name);
                await ctx.Game.FramesAsync(12, cancellationToken);
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        rect.x,
                        rect.z,
                        rect.width,
                        rect.height,
                        rect.rootSize,
                        paddingCells = 0,
                        fileName = evidenceLabel + "-" + name,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                manifest.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + name, shot));
                manifest.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = name,
                        kind = "cell_rect",
                        path = shot.TryReadResult<string>(out var path, "path")
                            ? path
                            : string.Empty,
                        details = shot.Result,
                        success = shot.Succeeded(),
                    }
                );
                if (!shot.Succeeded())
                    throw new InvalidOperationException("Capture failed: " + name);
            }
        }
        catch (Exception exception)
        {
            manifest.errors.Add(new RimBridgeEvidenceError { message = exception.ToString() });
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        foreach (var pawn in owned.OfType<Pawn>())
                            if (!pawn.Destroyed)
                                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                        foreach (var tower in towers)
                            if (tower.Occupant?.Spawned == true)
                                tower.Ground(tower.Occupant);
                        foreach (var thing in owned)
                            if (!thing.Destroyed)
                                thing.Destroy(DestroyMode.Vanish);
                        harmony.UnpatchAll(harmony.Id);
                        if (staged)
                            foreach (var cell in area)
                                fog.DecrementSeen(
                                    Faction.OfPlayer,
                                    map.cellIndices.CellToIndex(cell)
                                );
                        SectionLayerFog.PrefEnableFade = oldFade;
                        Prefs.DotHighlightDisplayMode = oldHighlight;
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
                    manifest.assertions.Add(
                        RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
                    );
                }
            }
            finally
            {
                gate.Release();
            }
        }
        RimBridgeEvidence.Complete(manifest);
        return manifest;
    }

    private static JobDriver_WatchtowerAccessPrototype Driver(Pawn pawn) =>
        pawn.jobs.curDriver as JobDriver_WatchtowerAccessPrototype;

    private static void Start(
        Pawn pawn,
        WatchtowerAccessPrototype tower,
        int holdTicks,
        Pawn takee = null
    )
    {
        var job = JobMaker.MakeJob(accessJob, tower);
        job.count = holdTicks;
        job.targetC = takee;
        pawn.jobs.TryTakeOrderedJob(job);
    }

    private static void Project(Pawn __instance, ref Vector3 __result)
    {
        var driver = Driver(__instance);
        if (driver != null)
            __result += driver.DrawingOffset;
        else if (
            __instance.Spawned
            && __instance.Position.GetEdifice(__instance.Map) is WatchtowerAccessPrototype tower
            && tower.Occupant == __instance
        )
            __result.z += __instance.Downed
                ? 1.02f
                : WatchtowerVisualPrototype.OccupantDrawProjection;
    }

    private static void EnsureDefinitions()
    {
        if (towerDef != null)
            return;
        towerDef = WatchtowerVisualScenarios.CreateDefinition(
            "TotalFog_WatchtowerAccessPrototype",
            typeof(WatchtowerAccessPrototype),
            Traversability.Impassable
        );
        accessJob = new JobDef
        {
            defName = "TotalFog_WatchtowerAccessPrototypeJob",
            driverClass = typeof(JobDriver_WatchtowerAccessPrototype),
            reportString = "using the watchtower ladder.",
            casualInterruptible = false,
        };
        PresentationScenarios.AssignHash(accessJob);
        DefGenerator.AddImpliedDef(accessJob);
        accessJob.ResolveReferences();
    }
}
