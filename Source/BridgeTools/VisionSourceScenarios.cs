using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native staffing, range and patch-order controls, excluded from player builds.</summary>
public sealed class VisionSourceScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static CompPowerTrader poweredConsole;
    private static int ordinaryPrefixCalls;
    private static int genericPrefixCalls;

    [Tool(
        "totalfog/vision_source_controls",
        Description = "On a paused initialized save, verify native Cameras work selection, staffed-console duration, work-disable and power-loss interruption, independent telescope caps, and explicit fog prefix ordering. Console power is supplied by a scoped probe. Reloads the unchanged save and restores settings/patches."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (string.IsNullOrWhiteSpace(saveName) || string.IsNullOrWhiteSpace(evidenceLabel))
            throw new ArgumentException("Supply the loaded save name and an evidence label.");
        await gate.WaitAsync(cancellationToken);
        var manifest = RimBridgeEvidence.CreateManifest(
            "totalfog/vision_source_controls",
            evidenceLabel
        );
        manifest.saveName = saveName;
        var harmony = new Harmony("brrainz.totalfog.vision-source-probe");
        var rows = new List<object>();
        var owned = new List<Thing>();
        Pawn worker = null;
        Building_VisionConsole console = null;
        WorkTypeDef cameras = null;
        bool oldWatcher = FogSettings.NeedWatcher;
        bool oldMusic = FogSettings.SuppressCombatMusic;
        int oldBase = FogSettings.BaseViewRange;
        int oldTower = FogSettings.TowerMaxSightRange;
        int oldTelescope = FogSettings.TelescopeMaxSightRange;
        bool oldPriorities = false;
        bool capturedPriorities = false;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map = Find.CurrentMap;
                    if (map == null || !Find.TickManager.Paused || !map.GetVisibility().Initialized)
                        throw new InvalidOperationException(
                            "Load and pause the named initialized save first."
                        );
                    cameras = DefDatabase<WorkTypeDef>.GetNamed("TotalFog_Surveillance");
                    worker = map.mapPawns.FreeColonistsSpawned.First(p =>
                        !p.Downed
                        && !p.Dead
                        && !p.WorkTypeIsDisabled(cameras)
                        && p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) >= .8f
                    );
                    int initialPriority = worker.workSettings.GetPriority(cameras);
                    Check(
                        "existing colony Cameras priority is readable",
                        initialPriority >= 0 && initialPriority <= 4,
                        initialPriority
                    );
                    Check(
                        "Cameras is independently ordered below Research",
                        cameras.defName != "BasicWorker"
                            && cameras.naturalPriority < WorkTypeDefOf.Research.naturalPriority,
                        cameras.naturalPriority
                    );
                    var origin = map.AllCells.First(c =>
                        new CellRect(c.x, c.z, 10, 7).InBounds(map)
                        && c.x > 5
                        && c.z > 5
                        && new CellRect(c.x, c.z, 10, 7).All(p =>
                            p.Standable(map) && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    foreach (var cell in new CellRect(origin.x, origin.z, 10, 7))
                        map.fogGrid.Unfog(cell);
                    Thing Spawn(ThingDef def, int x, int z)
                    {
                        var thing = ThingMaker.MakeThing(
                            def,
                            def.MadeFromStuff ? ThingDefOf.Steel : null
                        );
                        thing.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(thing, origin + new IntVec3(x, 0, z), map);
                        owned.Add(thing);
                        return thing;
                    }
                    console = (Building_VisionConsole)Spawn(FogDefOf.CameraConsole, 2, 2);
                    var offline = (Building_VisionConsole)Spawn(FogDefOf.CameraConsole, 6, 2);
                    poweredConsole = console.GetComp<CompPowerTrader>();
                    poweredConsole.PowerOn = true;
                    offline.GetComp<CompPowerTrader>().PowerOn = false;
                    harmony.Patch(
                        AccessTools.PropertyGetter(
                            typeof(CompPowerTrader),
                            nameof(CompPowerTrader.PowerOn)
                        ),
                        postfix: new HarmonyMethod(
                            typeof(VisionSourceScenarios),
                            nameof(PowerPostfix)
                        )
                    );
                    FogSettings.NeedWatcher = true;
                    worker.drafter.Drafted = false;
                    worker.jobs.EndCurrentJob(JobCondition.InterruptForced, false);
                    worker.pather.StopDead();
                    worker.Position = console.InteractionCell;
                    worker.Drawer.tweener.Notify_Teleported();
                    worker.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    oldPriorities = Find.PlaySettings.useWorkPriorities;
                    capturedPriorities = true;
                    Find.PlaySettings.useWorkPriorities = true;
                    worker.workSettings.DisableAll();
                    var giver = new JobGiver_Work();
                    Check(
                        "disabled Cameras work is absent from native work list",
                        !worker.workSettings.WorkGiversInOrderNormal.Any(w =>
                            w.def.workType == cameras
                        )
                    );
                    worker.workSettings.SetPriority(cameras, 1);
                    Check(
                        "enabled Cameras work appears in native work list",
                        worker.workSettings.WorkGiversInOrderNormal.Any(w =>
                            w.def.workType == cameras
                        )
                    );
                    var monitor = new WorkGiver_MonitorVision();
                    Check(
                        "offline console is rejected even with another working console",
                        !monitor.HasJobOnThing(worker, offline)
                    );
                    var job = giver.TryIssueJobPackage(worker, default).Job;
                    Check(
                        "native work selection chooses the working console",
                        job?.def == FogDefOf.SurveilCameraConsole && job.targetA.Thing == console,
                        job?.def?.defName
                    );
                    worker.jobs.StartJob(job, JobCondition.InterruptForced);

                    var telescope = Spawn(FogDefOf.WatchTelescope, 2, 5);
                    var source = worker.TryGetComp<CompFog>().FieldOfViewWatcher;
                    FogSettings.BaseViewRange = 100;
                    FogSettings.TelescopeMaxSightRange = 90;
                    FogSettings.TowerMaxSightRange = 30;
                    float first = source.CalcPawnSightRange(telescope.Position, false, false);
                    FogSettings.TowerMaxSightRange = 120;
                    float second = source.CalcPawnSightRange(telescope.Position, false, false);
                    Check(
                        "tower cap does not change telescope sight",
                        Math.Abs(first - second) < .001f,
                        new { first, second }
                    );
                    FogSettings.TelescopeMaxSightRange = 30;
                    float capped = source.CalcPawnSightRange(telescope.Position, false, false);
                    Check(
                        "telescope cap changes telescope sight",
                        capped <= 30 && first > capped,
                        new { first, capped }
                    );
                    float ground = source.CalcPawnSightRange(worker.Position, false, false);
                    FogSettings.TelescopeMaxSightRange = 120;
                    Check(
                        "telescope setting leaves ordinary sight unchanged",
                        Math.Abs(ground - source.CalcPawnSightRange(worker.Position, false, false))
                            < .001f,
                        ground
                    );
                    FogSettings.BaseViewRange = oldBase;
                    FogSettings.TowerMaxSightRange = oldTower;
                    FogSettings.TelescopeMaxSightRange = oldTelescope;

                    ordinaryPrefixCalls = genericPrefixCalls = 0;
                    var danger = AccessTools.PropertyGetter(
                        typeof(MusicManagerPlay),
                        nameof(MusicManagerPlay.DangerMusicMode)
                    );
                    harmony.Patch(
                        danger,
                        prefix: new HarmonyMethod(
                            typeof(VisionSourceScenarios),
                            nameof(OrdinaryPrefix)
                        )
                        {
                            priority = Priority.Normal,
                        }
                    );
                    FogSettings.SuppressCombatMusic = true;
                    Check(
                        "ordinary prefix runs before the explicit fog cancellation",
                        !(bool)danger.Invoke(Find.MusicManagerPlay, null)
                            && ordinaryPrefixCalls == 1,
                        ordinaryPrefixCalls
                    );
                    var target = AccessTools.Method(
                        typeof(VisionSourceScenarios),
                        nameof(GenericTarget)
                    );
                    var helper = typeof(TotalFogMod).Assembly.GetType("TotalFog.PatchSafety");
                    AccessTools
                        .Method(helper, "Patch")
                        .Invoke(
                            null,
                            new object[]
                            {
                                harmony,
                                target,
                                new HarmonyMethod(
                                    typeof(VisionSourceScenarios),
                                    nameof(GenericPrefix)
                                ),
                                null,
                                null,
                                null,
                            }
                        );
                    var prefix = Harmony
                        .GetPatchInfo(target)
                        .Prefixes.Single(p => p.owner == harmony.Id);
                    Check(
                        "generic bool prefixes retain Harmony default priority",
                        prefix.priority == Priority.Normal,
                        prefix.priority
                    );
                    Check(
                        "generic bool prefix executes",
                        (bool)target.Invoke(null, null) && genericPrefixCalls == 1
                    );
                },
                cancellationToken
            );
            await Step(300);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Check(
                        "operator remains staffed through 300 native ticks",
                        worker.CurJobDef == FogDefOf.SurveilCameraConsole && console.Manned,
                        worker.CurJobDef?.defName
                    );
                    worker.workSettings.SetPriority(cameras, 0);
                    Check(
                        "disabling Cameras work ends the native monitor job",
                        worker.CurJobDef != FogDefOf.SurveilCameraConsole,
                        worker.CurJobDef?.defName
                    );
                    worker.workSettings.SetPriority(cameras, 1);
                    var job = new JobGiver_Work().TryIssueJobPackage(worker, default).Job;
                    Check(
                        "reenabling Cameras permits native monitor work",
                        job?.def == FogDefOf.SurveilCameraConsole
                    );
                    worker.jobs.StartJob(job, JobCondition.InterruptForced);
                },
                cancellationToken
            );
            await Step(10);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    poweredConsole = null;
                    console.GetComp<CompPowerTrader>().PowerOn = false;
                },
                cancellationToken
            );
            await Step(5);
            await ctx.MainThread.InvokeAsync(
                () =>
                    Check(
                        "power loss ends monitor work",
                        worker.CurJobDef != FogDefOf.SurveilCameraConsole,
                        worker.CurJobDef?.defName
                    ),
                cancellationToken
            );
            RimBridgeEvidence.Complete(manifest);
            return new
            {
                success = manifest.success,
                manifest,
                rows,
            };
        }
        catch (Exception error)
        {
            manifest.success = false;
            manifest.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
            return new
            {
                success = false,
                manifest,
                rows,
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        poweredConsole = null;
                        FogSettings.NeedWatcher = oldWatcher;
                        FogSettings.SuppressCombatMusic = oldMusic;
                        FogSettings.BaseViewRange = oldBase;
                        FogSettings.TowerMaxSightRange = oldTower;
                        FogSettings.TelescopeMaxSightRange = oldTelescope;
                        if (capturedPriorities)
                            Find.PlaySettings.useWorkPriorities = oldPriorities;
                        foreach (var thing in owned)
                            if (!thing.Destroyed)
                                thing.Destroy(DestroyMode.Vanish);
                    },
                    CancellationToken.None
                );
                var reset = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                        ignoreModCompatibility = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!reset.Succeeded())
                    throw new InvalidOperationException(
                        "Vision-source cleanup could not reload the fixture."
                    );
            }
            finally
            {
                gate.Release();
            }
        }

        void Check(string name, bool passed, object actual = null)
        {
            manifest.assertions.Add(RimBridgeEvidence.IsTrue(name, passed, details: actual));
            rows.Add(
                new
                {
                    name,
                    passed,
                    actual,
                }
            );
            if (!passed)
                throw new InvalidOperationException(name);
        }
        async Task Step(int ticks)
        {
            var result = await ctx.Game.StepTicksAsync(ticks, cancellationToken: cancellationToken);
            if (!result.Success)
                throw new InvalidOperationException("Native stepping: " + result.Status);
        }
    }

    private static void PowerPostfix(CompPowerTrader __instance, ref bool __result)
    {
        if (__instance == poweredConsole)
            __result = true;
    }

    private static bool OrdinaryPrefix()
    {
        ordinaryPrefixCalls++;
        return true;
    }

    private static bool GenericPrefix()
    {
        genericPrefixCalls++;
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool GenericTarget() => true;
}
