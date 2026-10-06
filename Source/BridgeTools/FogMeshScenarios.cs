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
        Description = "Regenerate every fog section and compare all target vertex alphas against independent native cell queries, including map borders. Requires a paused map."
    )]
    public static async Task<object> FogMeshEquivalence(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
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
                    mismatches = 0;
                object firstMismatch = null;
                byte Cell(int x, int z)
                {
                    x = Math.Max(0, Math.Min(map.Size.x - 1, x));
                    z = Math.Max(0, Math.Min(map.Size.z - 1, z));
                    int index = map.cellIndices.CellToIndex(new IntVec3(x, 0, z));
                    if (map.fogGrid.IsFogged(new IntVec3(x, 0, z)) || !fog.knownCells[index])
                        return 255;
                    return counts[index] > 0 ? (byte)0 : SectionLayerFog.PrefFogAlpha;
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
                            if (actual[offset] == expected)
                                continue;
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
                    success = mismatches == 0 && vertices == map.Size.x * map.Size.z * 9,
                    sections,
                    vertices,
                    mismatches,
                    firstMismatch,
                };
            },
            cancellationToken
        );
    }
}
