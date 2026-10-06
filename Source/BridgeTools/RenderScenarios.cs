using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Observe native section regeneration without changing its lists or error handling.</summary>
public sealed class RenderScenarios
{
    private static readonly List<object> failures = new();
    private static readonly List<object> mutations = new();

    [ThreadStatic]
    private static SectionLayer activeLayer;

    [ThreadStatic]
    private static IntVec3 lastCell;

    [ThreadStatic]
    private static List<Thing> lastList;

    [ThreadStatic]
    private static int lastCount;
    private static int regenerations;

    [Tool(
        "totalfog/render_reload",
        Description = "Reload a paused fixture twice while tracing native section errors and thing-grid mutations during regeneration. Diagnostic patches are removed afterward."
    )]
    public static async Task<object> RenderReload(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int repetitions = 2
    )
    {
        if (repetitions < 1 || repetitions > 4)
            throw new ArgumentOutOfRangeException(nameof(repetitions));
        var harmony = new Harmony("brrainz.totalfog.render-probe");
        object[] patches = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    failures.Clear();
                    mutations.Clear();
                    regenerations = 0;
                    patches = Harmony
                        .GetAllPatchedMethods()
                        .Where(m =>
                            m.DeclaringType == typeof(ThingGrid)
                            || m.DeclaringType == typeof(SectionLayer_Things)
                            || m.Name == "TakePrintFrom"
                        )
                        .Select(m =>
                            (object)
                                new
                                {
                                    method = m.DeclaringType.FullName + "." + m.Name,
                                    owners = Harmony.GetPatchInfo(m).Owners.ToArray(),
                                }
                        )
                        .ToArray();
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(SectionLayer_Things),
                            nameof(SectionLayer_Things.Regenerate)
                        ),
                        prefix: new HarmonyMethod(typeof(RenderScenarios), nameof(Begin)),
                        finalizer: new HarmonyMethod(typeof(RenderScenarios), nameof(End))
                    );
                    harmony.Patch(
                        AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAt)),
                        postfix: new HarmonyMethod(typeof(RenderScenarios), nameof(ListRead))
                    );
                    foreach (var name in new[] { "RegisterInCell", "DeregisterInCell" })
                        harmony.Patch(
                            AccessTools.Method(typeof(ThingGrid), name),
                            prefix: new HarmonyMethod(typeof(RenderScenarios), nameof(GridMutation))
                        );
                },
                cancellationToken
            );
            for (int i = 0; i < repetitions; i++)
            {
                var load = await ctx.Tools.CallAsync(
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
                if (!load.Succeeded())
                    return new
                    {
                        success = false,
                        stage = "load",
                        load.Error,
                    };
                await ctx.Game.FramesAsync(30, cancellationToken);
            }
            return await ctx.MainThread.InvokeAsync(
                () =>
                    new
                    {
                        success = failures.Count == 0,
                        regenerations,
                        failures = failures.ToArray(),
                        mutations = mutations.ToArray(),
                        patches,
                    },
                cancellationToken
            );
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    harmony.UnpatchAll(harmony.Id);
                    activeLayer = null;
                    lastList = null;
                },
                CancellationToken.None
            );
        }
    }

    public static void Begin(SectionLayer_Things __instance, out SectionLayer __state)
    {
        __state = activeLayer;
        activeLayer = __instance;
        lastCell = IntVec3.Invalid;
        lastList = null;
        lastCount = -1;
        Interlocked.Increment(ref regenerations);
    }

    public static void End(Exception __exception, SectionLayer __state)
    {
        if (__exception != null)
            lock (failures)
                if (failures.Count < 20)
                    failures.Add(
                        new
                        {
                            layer = activeLayer?.GetType().FullName,
                            cell = lastCell.ToString(),
                            originalCount = lastCount,
                            currentCount = lastList?.Count,
                            things = lastList
                                ?.Select(t => t.ThingID + ":" + t.def.defName)
                                .ToArray(),
                            error = __exception.ToString(),
                        }
                    );
        activeLayer = __state;
        lastList = null;
    }

    public static void ListRead(IntVec3 c, List<Thing> __result)
    {
        if (activeLayer == null)
            return;
        lastCell = c;
        lastList = __result;
        lastCount = __result.Count;
    }

    public static void GridMutation(MethodBase __originalMethod)
    {
        if (activeLayer == null)
            return;
        lock (mutations)
            if (mutations.Count < 20)
                mutations.Add(
                    new
                    {
                        layer = activeLayer.GetType().FullName,
                        operation = __originalMethod.Name,
                        cell = lastCell.ToString(),
                        stack = Environment.StackTrace,
                    }
                );
    }
}
