using System;
using System.Linq;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class CoreContractsTests
{
    [Theory]
    [InlineData(3, 7)]
    [InlineData(7, 3)]
    [InlineData(1, 1)]
    public void RectangularMapsRoundTripEveryCell(int width, int height)
    {
        var grid = new VisibilityGrid(width, height);
        for (int i = 0; i < width * height; i++)
            Assert.Equal(i, grid.Index(grid.X(i), grid.Z(i)));
        Assert.False(grid.InBounds(width, 0));
        Assert.False(grid.InBounds(0, height));
        Assert.False(grid.InBounds(-1, 0));
    }

    [Fact]
    public void ObserverOverlapDoesNotFlickerAndDiscoverySurvivesRemoval()
    {
        var grid = new VisibilityGrid(5, 5);
        Assert.True(grid.Add(0, 12));
        Assert.False(grid.Add(0, 12));
        Assert.False(grid.Remove(0, 12));
        Assert.True(grid.IsVisible(0, 2, 2));
        Assert.True(grid.Remove(0, 12));
        Assert.False(grid.IsVisible(0, 2, 2));
        Assert.True(grid.Known[12]);
        Assert.False(grid.Remove(0, 12));
        Assert.Equal(0, grid.Counts(0)[12]);
    }

    [Fact]
    public void FactionsAreSparseAndCountsDoNotOverflowAtShortBoundary()
    {
        var grid = new VisibilityGrid(1, 1);
        for (int i = 0; i < 40000; i++) grid.Add(1000000, 0);
        Assert.Equal(40000, grid.Counts(1000000)[0]);
        Assert.False(grid.Known[0]);
        Assert.False(grid.IsVisible(0, 0, 0));
        Assert.Equal(1, grid.FactionCount);
        Assert.False(grid.Remove(0, 0));
        Assert.Equal(1, grid.FactionCount);
    }

    [Fact]
    public void PlayerAndNpcCoverageStaySeparateAcrossInterleavedUpdates()
    {
        var grid = new VisibilityGrid(2, 3);
        Assert.False(grid.Remove(0, 4));
        Assert.False(grid.IsVisible(0, 0, 2));
        Assert.Equal(0, grid.FactionCount);
        var player = grid.Counts(0);
        var enemy = grid.Counts(1000000);
        Assert.NotSame(player, enemy);
        grid.Add(1000000, 4);
        Assert.False(grid.Known[4]);
        Assert.False(grid.IsVisible(0, 0, 2));
        grid.Add(0, 4); grid.Add(0, 4);
        Assert.True(grid.Remove(1000000, 4));
        Assert.False(grid.Remove(0, 4));
        Assert.True(grid.IsVisible(0, 0, 2));
        Assert.Same(player, grid.Counts(0));
        Assert.Equal(2, grid.FactionCount);
        Assert.True(grid.Remove(0, 4));
        Assert.True(grid.Known[4]);
        Assert.False(grid.IsVisible(1000000, 0, 2));
    }

    [Fact]
    public void RepeatedCoverageUpdatesAndQueriesAllocateNothingAfterSetup()
    {
        var grid = new VisibilityGrid(2, 3);
        grid.Counts(0); grid.Counts(12);
        void Exercise()
        {
            for (int i = 0; i < 1000; i++)
            {
                grid.Add(0, 4); grid.Add(12, 4);
                grid.IsVisible(0, 0, 2); grid.IsVisible(12, 0, 2);
                grid.Remove(0, 4); grid.Remove(12, 4);
            }
        }
        Exercise();
        long before = GC.GetAllocatedBytesForCurrentThread();
        Exercise();
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }

    [Fact]
    public void DiscoveryLoadChecksDimensionsAndCopiesState()
    {
        var grid = new VisibilityGrid(2, 3);
        var saved = new bool[6]; saved[4] = true;
        grid.LoadKnown(saved); saved[4] = false;
        Assert.True(grid.Known[4]);
        Assert.Throws<ArgumentException>(() => grid.LoadKnown(new bool[5]));
    }

    [Fact]
    public void InvalidCellsAreInvisibleAndDoNotAliasValidCells()
    {
        var grid = new VisibilityGrid(3, 7);
        grid.Add(0, 3);
        Assert.False(grid.IsVisible(0, 3, 0));
        Assert.False(grid.IsVisible(0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Add(0, 21));
    }

    [Theory]
    [InlineData(16, 16, 4)]
    [InlineData(17, 17, 4)]
    [InlineData(0, 0, 1)]
    [InlineData(33, 33, 1)]
    public void MeshChangesReachAllAdjacentSections(int x, int z, int expected)
    {
        var sections = GridGeometry.AffectedSections(x, z, 34, 34, 17).ToArray();
        Assert.Equal(expected, sections.Length);
        Assert.Equal(sections.Length, sections.Distinct().Count());
    }
}
