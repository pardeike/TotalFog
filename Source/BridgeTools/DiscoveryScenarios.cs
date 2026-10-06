using System;
using System.Collections.Generic;
using System.Reflection;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class DiscoveryScenarios
{
    [Tool(
        "totalfog/discovery_overlays",
        Description = "Exercise the installed roof, fertility and terrain overlay methods on actual qualifying cells, across known and unknown discovery state."
    )]
    public static object DiscoveryOverlays()
    {
        var map = Find.CurrentMap;
        var fog = map.GetComponent<MapVisibility>();
        var results = new List<object>();
        foreach (var grid in new object[] { map.roofGrid, map.fertilityGrid, map.terrainGrid })
        {
            var method = grid.GetType()
                .GetMethod(
                    grid is RoofGrid ? "GetCellBool" : "CellBoolDrawerGetBoolInt",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );
            bool found = false;
            foreach (var cell in map.AllCells)
            {
                if (map.fogGrid.IsFogged(cell))
                    continue;
                int index = map.cellIndices.CellToIndex(cell);
                bool original = fog.knownCells[index];
                try
                {
                    fog.knownCells[index] = true;
                    bool known = (bool)method.Invoke(grid, new object[] { index });
                    if (!known)
                        continue;
                    fog.knownCells[index] = false;
                    bool unknown = (bool)method.Invoke(grid, new object[] { index });
                    fog.knownCells[index] = true;
                    bool restored = (bool)method.Invoke(grid, new object[] { index });
                    results.Add(
                        new
                        {
                            grid = grid.GetType().Name,
                            cell = new { cell.x, cell.z },
                            known,
                            unknown,
                            restored,
                        }
                    );
                    found = true;
                    break;
                }
                finally
                {
                    fog.knownCells[index] = original;
                }
            }
            if (!found)
                throw new InvalidOperationException(
                    "No qualifying overlay cell for " + grid.GetType().Name
                );
        }
        return new { initialized = fog.Initialized, results };
    }
}
