using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class GasVisibilityTests
{
    [Fact]
    public void Gas_mesh_uses_current_sight_and_preserves_native_density()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.knownCells[1] = true;
        var grid = new GasGrid();
        var shown = GasVisibility.ShownCells(map);
        Assert.False(GasVisibility.AnyVisibleGasAt(grid, new IntVec3(1, 0), map, shown));
        Assert.Equal(0, grid.Reads);
        map.Fog.ShownCounts[1] = 1;
        Assert.True(GasVisibility.AnyVisibleGasAt(grid, new IntVec3(1, 0), map, shown));
        grid.Present = false;
        Assert.False(GasVisibility.AnyVisibleGasAt(grid, new IntVec3(1, 0), map, shown));
    }

    [Fact]
    public void Initialization_retains_native_gas_presentation()
    {
        var map = new Map();
        Assert.Null(GasVisibility.ShownCells(map));
        Assert.True(GasVisibility.AnyVisibleGasAt(new GasGrid(), new IntVec3(1, 0), map, null));
    }
}
