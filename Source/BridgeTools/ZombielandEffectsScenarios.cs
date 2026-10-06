using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Observe cross-pawn effects on the real renderer, without a Zombieland build dependency.</summary>
public sealed partial class ZombielandEffectsScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static readonly HashSet<Material> healingMaterials = new();
    private static Vector3 endpoint;
    private static Material cyanLineMaterial;
    private static int glows,
        beams;
    private static Pawn simulationHealer;
    private static Pawn simulationTarget;
    private static int simulationCalls,
        simulationDraws;
    private static readonly List<object> simulationDeaths = new();
    private static readonly HashSet<string> readoutHighlights = new();
    private static int readoutCount;
    private static Vector2 readoutPoint;

    [Tool(
        "totalfog/zombieland_healing_clock",
        Description = "Verify the installed healer effect clock at exact boundaries, repeated calls and skipped simulation ticks. Uses detached records, changes no world state and advances no ticks."
    )]
    public static Task<object> HealingClock(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    ) =>
        ctx.MainThread.InvokeAsync<object>(
            () =>
            {
                var type =
                    AccessTools.TypeByName("ZombieLand.HealerInfo")
                    ?? throw new InvalidOperationException("Load Zombieland first.");
                var advance =
                    AccessTools.Method(type, "Advance", new[] { typeof(int) })
                    ?? throw new InvalidOperationException(
                        "The installed Zombieland has no simulation-clock fix."
                    );
                var step = AccessTools.Field(type, "step");
                int start = Find.TickManager.TicksGame;
                var info = Activator.CreateInstance(type, new object[] { null });
                var rows = new List<object>();
                bool passed = true;
                foreach (
                    var sample in new[]
                    {
                        (0, 0),
                        (12, 12),
                        (55, 55),
                        (55, 55),
                        (60, 60),
                        (120, 60),
                    }
                )
                {
                    advance.Invoke(info, new object[] { start + sample.Item1 });
                    int actual = (int)step.GetValue(info);
                    passed &= actual == sample.Item2;
                    rows.Add(
                        new
                        {
                            elapsed = sample.Item1,
                            expected = sample.Item2,
                            actual,
                        }
                    );
                }
                var staged = Activator.CreateInstance(type, new object[] { null });
                step.SetValue(staged, 12);
                advance.Invoke(staged, new object[] { start + 48 });
                bool preservesExistingAge = (int)step.GetValue(staged) == 60;
                return new
                {
                    passed = passed && preservesExistingAge && start == Find.TickManager.TicksGame,
                    startTick = start,
                    endTick = Find.TickManager.TicksGame,
                    preservesExistingAge,
                    rows,
                    zombieMvid = type.Assembly.ManifestModule.ModuleVersionId.ToString(),
                };
            },
            cancellationToken
        );

    [Tool(
        "totalfog/zombieland_hidden_healing",
        Description = "Repeatedly injure a staged wounded-target fixture while its healer is outside sight and camera view. Uses normal live playback through the production scheduler, observes CustomTick/draw calls, then reloads the named fixture and restores settings/test mode/diagnostic patches."
    )]
    public static async Task<object> HiddenHealing(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string healerId,
        string targetId,
        string saveName,
        int pulses = 9,
        int ticksPerPulse = 64
    )
    {
        if (pulses < 9 || pulses > 16 || ticksPerPulse < 32 || ticksPerPulse > 120)
            throw new ArgumentException("Use 9..16 pulses and 32..120 native ticks per pulse.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.hidden-healing-probe");
        Pawn healer = null,
            target = null;
        Map map = null;
        MapVisibility fog = null;
        IList effects = null;
        FieldInfo step = null;
        IntVec3 healerPosition = IntVec3.Invalid,
            targetPosition = IntVec3.Invalid;
        int oldRange = FogSettings.BaseViewRange,
            startTick = -1;
        bool testModeStarted = false;
        var rows = new List<object>();
        bool passed = true;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException(
                            "Load the staged healer fixture first."
                        );
                    fog = map.GetComponent<MapVisibility>();
                    if (
                        !Find.TickManager.Paused
                        || !fog.Initialized
                        || FogSettings.OnlyOutsideColony
                    )
                        throw new InvalidOperationException(
                            "Use paused initialized colony fog with bypass disabled."
                        );
                    healer = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == healerId);
                    target = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == targetId);
                    if (
                        healer.Dead
                        || target.Dead
                        || healer.Faction == Faction.OfPlayer
                        || target.Faction == Faction.OfPlayer
                        || !(bool)AccessTools.Field(healer.GetType(), "isHealer").GetValue(healer)
                    )
                        throw new InvalidOperationException(
                            "Stage a living non-player healer/target pair."
                        );
                    effects = (IList)
                        AccessTools.Field(healer.GetType(), "healInfo").GetValue(healer);
                    step = AccessTools.Field(
                        AccessTools.TypeByName("ZombieLand.HealerInfo"),
                        "step"
                    );
                    effects.Clear();
                    target.health.hediffSet.Clear();
                    healerPosition = healer.Position;
                    // Keep the target within every difficulty's healing radius.
                    targetPosition = healerPosition + IntVec3.North;
                    if (!targetPosition.Standable(map))
                        throw new InvalidOperationException(
                            "Stage a healer with an open north cell."
                        );
                    target.Position = targetPosition;
                    FogSettings.BaseViewRange = 5;
                    foreach (
                        var pawn in map.mapPawns.AllPawnsSpawned.Where(p =>
                            p.Faction == Faction.OfPlayer
                        )
                    )
                        Move(pawn, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
                    foreach (var source in fog.fowWatchers)
                        source.UpdateFoV(true);
                    fog.MapComponentTick();
                    if (
                        fog.IsShown(Faction.OfPlayer, healer.Position)
                        || fog.IsShown(Faction.OfPlayer, target.Position)
                    )
                        throw new InvalidOperationException(
                            "Both fixture pawns must be outside real player sight."
                        );
                    simulationHealer = healer;
                    simulationTarget = target;
                    simulationCalls = simulationDraws = 0;
                    simulationDeaths.Clear();
                    harmony.Patch(
                        AccessTools.Method(healer.GetType(), "CustomTick"),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(SuppressFixtureCleanup)
                        ),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveSimulation)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveSimulationDraw)
                        )
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(Pawn), nameof(Pawn.Kill)),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveSimulationDeath)
                        )
                    );
                    startTick = Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            var mode = await ctx.Tools.CallAsync(
                "zombieland/zombie_ticking_test_mode",
                new { enabled = true },
                cancellationToken: cancellationToken
            );
            if (!mode.Succeeded())
                throw new InvalidOperationException(
                    "Could not suppress unrelated zero-threat cleanup."
                );
            testModeStarted = true;
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = map.Size.x / 2, z = map.Size.z / 2 },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Could not move the camera off the healer.");
            for (int pulse = 1; pulse <= pulses; pulse++)
            {
                Hediff injury = null;
                int callsBefore = await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        healer.pather?.StopDead();
                        target.pather?.StopDead();
                        healer.Position = healerPosition;
                        target.Position = targetPosition;
                        // Create a real injury deterministically. Armor/random hit
                        // selection belongs to a different contract.
                        injury = HediffMaker.MakeHediff(
                            HediffDefOf.Cut,
                            target,
                            target.RaceProps.body.corePart
                        );
                        injury.Severity = 1f;
                        target.health.AddHediff(injury);
                        if (!target.health.hediffSet.hediffs.Contains(injury))
                            throw new InvalidOperationException(
                                "The fixture did not accept its staged injury."
                            );
                        return simulationCalls;
                    },
                    cancellationToken
                );
                // Zombieland's budgeted scheduler runs at TickManagerUpdate,
                // which direct DoSingleTick stepping does not exercise.
                int until = await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        int tick = Find.TickManager.TicksGame;
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                        return tick + ticksPerPulse;
                    },
                    cancellationToken
                );
                bool elapsed = false;
                for (int frame = 0; frame < 1200; frame += 4)
                {
                    await ctx.Game.FramesAsync(4, cancellationToken);
                    elapsed = await ctx.MainThread.InvokeAsync(
                        () => Find.TickManager.TicksGame >= until,
                        cancellationToken
                    );
                    if (elapsed)
                        break;
                }
                await ctx.MainThread.InvokeAsync(
                    () => Find.TickManager.CurTimeSpeed = TimeSpeed.Paused,
                    cancellationToken
                );
                if (!elapsed)
                    throw new InvalidOperationException(
                        "Normal playback did not complete the requested tick window."
                    );
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool healed =
                            !target.Dead && !target.health.hediffSet.hediffs.Contains(injury);
                        bool hidden =
                            !fog.IsShown(Faction.OfPlayer, healer.Position)
                            && !fog.IsShown(Faction.OfPlayer, target.Position);
                        bool simulated =
                            simulationCalls > callsBefore
                            && healer.Spawned
                            && !healer.Dead
                            && target.Spawned
                            && !target.Dead;
                        passed &= healed && hidden && simulated && simulationDraws == 0;
                        rows.Add(
                            new
                            {
                                pulse,
                                healed,
                                hidden,
                                simulated,
                                tick = Find.TickManager.TicksGame,
                                simulationCalls,
                                simulationDraws,
                                records = effects.Count,
                                healerDead = healer.Dead,
                                healerSpawned = healer.Spawned,
                                targetDead = target.Dead,
                                targetSpawned = target.Spawned,
                                steps = effects
                                    .Cast<object>()
                                    .Select(info => (int)step.GetValue(info))
                                    .ToArray(),
                                healerPosition = healer.Position.ToString(),
                                targetPosition = target.Position.ToString(),
                            }
                        );
                    },
                    cancellationToken
                );
                if (healer.Dead || target.Dead)
                    break;
            }
            return new
            {
                passed,
                healerId,
                targetId,
                startTick,
                rows,
                deaths = simulationDeaths.ToArray(),
                suppressedZeroThreatCleanup = true,
                endTick = await ctx.MainThread.InvokeAsync(
                    () => Find.TickManager.TicksGame,
                    cancellationToken
                ),
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
                        simulationHealer = simulationTarget = null;
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        FogSettings.BaseViewRange = oldRange;
                    },
                    CancellationToken.None
                );
                if (testModeStarted)
                    await ctx.Tools.CallAsync(
                        "zombieland/zombie_ticking_test_mode",
                        new { enabled = false },
                        cancellationToken: CancellationToken.None
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
                if (!reload.Succeeded())
                    throw new InvalidOperationException("Fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private static void ObserveSimulation(Pawn __instance)
    {
        if (__instance == simulationHealer)
            simulationCalls++;
    }

    private static void SuppressFixtureCleanup(Pawn __instance, ref float threatLevel)
    {
        if (__instance == simulationHealer || __instance == simulationTarget)
            threatLevel = 1f;
    }

    private static void ObserveSimulationDraw(Pawn __instance, DrawPhase phase, bool __runOriginal)
    {
        if (__instance == simulationHealer && __runOriginal && phase == DrawPhase.Draw)
            simulationDraws++;
    }

    private static void ObserveSimulationDeath(
        Pawn __instance,
        DamageInfo? dinfo,
        Hediff exactCulprit
    )
    {
        if (__instance != simulationHealer && __instance != simulationTarget)
            return;
        simulationDeaths.Add(
            new
            {
                pawn = __instance.ThingID,
                tick = Find.TickManager.TicksGame,
                damage = dinfo?.Def?.defName,
                amount = dinfo?.Amount,
                culprit = exactCulprit?.def?.defName,
                stack = Environment.StackTrace.Split('\n').Take(14).ToArray(),
            }
        );
    }

    [Tool(
        "totalfog/zombieland_healing_visibility",
        Description = "Observe a staged Zombieland healer/target through real observer movement: both visible, visible healer with hidden target, and both hidden. Captures effects, counter text and actual counter-hover targets. Drawing must preserve effect records, age and simulation population. Advances no ticks and restores the fixture."
    )]
    public static async Task<object> HealingVisibility(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string healerId,
        string targetId,
        string evidenceLabel,
        int frames = 12
    )
    {
        if (
            frames < 3
            || frames > 60
            || string.IsNullOrWhiteSpace(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Use a simple screenshot label and 3..60 frames.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.zombieland-effects-probe");
        Pawn healer = null,
            target = null,
            viewer = null;
        Map map = null;
        MapVisibility fog = null;
        IList effects = null;
        object[] originalEffects = null;
        Pawn[] observers = null;
        IntVec3[] positions = null;
        FieldInfo step = null;
        object effect = null;
        IEnumerable readoutZombies = null;
        object zombieTickManager = null;
        MethodInfo populationCount = null;
        int initialPopulation = -1;
        int oldRange = FogSettings.BaseViewRange,
            startTick = -1;
        bool oldFade = SectionLayerFog.PrefEnableFade;
        var rows = new List<State>();
        var pictures = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException(
                            "Load the staged healer fixture first."
                        );
                    fog = map.GetComponent<MapVisibility>();
                    if (
                        !Find.TickManager.Paused
                        || !fog.Initialized
                        || FogSettings.OnlyOutsideColony
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized colony fog fixture, with bypass disabled."
                        );
                    healer = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == healerId);
                    target = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == targetId);
                    if (
                        healer.Faction == Faction.OfPlayer
                        || target.Faction == Faction.OfPlayer
                        || healer.Dead
                        || target.Dead
                    )
                        throw new InvalidOperationException(
                            "Use a living non-player healer and target."
                        );
                    var settings = AccessTools
                        .Field(AccessTools.TypeByName("ZombieLand.ZombieSettings"), "Values")
                        .GetValue(null);
                    if (
                        !(bool)
                            AccessTools
                                .Field(settings.GetType(), "showZombieStats")
                                .GetValue(settings)
                    )
                        throw new InvalidOperationException(
                            "Enable the zombie counter in this fixture."
                        );
                    zombieTickManager = map.components.Single(c =>
                        c.GetType().FullName == "ZombieLand.TickManager"
                    );
                    readoutZombies = (IEnumerable)
                        AccessTools
                            .Field(zombieTickManager.GetType(), "allZombiesCached")
                            .GetValue(zombieTickManager);
                    populationCount = AccessTools.Method(
                        zombieTickManager.GetType(),
                        "ZombieCount"
                    );
                    initialPopulation = (int)populationCount.Invoke(zombieTickManager, null);
                    readoutPoint = Vector2.zero;
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(Widgets),
                            nameof(Widgets.Label),
                            new[] { typeof(Rect), typeof(string) }
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveReadout)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(TargetHighlighter),
                            nameof(TargetHighlighter.Highlight)
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveHighlight)
                        )
                    );
                    effects = (IList)
                        AccessTools.Field(healer.GetType(), "healInfo").GetValue(healer);
                    originalEffects = effects.Cast<object>().ToArray();
                    var infoType = AccessTools.TypeByName("ZombieLand.HealerInfo");
                    effect = Activator.CreateInstance(infoType, target);
                    step = AccessTools.Field(infoType, "step");
                    step.SetValue(effect, 12);
                    effects.Clear();
                    effects.Add(effect);
                    var constants = AccessTools.TypeByName("ZombieLand.Constants");
                    healingMaterials.Clear();
                    foreach (
                        var material in (Material[])
                            AccessTools.Field(constants, "BEING_HEALED").GetValue(null)
                    )
                        healingMaterials.Add(material);
                    endpoint = target.DrawPos + new Vector3(0, 0, .1f);
                    // Ref assemblies expose engine internals, but the live field is
                    // private. Resolve it once instead of emitting an illegal access.
                    cyanLineMaterial = (Material)
                        AccessTools.Field(typeof(GenDraw), "LineMatCyan").GetValue(null);
                    var graphics = AccessTools.TypeByName("ZombieLand.GraphicToolbox");
                    harmony.Patch(
                        AccessTools.Method(graphics, "DrawScaledMesh"),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveGlow)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(GenDraw),
                            nameof(GenDraw.DrawLineBetween),
                            new[]
                            {
                                typeof(Vector3),
                                typeof(Vector3),
                                typeof(Material),
                                typeof(float),
                            }
                        ),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveBeam)
                        )
                    );
                    observers = map
                        .mapPawns.AllPawnsSpawned.Where(p =>
                            p.Faction == Faction.OfPlayer
                            && p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null
                        )
                        .ToArray();
                    positions = observers.Select(p => p.Position).ToArray();
                    viewer = observers.First(p => p.IsColonist);
                    startTick = Find.TickManager.TicksGame;
                    FogSettings.BaseViewRange = 5;
                    SectionLayerFog.PrefEnableFade = false;
                    foreach (var pawn in observers)
                        Move(pawn, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
                    Refresh();
                    // First observe the target so the negative case is explored fog,
                    // where the fog overlay is translucent rather than unexplored black.
                    Move(viewer, target.Position);
                    Refresh();
                    var midpoint = new IntVec3(
                        (healer.Position.x + target.Position.x) / 2,
                        0,
                        (healer.Position.z + target.Position.z) / 2
                    );
                    Move(viewer, midpoint);
                    Refresh();
                    Require(true, true);
                },
                cancellationToken
            );
            await ctx.Tools.CallAsync(
                "rimworld/frame_cell_rect",
                new
                {
                    x = healer.Position.x - 3,
                    z = Math.Min(healer.Position.z, target.Position.z) - 3,
                    width = 7,
                    height = Math.Abs(healer.Position.z - target.Position.z) + 7,
                    rootSize = 12f,
                },
                cancellationToken: cancellationToken
            );
            await Sample("both-visible");
            await Capture("both-visible");
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    bool found = false;
                    foreach (var offset in GenRadial.RadialCellsAround(IntVec3.Zero, 5, true))
                    {
                        var cell = healer.Position + offset;
                        if (!cell.InBounds(map) || !cell.Standable(map))
                            continue;
                        Move(viewer, cell);
                        Refresh();
                        if (Shown(healer) && !Shown(target))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        throw new InvalidOperationException(
                            "No real sight boundary separates this healer/target pair."
                        );
                    Require(true, false);
                },
                cancellationToken
            );
            await Sample("visible-healer-hidden-target");
            await Capture("hidden-target-effect-on");
            await ctx.MainThread.InvokeAsync(() => effects.Clear(), cancellationToken);
            await ctx.Game.FramesAsync(frames, cancellationToken);
            await Capture("hidden-target-effect-off");
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    effects.Add(effect);
                    Move(viewer, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
                    Refresh();
                    Require(false, false);
                },
                cancellationToken
            );
            await Sample("both-hidden");
            await Capture("both-hidden");
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Move(
                        viewer,
                        new IntVec3(
                            (healer.Position.x + target.Position.x) / 2,
                            0,
                            (healer.Position.z + target.Position.z) / 2
                        )
                    );
                    Refresh();
                    Require(true, true);
                    step.SetValue(effect, 60);
                    glows = beams = 0;
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(frames, cancellationToken);
            bool expiredDrawSuppressed = await ctx.MainThread.InvokeAsync(
                () => glows == 0 && beams == 0,
                cancellationToken
            );
            bool rendererReadOnly = await ctx.MainThread.InvokeAsync(
                () =>
                    effects.Count == 1
                    && effects[0] == effect
                    && (int)step.GetValue(effect) == 60
                    && rows.All(r => r.Step == 12),
                cancellationToken
            );
            var endTick = await ctx.MainThread.InvokeAsync(
                () => Find.TickManager.TicksGame,
                cancellationToken
            );
            bool populationUnchanged = await ctx.MainThread.InvokeAsync(
                () => initialPopulation == (int)populationCount.Invoke(zombieTickManager, null),
                cancellationToken
            );
            bool readoutVisibility = rows.All(r =>
                r.DisplayedZombies == r.ExpectedZombies
                && r.HiddenHighlights.Length == 0
                && r.Highlights.OrderBy(id => id)
                    .SequenceEqual(r.ExpectedHighlights.OrderBy(id => id))
            );
            return new
            {
                passed = rows[0].Glows > 0
                    && rows[0].Beams > 0
                    && rows.Skip(1).All(r => r.Glows == 0 && r.Beams == 0)
                    && expiredDrawSuppressed
                    && rendererReadOnly
                    && readoutVisibility
                    && populationUnchanged
                    && startTick == endTick,
                healerId,
                targetId,
                startTick,
                endTick,
                initialPopulation,
                populationUnchanged,
                expiredDrawSuppressed,
                rendererReadOnly,
                readoutVisibility,
                rows,
                pictures,
            };

            bool Shown(Pawn pawn) => fog.IsShown(Faction.OfPlayer, pawn.Position);
            void Require(bool casterVisible, bool targetVisible)
            {
                if (Shown(healer) != casterVisible || Shown(target) != targetVisible)
                    throw new InvalidOperationException(
                        "The fixture does not establish the required native coverage."
                    );
            }
            void Refresh()
            {
                foreach (var source in fog.fowWatchers)
                    source.UpdateFoV(true);
                fog.MapComponentTick();
                map.mapDrawer.RegenerateEverythingNow();
            }
            async Task Sample(string phase)
            {
                await ctx.Tools.CallAsync(
                    "rimworld/clear_hover_target",
                    new { },
                    cancellationToken: cancellationToken
                );
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        glows = beams = readoutCount = 0;
                        readoutHighlights.Clear();
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                var point = await ctx.MainThread.InvokeAsync(() => readoutPoint, cancellationToken);
                if (point != Vector2.zero)
                {
                    var hover = await ctx.Tools.CallAsync(
                        "rimworld/set_hover_target",
                        new
                        {
                            screenX = point.x,
                            screenY = point.y,
                            settleMs = 0,
                            durationMs = 10000,
                        },
                        cancellationToken: cancellationToken
                    );
                    if (!hover.Succeeded())
                        throw new InvalidOperationException(
                            "Could not hover the drawn zombie counter."
                        );
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                }
                rows.Add(
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            var visible = readoutZombies
                                .Cast<Pawn>()
                                .Where(p => p.Spawned && !p.Dead && Visibility.IsVisible(p))
                                .Select(p => p.ThingID)
                                .ToArray();
                            return new State
                            {
                                Phase = phase,
                                HealerVisible = Shown(healer),
                                TargetVisible = Shown(target),
                                TargetHidden = target.TryGetComp<CompFog>().Hiddenable.Hidden,
                                Glows = glows,
                                Beams = beams,
                                Step = (int)step.GetValue(effect),
                                ExpectedZombies = visible.Length,
                                DisplayedZombies = readoutCount,
                                ExpectedHighlights =
                                    visible.Length <= 100 ? visible : Array.Empty<string>(),
                                Highlights = readoutHighlights.ToArray(),
                                HiddenHighlights = readoutHighlights.Except(visible).ToArray(),
                            };
                        },
                        cancellationToken
                    )
                );
                await ctx.Tools.CallAsync(
                    "rimworld/clear_hover_target",
                    new { },
                    cancellationToken: cancellationToken
                );
            }
            async Task Capture(string name)
            {
                var result = await ctx.Tools.CallAsync(
                    "rimworld/take_screenshot",
                    new { fileName = evidenceLabel + "-" + name, includeTargets = false },
                    cancellationToken: cancellationToken
                );
                if (!result.Succeeded())
                    throw new InvalidOperationException("Screenshot capture failed: " + name);
                pictures.Add(result);
            }
        }
        finally
        {
            try
            {
                await ctx.Tools.CallAsync(
                    "rimworld/clear_hover_target",
                    new { },
                    cancellationToken: CancellationToken.None
                );
            }
            finally
            {
                try
                {
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            harmony.UnpatchAll(harmony.Id);
                            healingMaterials.Clear();
                            cyanLineMaterial = null;
                            FogSettings.BaseViewRange = oldRange;
                            SectionLayerFog.PrefEnableFade = oldFade;
                            if (effects != null && originalEffects != null)
                            {
                                effects.Clear();
                                foreach (var item in originalEffects)
                                    effects.Add(item);
                            }
                            if (observers != null && positions != null)
                                for (int i = 0; i < observers.Length; i++)
                                    Move(observers[i], positions[i]);
                            if (fog != null && map != null)
                            {
                                foreach (var source in fog.fowWatchers)
                                    source.UpdateFoV(true);
                                fog.MapComponentTick();
                                map.mapDrawer.RegenerateEverythingNow();
                            }
                        },
                        CancellationToken.None
                    );
                }
                finally
                {
                    gate.Release();
                }
            }
        }
    }

    private sealed class State
    {
        public string Phase;
        public bool HealerVisible,
            TargetVisible,
            TargetHidden;
        public int Glows,
            Beams,
            Step;
        public int ExpectedZombies,
            DisplayedZombies;
        public string[] ExpectedHighlights,
            Highlights,
            HiddenHighlights;
    }

    private static void ObserveReadout(Rect rect, string label)
    {
        const string suffix = " Zombies";
        if (
            label == null
            || !label.EndsWith(suffix, StringComparison.Ordinal)
            || !int.TryParse(label.Substring(0, label.Length - suffix.Length), out int count)
        )
            return;
        readoutCount = count;
        readoutPoint = UI.GUIToScreenPoint(rect.center);
    }

    private static void ObserveHighlight(GlobalTargetInfo target)
    {
        if (target.Thing is Pawn pawn && pawn.GetType().FullName == "ZombieLand.Zombie")
            readoutHighlights.Add(pawn.ThingID);
    }

    private static void Move(Pawn pawn, IntVec3 position)
    {
        pawn.Position = position;
        pawn.TryGetComp<CompFog>().CompTick();
    }

    private static void ObserveGlow(Material mat, Vector3 pos)
    {
        if (healingMaterials.Contains(mat) && (pos - endpoint).sqrMagnitude < .01f)
            glows++;
    }

    private static void ObserveBeam(Vector3 B, Material mat)
    {
        if (mat == cyanLineMaterial && (B - endpoint).sqrMagnitude < .01f)
            beams++;
    }
}
