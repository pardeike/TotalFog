using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using Unity.Collections;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Identical development-only instrumentation for Total Fog and the inherited binary.</summary>
public sealed class PerformanceScenarios
{
    private static RuntimeFrameProbe active;
    private static readonly SemaphoreSlim drawGate = new(1, 1);
    private static DynamicDrawManager.ThingCullDetails[] cullInputs;
    private static List<Thing> cullThings;

    [Tool(
        "totalfog/draw_gate_cost",
        Description = "Capture actual pre-fog native cull inputs on a paused no-Symbiant fixture, then alternate the same filter with its optional provider registered/unregistered. Reuses scratch storage, checks identical output flags and restores registration. Measures filter dispatch cost, not TPS."
    )]
    public static async Task<object> DrawGateCost(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int iterations = 200
    )
    {
        await drawGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.draw-gate-cost");
        MethodInfo filter = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (
                        iterations < 100
                        || iterations > 1000
                        || Find.CurrentMap == null
                        || !Find.TickManager.Paused
                        || active != null
                    )
                        throw new InvalidOperationException(
                            "Use a paused fixture and 100..1000 iterations without another profile."
                        );
                    filter = AccessTools.Method(
                        AccessTools.TypeByName("TotalFog.Presentation.DynamicVisibility"),
                        "ComputeCulledThings_Postfix"
                    );
                    if (filter == null)
                        throw new InvalidOperationException(
                            "Install the candidate renderer first."
                        );
                    cullInputs = null;
                    harmony.Patch(
                        filter,
                        prefix: new HarmonyMethod(
                            typeof(PerformanceScenarios),
                            nameof(CaptureCullInputs)
                        )
                    );
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(3, cancellationToken);
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                {
                    harmony.UnpatchAll(harmony.Id);
                    if (cullInputs == null || cullThings == null)
                        throw new InvalidOperationException("No native cull input was captured.");
                    var type = AccessTools.TypeByName("ZombieLand.ZombieSymbiant");
                    if (type == null || cullThings.Any(thing => thing.GetType() == type))
                        throw new InvalidOperationException(
                            "Use Zombieland without a Symbiant in the draw list."
                        );
                    var registry = (System.Collections.IDictionary)
                        AccessTools
                            .Field(
                                AccessTools.TypeByName(
                                    "TotalFog.Presentation.CustomRenderVisibility"
                                ),
                                "providers"
                            )
                            .GetValue(null);
                    var provider =
                        registry[type]
                        ?? throw new InvalidOperationException(
                            "The optional provider is not registered."
                        );
                    if ((bool)AccessTools.Field(provider.GetType(), "Failed").GetValue(provider))
                        throw new InvalidOperationException(
                            "Do not replace a failed provider during this diagnostic."
                        );
                    var query =
                        (Func<Thing, bool>)
                            AccessTools.Field(provider.GetType(), "Query").GetValue(provider);
                    var run =
                        (Action<NativeArray<DynamicDrawManager.ThingCullDetails>, List<Thing>>)
                            Delegate.CreateDelegate(
                                typeof(Action<
                                    NativeArray<DynamicDrawManager.ThingCullDetails>,
                                    List<Thing>
                                >),
                                filter
                            );
                    using var scratch = new NativeArray<DynamicDrawManager.ThingCullDetails>(
                        cullInputs,
                        Allocator.Temp
                    );
                    int tick = Find.TickManager.TicksGame;
                    var rows = new List<object>();
                    try
                    {
                        for (int warm = 0; warm < 16; warm++)
                        {
                            Reset();
                            run(scratch, cullThings);
                        }
                        for (int sample = 0; sample < 7; sample++)
                        {
                            double registeredMs,
                                commonMs;
                            if (sample % 2 == 0)
                            {
                                registeredMs = Measure(true);
                                commonMs = Measure(false);
                            }
                            else
                            {
                                commonMs = Measure(false);
                                registeredMs = Measure(true);
                            }
                            rows.Add(
                                new
                                {
                                    sample,
                                    registeredMs,
                                    commonMs,
                                }
                            );
                        }
                        return new
                        {
                            success = tick == Find.TickManager.TicksGame,
                            filterOnly = true,
                            iterations,
                            drawThings = cullThings.Count,
                            nativeEntries = cullInputs.Count(entry =>
                                entry.shouldDraw || entry.shouldDrawShadow
                            ),
                            startTick = tick,
                            endTick = Find.TickManager.TicksGame,
                            identicalOutputFlags = true,
                            samples = rows,
                        };
                    }
                    finally
                    {
                        Visibility.RegisterRenderer(type, query);
                    }

                    void Reset() =>
                        NativeArray<DynamicDrawManager.ThingCullDetails>.Copy(cullInputs, scratch);
                    double Measure(bool registered)
                    {
                        Visibility.RegisterRenderer(type, registered ? query : null);
                        Reset();
                        run(scratch, cullThings);
                        var expected = scratch.ToArray();
                        var clock = Stopwatch.StartNew();
                        for (int i = 0; i < iterations; i++)
                        {
                            Reset();
                            run(scratch, cullThings);
                        }
                        clock.Stop();
                        for (int i = 0; i < expected.Length; i++)
                            if (
                                expected[i].shouldDraw != scratch[i].shouldDraw
                                || expected[i].shouldDrawShadow != scratch[i].shouldDrawShadow
                            )
                                throw new InvalidOperationException(
                                    "Cull inputs changed within the paused batch."
                                );
                        // The two paths must produce the same result, not only stable individual batches.
                        Visibility.RegisterRenderer(type, registered ? null : query);
                        Reset();
                        run(scratch, cullThings);
                        for (int i = 0; i < expected.Length; i++)
                            if (
                                expected[i].shouldDraw != scratch[i].shouldDraw
                                || expected[i].shouldDrawShadow != scratch[i].shouldDrawShadow
                            )
                                throw new InvalidOperationException(
                                    "Unregistering changed ordinary renderer flags."
                                );
                        return clock.Elapsed.TotalMilliseconds / iterations;
                    }
                },
                cancellationToken
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
                        cullInputs = null;
                        cullThings = null;
                    },
                    CancellationToken.None
                );
            }
            finally
            {
                drawGate.Release();
            }
        }
    }

    private static void CaptureCullInputs(
        NativeArray<DynamicDrawManager.ThingCullDetails> __0,
        List<Thing> __1
    )
    {
        if (cullInputs != null)
            return;
        cullInputs = __0.ToArray();
        cullThings = new List<Thing>(__1);
    }

    [Tool(
        "totalfog/zombieland_readout_cost",
        Description = "Measure the installed Zombieland counter query against its unchanged simulation population query on a paused fixture. Seven alternating samples, no GUI or game ticks, no world/settings mutation. Reports query cost, not whole-game performance."
    )]
    public static Task<object> ZombielandReadoutCost(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int iterations = 200
    ) =>
        ctx.MainThread.InvokeAsync<object>(
            () =>
            {
                if (
                    iterations < 100
                    || iterations > 1000
                    || Find.CurrentMap == null
                    || !Find.TickManager.Paused
                )
                    throw new InvalidOperationException(
                        "Use a paused Zombieland fixture and 100..1000 iterations."
                    );
                var manager = Find.CurrentMap.components.Single(c =>
                    c.GetType().FullName == "ZombieLand.TickManager"
                );
                var support = AccessTools.TypeByName("ZombieLand.TotalFogSupport");
                var visibleMethod = AccessTools.DeclaredMethod(support, "VisibleZombieCount");
                if (visibleMethod == null)
                    throw new InvalidOperationException(
                        "Install the counter visibility fix first."
                    );
                var population =
                    (Func<int>)
                        Delegate.CreateDelegate(
                            typeof(Func<int>),
                            manager,
                            AccessTools.Method(manager.GetType(), "ZombieCount")
                        );
                var visible =
                    (Func<int>)Delegate.CreateDelegate(typeof(Func<int>), manager, visibleMethod);
                int startTick = Find.TickManager.TicksGame;
                int total = population(),
                    displayed = visible();
                for (int warm = 0; warm < 16; warm++)
                {
                    population();
                    visible();
                }
                var samples = new List<object>();
                for (int sample = 0; sample < 7; sample++)
                {
                    double populationMs,
                        readoutMs;
                    if (sample % 2 == 0)
                    {
                        populationMs = Measure(population, total);
                        readoutMs = Measure(visible, displayed);
                    }
                    else
                    {
                        readoutMs = Measure(visible, displayed);
                        populationMs = Measure(population, total);
                    }
                    samples.Add(
                        new
                        {
                            sample,
                            populationMs,
                            readoutMs,
                        }
                    );
                }
                return new
                {
                    success = startTick == Find.TickManager.TicksGame
                        && total == population()
                        && displayed == visible(),
                    queryOnly = true,
                    iterations,
                    population = total,
                    displayed,
                    startTick,
                    endTick = Find.TickManager.TicksGame,
                    samples,
                    zombieMvid = manager
                        .GetType()
                        .Assembly.ManifestModule.ModuleVersionId.ToString(),
                };

                double Measure(Func<int> read, int expected)
                {
                    int checksum = 0;
                    var clock = Stopwatch.StartNew();
                    for (int i = 0; i < iterations; i++)
                        checksum += read();
                    clock.Stop();
                    if (checksum != iterations * expected)
                        throw new InvalidOperationException(
                            "Counter inputs changed during the paused sample."
                        );
                    return clock.Elapsed.TotalMilliseconds / iterations;
                }
            },
            cancellationToken
        );

    [Tool(
        "totalfog/dpa_playback",
        Description = "Reset an already-patched DPA session, play a paused fixture, capture the snapshot immediately, then stop and request cleanup. Avoids paused frames accumulating between separate external tool calls."
    )]
    public static async Task<object> DpaPlayback(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int durationMs = 8000,
        string speed = "Ultrafast"
    )
    {
        var status = await ctx.Tools.CallAsync(
            "rimworld/dpa_status",
            new { includePresets = false },
            cancellationToken: cancellationToken
        );
        if (
            !status.Succeeded()
            || !status.ReadResult<bool>("dpa", "currentlyProfiling")
            || !status.ReadResult<bool>("dpa", "isPatched")
        )
            return new
            {
                success = false,
                stage = "preflight",
                error = "Patch DPA targets before capturing playback.",
            };
        try
        {
            var reset = await ctx.Tools.CallAsync(
                "rimworld/dpa_reset",
                new { },
                cancellationToken: cancellationToken
            );
            if (!reset.Succeeded())
                return new
                {
                    success = false,
                    stage = "reset",
                    reset.Error,
                };
            var playback = await RuntimePerformance(
                ctx,
                cancellationToken,
                durationMs,
                speed,
                speed != "Normal"
            );
            if (!(bool)playback.GetType().GetProperty("success").GetValue(playback))
                return new
                {
                    success = false,
                    stage = "playback",
                    playback,
                };
            var snapshot = await ctx.Tools.CallAsync(
                "rimworld/dpa_snapshot",
                new { sortBy = "total", limit = 30 },
                cancellationToken: cancellationToken
            );
            return new
            {
                success = snapshot.Succeeded(),
                playback,
                snapshot = snapshot.Result,
                snapshot.Error,
            };
        }
        finally
        {
            var stop = await ctx.Tools.CallAsync(
                "rimworld/dpa_stop",
                new { },
                cancellationToken: CancellationToken.None
            );
            var cleanup = await ctx.Tools.CallAsync(
                "rimworld/dpa_cleanup",
                new { },
                cancellationToken: CancellationToken.None
            );
            if (!stop.Succeeded() || !cleanup.Succeeded())
                throw new InvalidOperationException(
                    "DPA stop or cleanup failed; check native DPA status before another sample."
                );
        }
    }

    [Tool(
        "totalfog/map_lookup_cost",
        Description = "Compare the actual game's component-list search with Total Fog's cached lookup on the current paused map. No gameplay state is changed."
    )]
    public static async Task<object> MapLookupCost(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int iterations = 500000
    )
    {
        if (iterations < 10000 || iterations > 1000000)
            throw new ArgumentOutOfRangeException(nameof(iterations));
        return await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map =
                    Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
                if (!Find.TickManager.Paused)
                    throw new InvalidOperationException("Pause the map first.");
                var expected = map.GetComponent<MapVisibility>();
                if (expected == null)
                    throw new InvalidOperationException("Total Fog must be loaded.");
                for (int i = 0; i < 10000; i++)
                    map.GetVisibility();
                double Measure(bool cached)
                {
                    int matches = 0;
                    long start = Stopwatch.GetTimestamp();
                    if (cached)
                        for (int i = 0; i < iterations; i++)
                        {
                            if (ReferenceEquals(expected, map.GetVisibility()))
                                matches++;
                        }
                    else
                        for (int i = 0; i < iterations; i++)
                        {
                            if (ReferenceEquals(expected, map.GetComponent<MapVisibility>()))
                                matches++;
                        }
                    if (matches != iterations)
                        throw new InvalidOperationException("Lookup returned another component.");
                    return (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
                }
                var cachedMs = new double[3];
                var engineMs = new double[3];
                for (int i = 0; i < 3; i++)
                    if (i % 2 == 0)
                    {
                        engineMs[i] = Measure(false);
                        cachedMs[i] = Measure(true);
                    }
                    else
                    {
                        cachedMs[i] = Measure(true);
                        engineMs[i] = Measure(false);
                    }
                return new
                {
                    success = true,
                    iterations,
                    components = map.components.Count,
                    cachedMs,
                    engineMs,
                };
            },
            cancellationToken
        );
    }

    [Tool(
        "totalfog/load_comparison_save",
        Description = "Load a native comparison save visually ready and paused from its first tick. Temporarily enables RimWorld's Pause on load preference, restores it without saving preferences, and returns the native load result. Does not edit the save or simulation."
    )]
    public static async Task<object> LoadComparisonSave(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var watch = Stopwatch.StartNew();
        bool previousPauseOnLoad = await ctx.MainThread.InvokeAsync(
            () =>
            {
                bool previous = Prefs.PauseOnLoad;
                Prefs.PauseOnLoad = true;
                return previous;
            },
            cancellationToken
        );
        object load;
        try
        {
            var result = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName,
                    readiness = "visual",
                    pauseIfNeeded = true,
                    ignoreModCompatibility = true,
                },
                cancellationToken: cancellationToken
            );
            if (!result.Succeeded())
                throw new InvalidOperationException("Comparison load failed: " + result.Error);
            load = result.Result;
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () => Prefs.PauseOnLoad = previousPauseOnLoad,
                CancellationToken.None
            );
        }
        return await ctx.MainThread.InvokeAsync(
            () =>
                (object)
                    new
                    {
                        load,
                        loadMs = watch.ElapsedMilliseconds,
                        previousPauseOnLoad,
                        restoredPauseOnLoad = Prefs.PauseOnLoad,
                    },
            cancellationToken
        );
    }

    [Tool(
        "totalfog/runtime_performance",
        Description = "Measure actual native playback, whole-tick elapsed time and frame intervals. Works with the inherited binary too. Optional wideView frames the map center at root size 100 through the bridge's session-only zoom extension and centers the sparse contamination input there. Records actual visible map cells and pawn root cells in view. Restores camera/extension, overlay and instrumentation. Reload the same save before each comparison sample."
    )]
    public static async Task<object> RuntimePerformance(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int durationMs = 15000,
        string speed = "Normal",
        bool forceRequestedSpeed = false,
        bool profileFog = false,
        bool profileZombieWork = false,
        int warmupTicks = 0,
        bool traceSlowdowns = false,
        bool contaminationOverlay = false,
        bool wideView = false
    )
    {
        if (durationMs < 1000 || durationMs > 30000)
            throw new ArgumentOutOfRangeException(nameof(durationMs));
        if (warmupTicks < 0 || warmupTicks > 600)
            throw new ArgumentOutOfRangeException(nameof(warmupTicks));
        await drawGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.performance-probe");
        RuntimeFrameProbe probe = null;
        object environment = null,
            zombieWork = null,
            wideScreenshot = null;
        Type zombieType = null;
        int zombiePopulationStart = 0;
        int warmupStartTick = -1,
            warmupEndTick = -1;
        Func<Map, object> startZombieWork = null,
            stopZombieWork = null,
            cancelZombieWork = null;
        bool zombieWorkStarted = false;
        Map telemetryMap = null;
        int[] collections = null;
        AutomaticPauseMode? previousAutomaticPause = null;
        FieldInfo overlayField = null;
        object overlayManager = null;
        bool previousOverlay = false;
        float[] groundCells = null,
            previousGround = null;
        bool? previousZoomExtension = null;
        Vector3 previousCameraPosition = default;
        float previousCameraSize = 0;
        int groundX = 40,
            groundZ = 40;
        try
        {
            if (wideView)
            {
                var center = await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (Find.CurrentMap == null || !Find.TickManager.Paused || active != null)
                            throw new InvalidOperationException(
                                "Use a paused comparison map without another performance sample."
                            );
                        var priorCell = Find.CameraDriver.MapPosition;
                        previousCameraPosition = new Vector3(priorCell.x, 0f, priorCell.z);
                        previousCameraSize = Find.CameraDriver.RootSize;
                        var cell = Find.CurrentMap.Center;
                        groundX = cell.x - 79;
                        groundZ = cell.z - 49;
                        return cell;
                    },
                    cancellationToken
                );
                var extended = await ctx.Tools.CallAsync(
                    "rimworld/set_camera_zoom_extension",
                    new { enabled = true },
                    cancellationToken: cancellationToken
                );
                if (!extended.Succeeded() || !extended.ReadResult<bool>("success"))
                    throw new InvalidOperationException(
                        "The bridge camera extension could not be enabled."
                    );
                previousZoomExtension = extended.ReadResult<bool>("previousEnabled");
                var framed = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = center.x,
                        z = center.z,
                        width = 1,
                        height = 1,
                        rootSize = 100f,
                    },
                    cancellationToken: cancellationToken
                );
                if (!framed.Succeeded() || !framed.ReadResult<bool>("success"))
                    throw new InvalidOperationException(
                        "The wide camera could not be established."
                    );
                await ctx.Game.FramesAsync(65, cancellationToken);
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (active != null)
                        throw new InvalidOperationException(
                            "A performance sample is already running."
                        );
                    if (Find.CurrentMap == null || !Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Load the comparison save and pause first."
                        );
                    if (contaminationOverlay)
                    {
                        var managerType =
                            AccessTools.TypeByName("ZombieLand.ContaminationManager")
                            ?? throw new InvalidOperationException(
                                "The contamination comparison requires Zombieland."
                            );
                        var constants = AccessTools.TypeByName("ZombieLand.Constants");
                        if (
                            !(bool)AccessTools.Field(constants, "CONTAMINATION").GetValue(null)
                            || Find.CameraDriver.CurrentViewRect.Area < 6400
                        )
                            throw new InvalidOperationException(
                                "Enable contamination and frame the complete sparse shape at distant zoom."
                            );
                        overlayManager = AccessTools
                            .PropertyGetter(managerType, "Instance")
                            .Invoke(null, null);
                        overlayField = AccessTools.Field(managerType, "showContaminationOverlay");
                        previousOverlay = (bool)overlayField.GetValue(overlayManager);
                        var map = Find.CurrentMap;
                        var grid = AccessTools
                            .Method(managerType, "GetOrCreateGrounds")
                            .Invoke(overlayManager, new object[] { map });
                        groundCells = (float[])
                            AccessTools.Field(grid.GetType(), "cells").GetValue(grid);
                        previousGround = (float[])groundCells.Clone();
                        var setter = AccessTools.PropertySetter(grid.GetType(), "Item");
                        overlayField.SetValue(overlayManager, true);
                        // Same predeclared sparse ground input on both fog variants.
                        // Gameplay visibility policies remain active; no settings or save writes.
                        for (int i = 0; i < 4000; i++)
                        {
                            var cell = new IntVec3(groundX + i % 80 * 2, 0, groundZ + i / 80 * 2);
                            if (
                                !cell.InBounds(map)
                                || !Find.CameraDriver.CurrentViewRect.Contains(cell)
                            )
                                throw new InvalidOperationException(
                                    "The complete 159x99 contamination shape must be in view."
                                );
                            setter.Invoke(grid, new object[] { cell, .65f });
                        }
                    }
                    if (profileZombieWork)
                    {
                        var type =
                            AccessTools.TypeByName("ZombieLand.ZombieTickingTelemetry")
                            ?? throw new InvalidOperationException(
                                "Zombieland ticking telemetry is unavailable."
                            );
                        if ((bool)AccessTools.Property(type, "Enabled").GetValue(null))
                            throw new InvalidOperationException(
                                "Do not replace another active zombie telemetry sample."
                            );
                        startZombieWork = Bind("Start");
                        stopZombieWork = Bind("Stop");
                        cancelZombieWork = Bind("Cancel");
                        Func<Map, object> Bind(string name) =>
                            (Func<Map, object>)
                                Delegate.CreateDelegate(
                                    typeof(Func<Map, object>),
                                    AccessTools.DeclaredMethod(type, name, new[] { typeof(Map) })
                                );
                    }
                    warmupStartTick = Find.TickManager.TicksGame;
                    previousAutomaticPause = Prefs.AutomaticPauseMode;
                    Prefs.AutomaticPauseMode = AutomaticPauseMode.Never;
                },
                cancellationToken
            );
            if (contaminationOverlay)
                await ctx.Game.FramesAsync(65, cancellationToken);
            if (wideView)
            {
                var captured = await ctx.Tools.CallAsync(
                    "rimworld/take_screenshot",
                    new
                    {
                        fileName = "TotalFogWideView-"
                            + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
                        includeTargets = false,
                        suppressMessage = true,
                    },
                    cancellationToken: cancellationToken
                );
                if (!captured.Succeeded() || !captured.ReadResult<bool>("success"))
                    throw new InvalidOperationException("The paused wide-view screenshot failed.");
                wideScreenshot = captured.Result;
            }
            if (warmupTicks > 0)
                await ctx.Game.StepTicksAsync(warmupTicks, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    warmupEndTick = Find.TickManager.TicksGame;
                    if (!Find.TickManager.Paused || warmupEndTick - warmupStartTick != warmupTicks)
                        throw new InvalidOperationException(
                            "Warmup must advance exactly the declared native tick count and pause."
                        );
                    var mod = AppDomain
                        .CurrentDomain.GetAssemblies()
                        .Single(a => a.GetName().Name is "TotalFog" or "rimworld-mod-real-fow");
                    string root = mod.GetName().Name == "TotalFog" ? "TotalFog" : "RimWorldRealFoW";
                    var settings = mod.GetType(
                        root + (root == "TotalFog" ? ".FogSettings" : ".RfowSettings")
                    );
                    var effectiveSettings = new Dictionary<string, string>();
                    foreach (
                        var name in new[]
                        {
                            "BaseViewRange",
                            "BaseHearingRange",
                            "BuildingVisionModifier",
                            "TurretVisionModifier",
                            "AnimalVisionModifier",
                            "AISmart",
                            "NeedWatcher",
                            "OnlyOutsideColony",
                            "PrisonerGiveVision",
                            "AllyGiveVision",
                            "MapRevealAtStart",
                            "TreesBlockSight",
                            "ClearFogDuringTargeting",
                            "DoAudioCheck",
                            "AudioSourceRange",
                            "VolumeMufflingModifier",
                            "WildLifeTabVisible",
                            "fogFadeSpeed",
                            "fogAlpha",
                            "HideSpeakBubble",
                            "HideThreatBig",
                            "HideThreatSmall",
                            "HideEventPositive",
                            "HideEventNegative",
                            "HideEventNeutral",
                            "DelayAlertsUntilSeen",
                        }
                    )
                    {
                        var field = AccessTools.Field(settings, name);
                        var property = field == null ? AccessTools.Property(settings, name) : null;
                        if (field == null && property == null)
                            throw new InvalidOperationException(
                                "Missing comparison setting: " + name
                            );
                        effectiveSettings.Add(
                            name,
                            Convert.ToString(
                                field != null ? field.GetValue(null) : property.GetValue(null),
                                System.Globalization.CultureInfo.InvariantCulture
                            )
                        );
                    }
                    // The exact inherited CompTick always runs hearing checks when
                    // its common range/capacity gates permit them; it has no toggle.
                    // Include that fixed policy so comparisons reject a candidate
                    // with its new hearing-cue option disabled.
                    effectiveSettings.Add(
                        "ShowHearingCues",
                        Convert.ToString(
                            root == "TotalFog"
                                ? (bool)
                                    AccessTools.Field(settings, "ShowHearingCues").GetValue(null)
                                : true,
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    );
                    // The inherited binary has no always-silent arrival switch.
                    // Match its native arrivals rather than timing a different policy.
                    effectiveSettings.Add(
                        "SilentRaids",
                        Convert.ToString(
                            root == "TotalFog"
                                && (bool)AccessTools.Field(settings, "SilentRaids").GetValue(null),
                            System.Globalization.CultureInfo.InvariantCulture
                        )
                    );
                    var zombieMod = AppDomain
                        .CurrentDomain.GetAssemblies()
                        .SingleOrDefault(a => a.GetName().Name == "ZombieLand");
                    zombieType = zombieMod?.GetType("ZombieLand.Zombie");
                    zombiePopulationStart = CountSpawnedZombies(Find.CurrentMap, zombieType);
                    var zombieSettings = zombieMod?.GetType("ZombieLand.ZombieSettings");
                    var zombieValues =
                        zombieSettings == null
                            ? null
                            : AccessTools.Field(zombieSettings, "Values").GetValue(null);
                    var ceMod = AppDomain
                        .CurrentDomain.GetAssemblies()
                        .SingleOrDefault(a => a.GetName().Name == "CombatExtended");
                    var ceValues =
                        ceMod == null
                            ? null
                            : AccessTools
                                .Field(ceMod.GetType("CombatExtended.Controller"), "settings")
                                .GetValue(null);
                    if (ceMod != null && ceValues == null)
                        throw new InvalidOperationException(
                            "Combat Extended settings are unavailable for comparison."
                        );
                    var view = Find.CameraDriver.CurrentViewRect;
                    environment = new
                    {
                        assembly = mod.GetName().Name,
                        mod.Location,
                        mvid = mod.ManifestModule.ModuleVersionId,
                        zombieAssembly = zombieMod == null
                            ? null
                            : new
                            {
                                zombieMod.Location,
                                mvid = zombieMod.ManifestModule.ModuleVersionId,
                                scalarSettings = ScalarSettings(zombieValues),
                            },
                        combatExtendedAssembly = ceMod == null
                            ? null
                            : new
                            {
                                ceMod.Location,
                                mvid = ceMod.ManifestModule.ModuleVersionId,
                                scalarSettings = ScalarSettings(ceValues),
                            },
                        camera = new
                        {
                            view.minX,
                            view.minZ,
                            view.maxX,
                            view.maxZ,
                            zoom = Find.CameraDriver.CurrentZoom.ToString(),
                            rootSize = Find.CameraDriver.RootSize,
                            visibleMapCells = Math.Max(
                                0,
                                Math.Min(view.maxX, Find.CurrentMap.Size.x - 1)
                                    - Math.Max(view.minX, 0)
                                    + 1
                            )
                                * Math.Max(
                                    0,
                                    Math.Min(view.maxZ, Find.CurrentMap.Size.z - 1)
                                        - Math.Max(view.minZ, 0)
                                        + 1
                                ),
                            pawnRootCellsInView = Find.CurrentMap.mapPawns.AllPawnsSpawned.Count(
                                p => view.Contains(p.Position)
                            ),
                            zombieRootCellsInView = zombieType == null
                                ? 0
                                : Find.CurrentMap.mapPawns.AllPawnsSpawned.Count(p =>
                                    !p.Dead
                                    && zombieType.IsInstanceOfType(p)
                                    && view.Contains(p.Position)
                                ),
                        },
                        gameVersion = RimWorld.VersionControl.CurrentVersionStringWithRev,
                        engineMvid = typeof(Pawn).Assembly.ManifestModule.ModuleVersionId,
                        runtimeNamespace = root,
                        starterTypeName = root == "TotalFog" ? "TotalFogMod" : "RealFoWModStarter",
                        mapX = Find.CurrentMap.Size.x,
                        mapZ = Find.CurrentMap.Size.z,
                        pawns = Find.CurrentMap.mapPawns.AllPawnsSpawned.Count,
                        Screen.width,
                        Screen.height,
                        QualitySettings.vSyncCount,
                        Application.targetFrameRate,
                        Application.isFocused,
                        renderer = SystemInfo.graphicsDeviceType.ToString(),
                        cpu = SystemInfo.processorType,
                        effectiveSettings,
                        automaticPauseBefore = previousAutomaticPause.Value.ToString(),
                        wideView,
                        contaminationOverlay = contaminationOverlay,
                        contaminationInput = contaminationOverlay
                            ? new
                            {
                                count = 4000,
                                x = groundX,
                                z = groundZ,
                                width = 80,
                                height = 50,
                                stride = 2,
                                value = .65f,
                            }
                            : null,
                    };
                    // A discovered ancient danger is a legitimate major-threat pause.
                    // Timing needs an uninterrupted interval; restore the preference
                    // afterward and keep every letter/notification in the game.
                    probe = new GameObject(
                        "TotalFogPerformanceProbe"
                    ).AddComponent<RuntimeFrameProbe>();
                    active = probe;
                    harmony.Patch(
                        AccessTools.Method(typeof(TickManager), nameof(TickManager.DoSingleTick)),
                        prefix: new HarmonyMethod(typeof(PerformanceScenarios), nameof(TickBegin)),
                        postfix: new HarmonyMethod(typeof(PerformanceScenarios), nameof(TickEnd))
                    );
                    if (traceSlowdowns)
                        foreach (
                            string method in new[]
                            {
                                nameof(TimeSlower.SignalForceNormalSpeed),
                                nameof(TimeSlower.SignalForceNormalSpeedShort),
                            }
                        )
                            harmony.Patch(
                                AccessTools.Method(typeof(TimeSlower), method),
                                prefix: new HarmonyMethod(
                                    typeof(PerformanceScenarios),
                                    nameof(SlowdownRequested)
                                )
                            );
                    if (profileFog)
                        foreach (
                            var target in new[]
                            {
                                ("Verse.Map", "MapUpdate"),
                                ("Verse.MapDrawer", "DrawMapMesh"),
                                ("Verse.DynamicDrawManager", "DrawDynamicThings"),
                                ("TotalFog.CompFog", "CompTick"),
                                ("TotalFog.CompSightSource", "UpdateFoV"),
                                ("TotalFog.CompSightSource", "CalcPawnSightRange"),
                                ("TotalFog.MapVisibility", "MapComponentTick"),
                                ("TotalFog.MapVisibility", "VisibilityChanged"),
                                ("TotalFog.CompVisibility", "UpdateVisibility"),
                                ("TotalFog.Presentation.ThingVisibility", "IsVisible"),
                                ("TotalFog.FogMapUtility", "GetVisibility"),
                                ("TotalFog.SectionLayerFog", "Regenerate"),
                                ("TotalFog.SectionLayerFog", "DrawLayer"),
                                (
                                    "TotalFog.Detours.Verb",
                                    "CanHitCellFromCellIgnoringRange_Postfix"
                                ),
                                ("TotalFog.Core.FieldOfView", "Compute"),
                                ("TotalFog.Core.FieldOfView", "ComputeMask"),
                                ("TotalFog.Core.VisibilityMask", "ApplyDifference"),
                            }
                        )
                        {
                            string name = target.Item1;
                            if (root != "TotalFog")
                                name = name.Replace(
                                        "TotalFog.CompFog",
                                        "RimWorldRealFoW.CompMainComponent"
                                    )
                                    .Replace(
                                        "TotalFog.CompSightSource",
                                        "RimWorldRealFoW.CompFieldOfViewWatcher"
                                    )
                                    .Replace(
                                        "TotalFog.MapVisibility",
                                        "RimWorldRealFoW.MapComponentSeenFog"
                                    )
                                    .Replace(
                                        "TotalFog.CompVisibility",
                                        "RimWorldRealFoW.CompHideFromPlayer"
                                    )
                                    .Replace(
                                        "TotalFog.Presentation",
                                        "RimWorldRealFoW.Presentation"
                                    )
                                    .Replace("TotalFog.Detours", "RimWorldRealFoW.Detours");
                            var type = mod.GetType(name) ?? typeof(Map).Assembly.GetType(name);
                            var method =
                                type == null ? null : AccessTools.Method(type, target.Item2);
                            if (method == null)
                                continue;
                            probe.Counters.Add(method, new MethodCounter());
                            harmony.Patch(
                                method,
                                prefix: new HarmonyMethod(
                                    typeof(PerformanceScenarios),
                                    nameof(MethodBegin)
                                ),
                                postfix: new HarmonyMethod(
                                    typeof(PerformanceScenarios),
                                    nameof(MethodEnd)
                                )
                            );
                        }
                    if (profileZombieWork)
                    {
                        telemetryMap = Find.CurrentMap;
                        var zombie = AccessTools.TypeByName("ZombieLand.Zombie");
                        harmony.Patch(
                            AccessTools.DeclaredMethod(zombie, nameof(Pawn.Kill)),
                            prefix: new HarmonyMethod(
                                typeof(PerformanceScenarios),
                                nameof(ZombieKilled)
                            )
                        );
                        harmony.Patch(
                            AccessTools.DeclaredMethod(zombie, nameof(Pawn.DeSpawn)),
                            prefix: new HarmonyMethod(
                                typeof(PerformanceScenarios),
                                nameof(ZombieDespawned)
                            )
                        );
                        startZombieWork(telemetryMap);
                        zombieWorkStarted = true;
                    }
                    collections = new[]
                    {
                        GC.CollectionCount(0),
                        GC.CollectionCount(1),
                        GC.CollectionCount(2),
                    };
                },
                cancellationToken
            );
            var playback = await ctx.Tools.CallAsync(
                "rimworld/play_for",
                new
                {
                    durationMs,
                    speed,
                    forceRequestedSpeed,
                    pollIntervalMs = 100,
                },
                cancellationToken: cancellationToken
            );
            return await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (zombieWorkStarted)
                    {
                        zombieWork = stopZombieWork(telemetryMap);
                        zombieWorkStarted = false;
                        if (!(bool)zombieWork.GetType().GetProperty("success").GetValue(zombieWork))
                            throw new InvalidOperationException(
                                "Zombie telemetry did not finish on its original map."
                            );
                    }
                    return new
                    {
                        success = playback.Succeeded()
                            && probe.TickCount > 0
                            && probe.FrameCount > 0
                            && !probe.Overflow
                            && (
                                !contaminationOverlay || (bool)overlayField.GetValue(overlayManager)
                            ),
                        environment,
                        wideScreenshot,
                        playback = playback.Result,
                        tickTiming = "Stopwatch elapsed, including scheduling delays",
                        tickCpuMs = Summarize(probe.Ticks, probe.TickCount),
                        frameIntervalMs = Summarize(probe.Frames, probe.FrameCount),
                        probe.Overflow,
                        probe.FocusLost,
                        timeSpeedSamples = probe
                            .TimeSpeedTicks.Select(
                                (ticks, value) =>
                                    new { speed = ((TimeSpeed)value).ToString(), ticks }
                            )
                            .Where(sample => sample.ticks > 0)
                            .ToArray(),
                        tickRateSamples = probe
                            .TickRateTicks.Select(sample => new
                            {
                                multiplier = sample.Key,
                                ticks = sample.Value,
                            })
                            .OrderBy(sample => sample.multiplier)
                            .ToArray(),
                        profileFog,
                        profileZombieWork,
                        zombieWork,
                        traceSlowdowns,
                        slowdownRequests = probe.Slowdowns,
                        zombiePopulation = zombieType == null
                            ? null
                            : new
                            {
                                start = zombiePopulationStart,
                                end = CountSpawnedZombies(Find.CurrentMap, zombieType),
                            },
                        zombieRemovals = profileZombieWork
                            ? new
                            {
                                probe.Kills,
                                probe.LivingDespawns,
                                probe.DeadDespawns,
                                probe.RemovalExamples,
                            }
                            : null,
                        warmup = new
                        {
                            requestedTicks = warmupTicks,
                            startTick = warmupStartTick,
                            endTick = warmupEndTick,
                        },
                        inclusiveMethods = probe
                            .Counters.Select(p => new
                            {
                                method = p.Key.DeclaringType.FullName + "." + p.Key.Name,
                                p.Value.Calls,
                                milliseconds = p.Value.Elapsed * 1000d / Stopwatch.Frequency,
                            })
                            .ToArray(),
                        collections = new[]
                        {
                            GC.CollectionCount(0) - collections[0],
                            GC.CollectionCount(1) - collections[1],
                            GC.CollectionCount(2) - collections[2],
                        },
                    };
                },
                cancellationToken
            );
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (zombieWorkStarted)
                            cancelZombieWork(telemetryMap);
                        if (overlayField != null)
                        {
                            overlayField.SetValue(overlayManager, previousOverlay);
                            if (previousGround != null)
                                Array.Copy(previousGround, groundCells, groundCells.Length);
                        }
                        if (previousAutomaticPause.HasValue)
                        {
                            Prefs.AutomaticPauseMode = previousAutomaticPause.Value;
                            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        }
                        if (probe != null)
                        {
                            harmony.UnpatchAll(harmony.Id);
                            if (active == probe)
                                active = null;
                            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                            UnityEngine.Object.Destroy(probe.gameObject);
                        }
                        if (previousZoomExtension.HasValue)
                            Find.CameraDriver.SetRootPosAndSize(
                                previousCameraPosition,
                                previousCameraSize
                            );
                    },
                    CancellationToken.None
                );
            }
            finally
            {
                try
                {
                    if (previousZoomExtension.HasValue)
                    {
                        var restored = await ctx.Tools.CallAsync(
                            "rimworld/set_camera_zoom_extension",
                            new { enabled = previousZoomExtension.Value },
                            cancellationToken: CancellationToken.None
                        );
                        if (!restored.Succeeded() || !restored.ReadResult<bool>("success"))
                            throw new InvalidOperationException(
                                "The prior camera zoom extension was not restored."
                            );
                    }
                }
                finally
                {
                    drawGate.Release();
                }
            }
        }
    }

    private static int CountSpawnedZombies(Map map, Type zombieType)
    {
        if (map == null || zombieType == null)
            return 0;
        int count = 0;
        var pawns = map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < pawns.Count; i++)
            if (!pawns[i].Dead && zombieType.IsInstanceOfType(pawns[i]))
                count++;
        return count;
    }

    private static Dictionary<string, string> ScalarSettings(object settings)
    {
        if (settings == null)
            return null;
        return settings
            .GetType()
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(field =>
                field.FieldType.IsPrimitive
                || field.FieldType.IsEnum
                || field.FieldType == typeof(string)
            )
            .ToDictionary(
                field => field.Name,
                field =>
                    Convert.ToString(
                        field.GetValue(settings),
                        System.Globalization.CultureInfo.InvariantCulture
                    )
            );
    }

    private static void ZombieKilled(Pawn __instance, DamageInfo? __0, Hediff __1)
    {
        if (active == null)
            return;
        string cause = __0?.Def?.defName ?? __1?.def?.defName ?? "no supplied cause";
        active.Kills.TryGetValue(cause, out int count);
        active.Kills[cause] = count + 1;
        CaptureRemovalExample("kill:" + cause, __instance);
    }

    private static void ZombieDespawned(Pawn __instance)
    {
        if (active == null)
            return;
        if (__instance.Dead)
            active.DeadDespawns++;
        else
        {
            active.LivingDespawns++;
            CaptureRemovalExample("living despawn", __instance);
        }
    }

    private static void CaptureRemovalExample(string cause, Pawn pawn)
    {
        var examples = active.RemovalExamples;
        if (examples.Count >= 4 || examples.ContainsKey(cause))
            return;
        examples.Add(
            cause,
            new
            {
                pawn.ThingID,
                position = pawn.Position.ToString(),
                hediffs = pawn.health.hediffSet.hediffs.Select(h => h.def.defName).ToArray(),
                caller = new StackTrace(2, false).ToString(),
            }
        );
    }

    public static void TickBegin(out long __state) => __state = Stopwatch.GetTimestamp();

    public static void MethodBegin(out long __state) => __state = Stopwatch.GetTimestamp();

    internal static async Task<object> PausedRenderProfile(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int frames,
        MethodBase[] methods,
        Action start = null
    )
    {
        await drawGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.paused-render-cost");
        RuntimeFrameProbe probe = null;
        int tick = -1;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (active != null || Find.CurrentMap == null || !Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Use a paused map without another profile."
                        );
                    tick = Find.TickManager.TicksGame;
                    probe = new GameObject(
                        "TotalFogPausedRenderProbe"
                    ).AddComponent<RuntimeFrameProbe>();
                    probe.IncludePausedFrames = true;
                    active = probe;
                    foreach (var method in methods)
                    {
                        if (method == null)
                            throw new InvalidOperationException(
                                "A native render method is missing."
                            );
                        probe.Counters.Add(method, new MethodCounter());
                        harmony.Patch(
                            method,
                            prefix: new HarmonyMethod(
                                typeof(PerformanceScenarios),
                                nameof(MethodBegin)
                            ),
                            postfix: new HarmonyMethod(
                                typeof(PerformanceScenarios),
                                nameof(MethodEnd)
                            )
                        );
                    }
                    start?.Invoke();
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(frames, cancellationToken);
            return await ctx.MainThread.InvokeAsync<object>(
                () =>
                    new
                    {
                        success = Find.TickManager.Paused
                            && tick == Find.TickManager.TicksGame
                            && probe.FrameCount > 0
                            && !probe.Overflow,
                        startTick = tick,
                        endTick = Find.TickManager.TicksGame,
                        frames = probe.FrameCount,
                        probe.FocusLost,
                        frameIntervalMs = Summarize(probe.Frames, probe.FrameCount),
                        inclusiveMethods = probe
                            .Counters.Select(pair => new
                            {
                                method = pair.Key.DeclaringType.FullName + "." + pair.Key.Name,
                                pair.Value.Calls,
                                milliseconds = pair.Value.Elapsed * 1000d / Stopwatch.Frequency,
                                millisecondsPerFrame = pair.Value.Elapsed
                                    * 1000d
                                    / Stopwatch.Frequency
                                    / probe.FrameCount,
                            })
                            .ToArray(),
                    },
                cancellationToken
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
                        if (active == probe)
                            active = null;
                        if (probe != null)
                            UnityEngine.Object.Destroy(probe.gameObject);
                    },
                    CancellationToken.None
                );
            }
            finally
            {
                drawGate.Release();
            }
        }
    }

    public static void MethodEnd(MethodBase __originalMethod, long __state)
    {
        if (active == null || !active.Counters.TryGetValue(__originalMethod, out var counter))
            return;
        counter.Calls++;
        counter.Elapsed += Stopwatch.GetTimestamp() - __state;
    }

    private static void SlowdownRequested(MethodBase __originalMethod)
    {
        var probe = active;
        if (probe == null || probe.Slowdowns.Count >= 8)
            return;
        probe.Slowdowns.Add(
            new
            {
                tick = Find.TickManager.TicksGame,
                request = __originalMethod.Name,
                callers = new StackTrace(1, false)
                    .GetFrames()
                    .Take(10)
                    .Select(frame => frame.GetMethod())
                    .Select(method => method.DeclaringType?.FullName + "." + method.Name)
                    .ToArray(),
            }
        );
    }

    public static void TickEnd(long __state)
    {
        var probe = active;
        if (probe == null)
            return;
        if (probe.TickCount == probe.Ticks.Length)
        {
            probe.Overflow = true;
            return;
        }
        probe.Ticks[probe.TickCount++] =
            (Stopwatch.GetTimestamp() - __state) * 1000d / Stopwatch.Frequency;
        probe.TimeSpeedTicks[(int)Find.TickManager.CurTimeSpeed]++;
        float rate = Find.TickManager.TickRateMultiplier;
        probe.TickRateTicks.TryGetValue(rate, out int ticksAtRate);
        probe.TickRateTicks[rate] = ticksAtRate + 1;
    }

    private static object Summarize(double[] values, int count)
    {
        var sorted = new double[count];
        Array.Copy(values, sorted, count);
        Array.Sort(sorted);
        if (count == 0)
            return new { count };
        double Percentile(double p) =>
            sorted[Math.Min(count - 1, (int)Math.Ceiling(count * p) - 1)];
        return new
        {
            count,
            mean = sorted.Average(),
            median = Percentile(.5),
            p95 = Percentile(.95),
            p99 = Percentile(.99),
            max = sorted[count - 1],
            total = sorted.Sum(),
        };
    }
}

public sealed class RuntimeFrameProbe : MonoBehaviour
{
    internal readonly double[] Ticks = new double[500000],
        Frames = new double[10000];
    internal readonly int[] TimeSpeedTicks = new int[5];
    internal readonly Dictionary<float, int> TickRateTicks = new();
    internal readonly List<object> Slowdowns = new();
    internal readonly Dictionary<MethodBase, MethodCounter> Counters = new();
    internal readonly Dictionary<string, int> Kills = new();
    internal readonly Dictionary<string, object> RemovalExamples = new();
    internal int LivingDespawns,
        DeadDespawns;
    internal int TickCount,
        FrameCount;
    internal bool Overflow,
        FocusLost,
        IncludePausedFrames;
    private long previous;

    public void Update()
    {
        if (!Application.isFocused)
            FocusLost = true;
        if (Find.TickManager == null || !IncludePausedFrames && Find.TickManager.Paused)
        {
            previous = 0;
            return;
        }
        long now = Stopwatch.GetTimestamp();
        if (previous != 0)
        {
            if (FrameCount < Frames.Length)
                Frames[FrameCount++] = (now - previous) * 1000d / Stopwatch.Frequency;
            else
                Overflow = true;
        }
        previous = now;
    }
}

internal sealed class MethodCounter
{
    internal long Calls,
        Elapsed;
}
