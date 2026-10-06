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
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class ContaminationRenderCostScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/contamination_render_cost",
        Description = "Paused dense/sparse contaminated ground and steel-stack overlay diagnostic. Alternate native section and original CellBoolDrawer routes with identical colony-bypass visibility, measure warm, dirty and closed overlay frames, preserve native method timings, and reload the unchanged named fixture. Optional cell delegate is restored. Not simulation TPS or GPU completion acceptance."
    )]
    public static async Task<object> RenderCost(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int count = 400,
        bool sparse = false,
        int frames = 60,
        int pairs = 3
    )
    {
        if (count is not (400 or 4000) || frames < 30 || frames > 120 || pairs < 1 || pairs > 5)
            throw new ArgumentException("Use 400/4000 cells, 30..120 frames and 1..5 pairs.");
        await gate.WaitAsync(cancellationToken);
        Map map = null;
        object manager = null,
            ground = null,
            originalApi = null;
        FieldInfo cellApi = null,
            overlay = null;
        bool oldOverlay = false,
            oldBypass = FogSettings.OnlyOutsideColony;
        var rows = new List<object>();
        var items = new List<Thing>();
        int width = count == 400 ? 20 : 80,
            height = count / width,
            stride = sparse ? 2 : 1;
        int bodyWidth = (width - 1) * stride + 1,
            bodyHeight = (height - 1) * stride + 1;
        var cells = Enumerable
            .Range(0, count)
            .Select(i => new IntVec3(40 + i % width * stride, 0, 40 + i / width * stride))
            .ToArray();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    if (
                        !Find.TickManager.Paused
                        || !map.IsPlayerHome
                        || !map.GetVisibility().Initialized
                        || cells.Any(c => !c.InBounds(map))
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized home map containing the complete shape."
                        );
                    var type = AccessTools.TypeByName("ZombieLand.ContaminationManager");
                    manager = AccessTools.PropertyGetter(type, "Instance").Invoke(null, null);
                    overlay = AccessTools.Field(type, "showContaminationOverlay");
                    oldOverlay = (bool)overlay.GetValue(manager);
                    cellApi = AccessTools.Field(
                        AccessTools.TypeByName("ZombieLand.TotalFogSupport"),
                        "isCellVisible"
                    );
                    originalApi =
                        cellApi.GetValue(null)
                        ?? throw new InvalidOperationException(
                            "The optional cell filter is not bound."
                        );
                    FogSettings.OnlyOutsideColony = true;
                    var grounds = (IDictionary)AccessTools.Field(type, "grounds").GetValue(manager);
                    ground = grounds[map.Index];
                    Array.Clear(
                        (float[])AccessTools.Field(ground.GetType(), "cells").GetValue(ground),
                        0,
                        map.cellIndices.NumGridCells
                    );
                    var setGround = AccessTools.PropertySetter(ground.GetType(), "Item");
                    var setThing = AccessTools.Method(
                        type,
                        "Set",
                        new[] { typeof(Thing), typeof(float), typeof(Map) }
                    );
                    foreach (var cell in cells)
                    {
                        map.fogGrid.Unfog(cell);
                        var item = GenSpawn.Spawn(
                            ThingMaker.MakeThing(ThingDefOf.Steel),
                            cell,
                            map
                        );
                        items.Add(item);
                        setGround.Invoke(ground, new object[] { cell, .65f });
                        setThing.Invoke(manager, new object[] { item, .65f, map });
                    }
                    Find.Selector.ClearSelection();
                    overlay.SetValue(manager, false);
                },
                cancellationToken
            );
            var framed = await ctx.Tools.CallAsync(
                "rimworld/frame_cell_rect",
                new
                {
                    x = 40,
                    z = 40,
                    width = bodyWidth,
                    height = bodyHeight,
                    rootSize = 80f,
                },
                cancellationToken: cancellationToken
            );
            if (!framed.Succeeded())
                throw new InvalidOperationException("Overlay camera setup failed.");
            await ctx.Game.FramesAsync(65, cancellationToken);
            var layer = AccessTools.TypeByName("ZombieLand.SectionLayer_Contamination");
            var ui = AccessTools.TypeByName(
                "ZombieLand.Patches+MapInterface_MapInterfaceUpdate_Patch"
            );
            var methods = new MethodBase[]
            {
                AccessTools.Method(ui, "Postfix"),
                AccessTools.Method(layer, "DrawLayer"),
                AccessTools.Method(layer, "Rebuild"),
                AccessTools.Method(typeof(CellBoolDrawer), "RegenerateMesh"),
                AccessTools.Method(typeof(CellBoolDrawer), "ActuallyDraw"),
            };
            for (int pair = 0; pair < pairs; pair++)
                foreach (
                    bool sectionRoute in pair % 2 == 0
                        ? new[] { false, true }
                        : new[] { true, false }
                )
                {
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            cellApi.SetValue(null, sectionRoute ? originalApi : null);
                            overlay.SetValue(manager, true);
                            Dirty();
                            if (
                                Find.CameraDriver.CurrentViewRect.Area < 6400
                                || cells.Any(c =>
                                    !Find.CameraDriver.CurrentViewRect.Contains(c)
                                    || !Visibility.IsVisible(map, c)
                                )
                            )
                                throw new InvalidOperationException(
                                    "Both routes must see the complete shape at cached zoom."
                                );
                        },
                        cancellationToken
                    );
                    await ctx.Game.FramesAsync(65, cancellationToken);
                    foreach (var mode in new[] { "warm", "dirty", "closed" })
                    {
                        await ctx.MainThread.InvokeAsync(
                            () =>
                            {
                                overlay.SetValue(manager, mode != "closed");
                            },
                            cancellationToken
                        );
                        if (mode == "closed")
                            await ctx.Game.FramesAsync(6, cancellationToken);
                        rows.Add(
                            new
                            {
                                pair,
                                sectionRoute,
                                mode,
                                measurement = await PerformanceScenarios.PausedRenderProfile(
                                    ctx,
                                    cancellationToken,
                                    frames,
                                    methods,
                                    mode == "dirty" ? Dirty : null
                                ),
                            }
                        );
                    }
                }
            return new
            {
                count,
                sparse,
                bodyWidth,
                bodyHeight,
                frames,
                pairs,
                rows,
                diagnosticOnly = true,
                timing = "Inclusive native Stopwatch elapsed includes Harmony probe and scheduling overhead. No GPU completion measurement.",
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (originalApi != null)
                            cellApi.SetValue(null, originalApi);
                        if (manager != null)
                            overlay.SetValue(manager, oldOverlay);
                        FogSettings.OnlyOutsideColony = oldBypass;
                    },
                    CancellationToken.None
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
                    throw new InvalidOperationException("Cost fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }
        void Dirty()
        {
            (
                (CellBoolDrawer)AccessTools.Field(ground.GetType(), "drawer").GetValue(ground)
            ).SetDirty();
            AccessTools.Field(manager.GetType(), "currentMapDirty").SetValue(manager, true);
            map.mapDrawer.WholeMapChanged(
                DefDatabase<MapMeshFlagDef>.GetNamed("ZombieContaminationOverlay")
            );
        }
    }
}
