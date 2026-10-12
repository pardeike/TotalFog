using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Paused, native gas-grid rendering; no player payload changes.</summary>
public sealed class GasMeshScenarios
{
    [Tool(
        "totalfog/gas_mesh_visibility",
        Description = "On a paused fixture, stage nine native smoke cells in one empty section and check hidden/visible/hidden mesh regeneration, retained gas density, screenshots and regeneration CPU time. Reloads the unchanged save."
    )]
    public static async Task<object> GasMeshVisibility(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        var manifest = RimBridgeEvidence.CreateManifest("totalfog/gas_mesh_visibility");
        manifest.saveName = saveName;
        Map map = null;
        MapVisibility fog = null;
        CellRect area = default;
        SectionLayer_Gas layer = null;
        int tick = 0;
        bool addedSight = false;
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    if (map == null || !Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Load and pause the named fixture first."
                        );
                    fog = map.GetVisibility();
                    if (!fog.Initialized)
                        throw new InvalidOperationException("Sight must be initialized.");
                    var counts = fog.GetFactionShownCells(Faction.OfPlayer);
                    var origin = map.AllCells.First(c =>
                    {
                        var rect = new CellRect(c.x, c.z, 3, 3);
                        return rect.InBounds(map)
                            && c.x % 17 < 14
                            && c.z % 17 < 14
                            && rect.All(p =>
                                p.Standable(map) && counts[map.cellIndices.CellToIndex(p)] == 0
                            )
                            && map.mapDrawer.SectionAt(c)
                                .CellRect.All(p => !map.gasGrid.AnyGasAt(p));
                    });
                    area = new CellRect(origin.x, origin.z, 3, 3);
                    layer = (
                        (IEnumerable<SectionLayer>)
                            AccessTools
                                .Field(typeof(Section), "layers")
                                .GetValue(map.mapDrawer.SectionAt(origin))
                    )
                        .OfType<SectionLayer_Gas>()
                        .Single();
                    tick = Find.TickManager.TicksGame;
                    foreach (var cell in area)
                    {
                        map.fogGrid.Unfog(cell);
                        fog.knownCells[map.cellIndices.CellToIndex(cell)] = true;
                        map.gasGrid.AddGas(cell, GasType.BlindSmoke, 255, false);
                    }
                },
                cancellationToken
            );
            var frame = await ctx.Tools.CallAsync(
                "rimworld/frame_cell_rect",
                new
                {
                    x = area.minX,
                    z = area.minZ,
                    width = 3,
                    height = 3,
                    paddingCells = 3,
                    rootSize = 15,
                },
                cancellationToken: cancellationToken
            );
            if (!frame.Succeeded())
                throw new InvalidOperationException("Gas framing failed.");
            for (int phase = 0; phase < 3; phase++)
            {
                bool shown = phase == 1;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        foreach (var cell in area)
                        {
                            int index = map.cellIndices.CellToIndex(cell);
                            if (shown)
                                fog.IncrementSeen(Faction.OfPlayer, index);
                            else if (addedSight)
                                fog.DecrementSeen(Faction.OfPlayer, index);
                        }
                        addedSight = shown;
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(6, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        // Read the normally regenerated mesh BEFORE the timed probe.
                        int vertices = layer.GetSubMesh(layer.Mat).verts.Count;
                        bool densityRetained = area.All(c =>
                            map.gasGrid.DensityAt(c, GasType.BlindSmoke) == 255
                        );
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "native mesh phase " + phase,
                                vertices == (shown ? 36 : 0)
                                    && densityRetained
                                    && Find.TickManager.TicksGame == tick
                            )
                        );
                        var watch = Stopwatch.StartNew();
                        for (int repeat = 0; repeat < 100; repeat++)
                            layer.Regenerate();
                        rows.Add(
                            new
                            {
                                phase,
                                shown,
                                vertices,
                                densityRetained,
                                tick = Find.TickManager.TicksGame,
                                meanRegenerationMs = watch.Elapsed.TotalMilliseconds / 100,
                            }
                        );
                    },
                    cancellationToken
                );
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ,
                        width = 3,
                        height = 3,
                        paddingCells = 3,
                        rootSize = 15,
                        fileName = evidenceLabel + "-" + phase,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                manifest.assertions.Add(
                    RimBridgeEvidence.ToolSucceeded("capture phase " + phase, shot)
                );
                manifest.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = "phase " + phase,
                        kind = "cell_rect",
                        path = shot.TryReadResult<string>(out var path, "path")
                            ? path
                            : string.Empty,
                        details = shot.Result,
                    }
                );
            }
            manifest.environment.gameVersion = VersionControl.CurrentVersionString;
            manifest.environment.mapId = map.GetUniqueLoadID();
            manifest.environment.details = new
            {
                rows,
                x = area.minX,
                z = area.minZ,
                tick,
            };
        }
        finally
        {
            if (map != null)
            {
                var reload = await ctx.Tools.CallAsync(
                    "totalfog/load_comparison_save",
                    new { saveName },
                    cancellationToken: CancellationToken.None
                );
                manifest.assertions.Add(
                    RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
                );
            }
        }
        RimBridgeEvidence.Complete(manifest);
        return manifest;
    }
}
