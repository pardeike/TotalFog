using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public sealed class DebugVisibilityTests
{
    [Fact]
    public void RepeatedRevealAndFogAreIdempotentAndPreserveRealObservers()
    {
        var grid = new VisibilityGrid(2, 1) { Initialized = true };
        grid.Add(0, 0);
        grid.Add(0, 0);
        grid.SetDebugVisible(0, 0, true);
        grid.SetDebugVisible(0, 0, true);
        Assert.Equal(3, grid.Counts(0)[0]);
        grid.SetDebugVisible(0, 0, false);
        grid.SetDebugVisible(0, 0, false);
        Assert.Equal(2, grid.Counts(0)[0]);
        Assert.True(grid.IsVisible(0, 0, 0));
    }

    [Fact]
    public void ObserverMovementCannotRemoveTheDebugReveal()
    {
        var grid = new VisibilityGrid(1, 1) { Initialized = true };
        grid.SetDebugVisible(0, 0, true);
        grid.Add(0, 0);
        Assert.False(grid.Remove(0, 0));
        Assert.True(grid.IsVisible(0, 0, 0));
        grid.SetDebugVisible(0, 0, false);
        Assert.False(grid.IsVisible(0, 0, 0));
        Assert.True(grid.Known[0]);
    }

    [Fact]
    public void DebugRevealIsScopedToTheCellAndObserverFaction()
    {
        var grid = new VisibilityGrid(2, 1) { Initialized = true };
        grid.SetDebugVisible(0, 0, true);
        Assert.False(grid.IsVisible(0, 1, 0));
        Assert.False(grid.IsVisible(8, 0, 0));
        grid.SetDebugVisible(8, 1, true);
        grid.SetDebugVisible(0, 0, false);
        Assert.False(grid.IsVisible(0, 0, 0));
        Assert.True(grid.IsVisible(8, 1, 0));
        Assert.False(grid.Known[1]);
    }

    [Fact]
    public void RefoggingWithoutDebugRevealDoesNotAllocateCoverageOrRemoveObservers()
    {
        var grid = new VisibilityGrid(1, 1);
        grid.SetDebugVisible(0, 0, false);
        Assert.Equal(0, grid.FactionCount);
        grid.Add(0, 0);
        grid.SetDebugVisible(0, 0, false);
        Assert.Equal(1, grid.Counts(0)[0]);
    }
}
