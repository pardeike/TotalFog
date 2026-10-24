using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class HearingCueScenarios
{
    private static Pawn listener,
        target;
    private static string phase;
    private static readonly List<object> samples = new();
    private static readonly HashSet<Mote_HearingCue> drawnCues = new();
    private static readonly List<(string phase, bool moving, int cues, bool visible)> outcomes =
        new();
    private static HashSet<Mote_HearingCue> before;
    private static int zombieTicks,
        zombieMovingTicks,
        zombieCellChanges;
    private static IntVec3 zombieLastCell;

    [Tool(
        "totalfog/hearing_cues",
        Description = "Compare scheduled hearing rings for real humans and ordinary zombies behind a wall while walking, waiting and attacking. Record each eligibility gate and native cue visibility. Restore settings and reload the unchanged paused single-player save."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        bool exploredGround = false,
        string evidenceLabel = "hearing-cues"
    )
    {
        if (
            string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Use a plain evidence label.");
        var proof = RimBridgeEvidence.CreateManifest("totalfog/hearing_cues");
        proof.saveName = saveName;
        var owned = new List<Thing>();
        var movements = new List<object>();
        var captureStates = new List<object>();
        var oldHearing = FogSettings.BaseHearingRange;
        var oldRange = FogSettings.BaseViewRange;
        var oldCues = FogSettings.ShowHearingCues;
        var oldOutside = FogSettings.OnlyOutsideColony;
        var harmony = new Harmony("brrainz.totalfog.hearing-cue-probe");
        Map map = null;
        IntVec3 origin = IntVec3.Invalid;
        Thing attackWall = null;
        IntVec3 startPosition = IntVec3.Invalid;
        int wallHitPoints = 0;
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
                    origin = map.AllCells.First(c =>
                        c.x > 10
                        && c.z > 10
                        && c.x < map.Size.x - 12
                        && c.z < map.Size.z - 15
                        && new CellRect(c.x, c.z, 10, 13).All(p =>
                            p.Standable(map)
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    FogSettings.BaseHearingRange = 10;
                    FogSettings.BaseViewRange = 8;
                    FogSettings.ShowHearingCues = true;
                    FogSettings.OnlyOutsideColony = false;
                    foreach (var cell in new CellRect(origin.x, origin.z, 10, 13))
                    {
                        map.fogGrid.Unfog(cell);
                        if (exploredGround)
                            fog.RevealCell(map.cellIndices.CellToIndex(cell));
                    }
                    for (int z = 0; z < 13; z++)
                    {
                        var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                        owned.Add(GenSpawn.Spawn(wall, origin + new IntVec3(3, 0, z), map));
                        if (z == 6)
                            attackWall = wall;
                    }
                    listener = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    owned.Add(GenSpawn.Spawn(listener, origin + new IntVec3(1, 0, 6), map));
                    Wait(listener);
                    listener.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    samples.Clear();
                    outcomes.Clear();
                    harmony.Patch(
                        AccessTools.Method(typeof(CompSightSource), "EmitHearingCues"),
                        prefix: new HarmonyMethod(typeof(HearingCueScenarios), nameof(BeforeEmit)),
                        postfix: new HarmonyMethod(typeof(HearingCueScenarios), nameof(AfterEmit))
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.Zombie"),
                            "CustomTick"
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            "SuppressMobileFixtureCleanup"
                        ),
                        postfix: new HarmonyMethod(
                            typeof(HearingCueScenarios),
                            nameof(AfterZombieTick)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(Graphic_HearingCue),
                            nameof(Graphic_HearingCue.DrawWorker)
                        ),
                        postfix: new HarmonyMethod(typeof(HearingCueScenarios), nameof(RecordDraw))
                    );
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/screenshot_cell_rect",
                new
                {
                    fileName = evidenceLabel + "-setup",
                    x = origin.x,
                    z = origin.z + 3,
                    width = 8,
                    height = 8,
                    paddingCells = 1,
                    rootSize = 12,
                    includeTargets = false,
                    doNotResetCamera = true,
                },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Hearing camera setup failed.");
            foreach (var kind in new[] { "human", "zombie" })
            foreach (var activity in new[] { "walking", "stationary", "attacking", "natural" })
            {
                if (kind == "human" && activity == "natural")
                    continue;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (target?.Spawned == true)
                            target.Destroy(DestroyMode.Vanish);
                        phase = kind + "-" + activity;
                        drawnCues.Clear();
                        zombieTicks = zombieMovingTicks = zombieCellChanges = 0;
                        zombieLastCell = IntVec3.Invalid;
                        var cell = origin + new IntVec3(activity == "attacking" ? 4 : 5, 0, 6);
                        if (kind == "human")
                        {
                            target = PawnGenerator.GeneratePawn(
                                PawnKindDefOf.Colonist,
                                Faction.OfAncientsHostile
                            );
                            GenSpawn.Spawn(target, cell, map);
                        }
                        else
                        {
                            target = (Pawn)
                                AccessTools
                                    .Method(
                                        AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions"),
                                        "SpawnZombie"
                                    )
                                    .Invoke(
                                        null,
                                        new object[]
                                        {
                                            cell,
                                            map,
                                            Enum.Parse(
                                                AccessTools.TypeByName("ZombieLand.ZombieType"),
                                                "Normal"
                                            ),
                                            true,
                                        }
                                    );
                        }
                        if (target == null)
                            throw new InvalidOperationException("Target spawn failed.");
                        owned.Add(target);
                        startPosition = target.Position;
                        wallHitPoints = attackWall.HitPoints;
                        if (activity == "walking")
                            target.jobs.StartJob(
                                JobMaker.MakeJob(JobDefOf.Goto, cell + new IntVec3(0, 0, 5)),
                                JobCondition.InterruptForced
                            );
                        else if (activity == "stationary")
                            Wait(target);
                        else if (activity == "attacking")
                            target.jobs.StartJob(
                                JobMaker.MakeJob(JobDefOf.AttackMelee, attackWall),
                                JobCondition.InterruptForced
                            );
                    },
                    cancellationToken
                );
                for (int part = 0; part < 2; part++)
                {
                    var ticks = await ctx.Game.RunForTicksAsync(
                        120,
                        new RimBridgeRunTicksOptions
                        {
                            ForceNormalSpeed = true,
                            PauseWhenDone = true,
                            TimeoutMs = 10000,
                        },
                        cancellationToken
                    );
                    if (!ticks.Success)
                        throw new InvalidOperationException(phase + " normal playback failed.");
                    if (part == 0 && activity == "walking")
                    {
                        await ctx.MainThread.InvokeAsync(
                            () =>
                            {
                                var live = map
                                    .dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
                                    .Where(c => c.Alpha > 0 && c.ExactScale.x > 0)
                                    .ToArray();
                                proof.assertions.Add(
                                    RimBridgeEvidence.IsTrue(
                                        phase + " ring draws above opaque fog",
                                        live.Length > 0
                                            && live.All(c =>
                                                c.DrawPos.y > AltitudeLayer.FogOfWar.AltitudeFor()
                                            )
                                    )
                                );
                                proof.assertions.Add(
                                    RimBridgeEvidence.IsTrue(
                                        phase + " material draws after opaque fog",
                                        live.Length > 0
                                            && live.All(c =>
                                                c.Graphic.MatSingle.renderQueue
                                                > MatBases.FogOfWar.renderQueue
                                            )
                                    )
                                );
                            },
                            cancellationToken
                        );
                        captureStates.Add(
                            await ctx.MainThread.InvokeAsync<object>(
                                () =>
                                    new
                                    {
                                        phase,
                                        tick = Find.TickManager.TicksGame,
                                        fogAltitude = AltitudeLayer.FogOfWar.AltitudeFor(),
                                        fogRenderQueue = MatBases.FogOfWar.renderQueue,
                                        motes = map
                                            .dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
                                            .Select(c => new
                                            {
                                                c.Alpha,
                                                c.AgeSecs,
                                                scale = c.ExactScale.ToString(),
                                                position = c.DrawPos.ToString(),
                                                renderQueue = c.Graphic.MatSingle.renderQueue,
                                                c.paused,
                                                realTime = c.def.mote.realTime,
                                                known = map.GetVisibility()
                                                    .IsKnown(
                                                        Faction.OfPlayer,
                                                        map.cellIndices.CellToIndex(c.Position)
                                                    ),
                                            })
                                            .ToArray(),
                                    },
                                cancellationToken
                            )
                        );
                        var shot = await ctx.Tools.CallAsync(
                            "rimworld/screenshot_cell_rect",
                            new
                            {
                                fileName = evidenceLabel
                                    + "-"
                                    + phase
                                    + (exploredGround ? "-explored" : "-unknown"),
                                x = origin.x,
                                z = origin.z + 3,
                                width = 8,
                                height = 8,
                                paddingCells = 1,
                                rootSize = 12,
                                includeTargets = false,
                                doNotResetCamera = true,
                            },
                            cancellationToken: cancellationToken
                        );
                        proof.assertions.Add(
                            RimBridgeEvidence.ToolSucceeded(phase + " screenshot", shot)
                        );
                        proof.captures.Add(
                            new RimBridgeEvidenceCapture
                            {
                                label = phase,
                                path = shot.TryReadResult<string>(out var path, "path")
                                    ? path
                                    : string.Empty,
                                details = shot.Result,
                            }
                        );
                    }
                }
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool moved = target.Position != startPosition;
                        proof.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                phase + " preserves hidden target and terrain knowledge",
                                !Visibility.IsVisible(target)
                                    && !map.GetVisibility()
                                        .IsShown(Faction.OfPlayer, target.Position)
                                    && map.GetVisibility()
                                        .IsKnown(
                                            Faction.OfPlayer,
                                            map.cellIndices.CellToIndex(target.Position)
                                        ) == exploredGround
                            )
                        );
                        movements.Add(
                            new
                            {
                                phase,
                                start = startPosition.ToString(),
                                end = target.Position.ToString(),
                                moved,
                                wallDamage = wallHitPoints - attackWall.HitPoints,
                                drawnCues = drawnCues.Count,
                                zombieTicks,
                                zombieMovingTicks,
                                zombieCellChanges,
                                target.Dead,
                            }
                        );
                        if (activity == "walking")
                            proof.assertions.Add(
                                RimBridgeEvidence.IsTrue(phase + " changes native position", moved)
                            );
                        var checks = outcomes.Where(o => o.phase == phase).ToArray();
                        if (activity is "walking" or "natural")
                        {
                            proof.assertions.Add(
                                RimBridgeEvidence.IsTrue(
                                    phase + " emits an observer-visible ring",
                                    checks.Any(o => o.moving && o.cues > 0 && o.visible)
                                )
                            );
                            proof.assertions.Add(
                                RimBridgeEvidence.IsTrue(
                                    phase + " submits a positive-alpha ring to the native graphic",
                                    drawnCues.Count > 0
                                )
                            );
                        }
                        else
                            proof.assertions.Add(
                                RimBridgeEvidence.IsTrue(
                                    phase + " preserves movement-only cue policy",
                                    checks.Length > 0 && checks.All(o => !o.moving && o.cues == 0)
                                )
                            );
                        if (activity == "attacking")
                            proof.assertions.Add(
                                RimBridgeEvidence.IsTrue(
                                    phase + " damages the native wall",
                                    wallHitPoints > attackWall.HitPoints
                                )
                            );
                    },
                    cancellationToken
                );
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    proof.assertions.Add(
                        RimBridgeEvidence.IsTrue(
                            "scheduled listener sampled each phase",
                            new[]
                            {
                                "human-walking",
                                "human-stationary",
                                "human-attacking",
                                "zombie-walking",
                                "zombie-stationary",
                                "zombie-attacking",
                                "zombie-natural",
                            }.All(name =>
                                samples.Any(s =>
                                    (string)AccessTools.Property(s.GetType(), "phase").GetValue(s)
                                    == name
                                )
                            )
                        )
                    );
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
                    harmony.UnpatchAll(harmony.Id);
                    proof.environment.details = new
                    {
                        gameplayMvid = typeof(MapVisibility)
                            .Assembly
                            .ManifestModule
                            .ModuleVersionId,
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => new { m.PackageId, m.RootDir })
                            .ToArray(),
                        origin = origin.ToString(),
                        samples = samples.ToArray(),
                        movements = movements.ToArray(),
                        exploredGround,
                        captureStates = captureStates.ToArray(),
                    };
                    drawnCues.Clear();
                    samples.Clear();
                    outcomes.Clear();
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy(DestroyMode.Vanish);
                    FogSettings.BaseHearingRange = oldHearing;
                    FogSettings.BaseViewRange = oldRange;
                    FogSettings.ShowHearingCues = oldCues;
                    FogSettings.OnlyOutsideColony = oldOutside;
                    listener = target = null;
                    before = null;
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
    }

    private static void RecordDraw(Thing thing)
    {
        if (thing is Mote_HearingCue cue && cue.Alpha > 0 && cue.ExactScale.x > 0)
            drawnCues.Add(cue);
    }

    private static void AfterZombieTick(Pawn __instance)
    {
        if (__instance != target)
            return;
        zombieTicks++;
        if (__instance.pather?.Moving == true)
            zombieMovingTicks++;
        if (zombieLastCell.IsValid && zombieLastCell != __instance.Position)
            zombieCellChanges++;
        zombieLastCell = __instance.Position;
    }

    private static void Wait(Pawn pawn)
    {
        var job = JobMaker.MakeJob(JobDefOf.Wait);
        job.expiryInterval = 10000;
        pawn.jobs.StartJob(job, JobCondition.InterruptForced);
    }

    private static void BeforeEmit(CompSightSource __instance)
    {
        if (__instance.parent != listener || target?.Spawned != true)
            return;
        before = new HashSet<Mote_HearingCue>(
            listener.Map.dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
        );
    }

    private static void AfterEmit(CompSightSource __instance)
    {
        if (__instance.parent != listener || target?.Spawned != true || before == null)
            return;
        var map = listener.Map;
        var hearing = listener.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
        var created = map
            .dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
            .Where(c => !before.Contains(c))
            .ToArray();
        outcomes.Add(
            (
                phase,
                target.pather?.Moving == true,
                created.Length,
                created.All(c => Visibility.IsVisible(c))
            )
        );
        samples.Add(
            new
            {
                phase,
                tick = Find.TickManager.TicksGame,
                type = target.GetType().FullName,
                position = target.Position.ToString(),
                target.BodySize,
                registered = map.mapPawns.AllPawnsSpawned.Contains(target),
                targetFaction = target.Faction?.loadID,
                observerFaction = listener.Faction?.loadID,
                moving = target.pather?.Moving == true,
                inRange = target.Position.InHorDistOf(
                    listener.Position,
                    FogSettings.BaseHearingRange * hearing
                ),
                hidden = !map.GetVisibility().IsShown(listener.Faction, target.Position),
                nativeFog = target.Position.Fogged(map),
                target.Dead,
                target.Downed,
                job = target.CurJobDef?.defName,
                hearing,
                cuesEnabled = FogSettings.ShowHearingCues,
                cues = map
                    .dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
                    .Where(c => !before.Contains(c))
                    .Select(c => new
                    {
                        c.ObserverFactionId,
                        visible = Visibility.IsVisible(c),
                        position = c.exactPosition.ToString(),
                        c.Alpha,
                    })
                    .ToArray(),
            }
        );
        before = null;
    }
}
