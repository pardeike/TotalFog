using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class FogMeshScenarios
{
    [Tool(
        "totalfog/fog_mesh_equivalence",
        Description = "Regenerate every fog section and compare all target vertex alphas against independent native cell queries, including map borders. Optionally draw each layer and verify its uploaded colors, including paused fade completion. Requires a paused map."
    )]
    public static async Task<object> FogMeshEquivalence(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        [ToolParameter(
            Description = "Draw each fog layer and verify uploaded alphas against targets",
            DefaultValue = false
        )]
            bool verifyDrawn = false
    )
    {
        return await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map =
                    Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
                if (!Find.TickManager.Paused)
                    throw new InvalidOperationException("Pause the map first.");
                var fog = map.GetVisibility();
                var counts = fog.GetFactionShownCells(Faction.OfPlayer);
                var layersField = AccessTools.Field(typeof(Section), "layers");
                var targetsField = AccessTools.Field(typeof(SectionLayerFog), "targetAlphas");
                int[][] neighbors =
                [
                    [0, 1, 3, 4],
                    [3, 4],
                    [3, 4, 6, 7],
                    [4, 7],
                    [4, 5, 7, 8],
                    [4, 5],
                    [1, 2, 4, 5],
                    [1, 4],
                    [4],
                ];
                int sections = 0,
                    vertices = 0,
                    mismatches = 0,
                    drawnMismatches = 0,
                    heightMismatches = 0;
                object firstMismatch = null;
                object firstDrawnMismatch = null;
                byte Cell(int x, int z)
                {
                    x = Math.Max(0, Math.Min(map.Size.x - 1, x));
                    z = Math.Max(0, Math.Min(map.Size.z - 1, z));
                    byte Raw(IntVec3 cell)
                    {
                        int cellIndex = map.cellIndices.CellToIndex(cell);
                        if (map.fogGrid.IsFogged(cell) || !fog.IsKnown(Faction.OfPlayer, cellIndex))
                            return 255;
                        return counts[cellIndex] > 0 ? (byte)0 : SectionLayerFog.PrefFogAlpha;
                    }
                    var position = new IntVec3(x, 0, z);
                    byte alpha = Raw(position);
                    if (position.GetEdifice(map) is Watchtowers.Building_Watchtower tower)
                        foreach (var cell in tower.OccupiedRect().ClipInsideMap(map))
                            alpha = Math.Min(alpha, Raw(cell));
                    return alpha;
                }
                for (int sx = 0; sx < map.Size.x; sx += 17)
                for (int sz = 0; sz < map.Size.z; sz += 17)
                {
                    var section = map.mapDrawer.SectionAt(new IntVec3(sx, 0, sz));
                    bool found = false;
                    foreach (var layer in (IEnumerable<SectionLayer>)layersField.GetValue(section))
                    {
                        if (layer is not SectionLayerFog fogLayer)
                            continue;
                        found = true;
                        sections++;
                        fogLayer.Regenerate();
                        var actual = (byte[])targetsField.GetValue(fogLayer);
                        var geometry = fogLayer.GetSubMesh(MatBases.FogOfWar).mesh.vertices;
                        foreach (var vertex in geometry)
                        {
                            bool atTower = false;
                            foreach (var tower in fog.TowerComponent.Towers)
                                if (
                                    vertex.x >= tower.Position.x - 1.5f
                                    && vertex.x <= tower.Position.x + 2.5f
                                    && vertex.z >= tower.Position.z - 1.5f
                                    && vertex.z <= tower.Position.z + 3.5f
                                )
                                {
                                    atTower = true;
                                    break;
                                }
                            float expectedHeight = atTower
                                ? AltitudeLayer.BuildingOnTop.AltitudeFor() - Altitudes.AltInc
                                : AltitudeLayer.FogOfWar.AltitudeFor();
                            if (Math.Abs(vertex.y - expectedHeight) > .0001f)
                                heightMismatches++;
                        }
                        UnityEngine.Color32[] drawn = null;
                        if (verifyDrawn)
                        {
                            if (!fogLayer.Visible)
                                throw new InvalidOperationException(
                                    "Enable normal fog drawing first."
                                );
                            fogLayer.DrawLayer();
                            var mesh = fogLayer.GetSubMesh(MatBases.FogOfWar);
                            if (!mesh.disabled)
                            {
                                drawn = mesh.mesh.colors32;
                                if (drawn.Length != actual.Length)
                                    throw new InvalidOperationException(
                                        "Uploaded fog colors differ from section geometry."
                                    );
                            }
                        }
                        int offset = 0;
                        var rect = section.CellRect;
                        for (int x = rect.minX; x <= rect.maxX; x++)
                        for (int z = rect.minZ; z <= rect.maxZ; z++)
                        for (int vertex = 0; vertex < 9; vertex++, offset++, vertices++)
                        {
                            byte expected = 0;
                            foreach (int neighbor in neighbors[vertex])
                                expected = Math.Max(
                                    expected,
                                    Cell(x + neighbor % 3 - 1, z + neighbor / 3 - 1)
                                );
                            if (actual[offset] != expected)
                            {
                                mismatches++;
                                firstMismatch ??= new
                                {
                                    x,
                                    z,
                                    vertex,
                                    actual = actual[offset],
                                    expected,
                                };
                            }
                            byte drawnAlpha = drawn == null ? (byte)0 : drawn[offset].a;
                            if (verifyDrawn && drawnAlpha != expected)
                            {
                                drawnMismatches++;
                                firstDrawnMismatch ??= new
                                {
                                    x,
                                    z,
                                    vertex,
                                    actual = drawnAlpha,
                                    expected,
                                };
                            }
                        }
                        if (offset != actual.Length)
                            throw new InvalidOperationException(
                                "Fog vertex count differs from section geometry."
                            );
                    }
                    if (!found)
                        throw new InvalidOperationException(
                            "A native section has no Total Fog layer."
                        );
                }
                return new
                {
                    success = mismatches == 0
                        && drawnMismatches == 0
                        && heightMismatches == 0
                        && vertices == map.Size.x * map.Size.z * 9,
                    sections,
                    vertices,
                    mismatches,
                    firstMismatch,
                    verifyDrawn,
                    drawnMismatches,
                    firstDrawnMismatch,
                    heightMismatches,
                };
            },
            cancellationToken
        );
    }
}
