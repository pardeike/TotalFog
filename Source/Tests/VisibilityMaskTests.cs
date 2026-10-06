using System;
using System.Collections.Generic;
using System.Linq;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class VisibilityMaskTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(257)]
    public void HorizontalRunsClipPreserveExistingCellsAndNeverSetPadding(int width)
    {
        var mask = new VisibilityMask();
        mask.Reset(13, 7, width, 3, 500);
        var expected = new HashSet<int>();
        mask.AddCell(13 + width - 1, 9);
        if (width > 0)
            expected.Add(9 * 500 + 13 + width - 1);
        foreach (
            var run in new[]
            {
                (-20, -1, 7),
                (12, 13, 7),
                (15, 14, 7),
                (16, 90, 8),
                (60, 140, 8),
                (0, 500, 6),
                (0, 500, 10),
                (13 + width - 1, 500, 9),
            }
        )
        {
            mask.AddHorizontalRun(run.Item1, run.Item2, run.Item3);
            for (int x = run.Item1; x <= run.Item2; x++)
                if (x >= 13 && x < 13 + width && run.Item3 >= 7 && run.Item3 < 10)
                    expected.Add(run.Item3 * 500 + x);
        }
        for (int z = 6; z <= 10; z++)
        for (int x = 0; x < 500; x++)
            Assert.Equal(expected.Contains(z * 500 + x), mask.Contains(z * 500 + x));
        var changes = new List<int>();
        VisibilityMask.ApplyDifference(
            new VisibilityMask(),
            mask,
            (i, visible) =>
            {
                Assert.True(visible);
                changes.Add(i);
            }
        );
        Assert.Equal(expected.OrderBy(i => i), changes);
    }

    [Fact]
    public void ShiftedFootprintsOnlyReportChangedCells()
    {
        var a = new VisibilityMask();
        var b = new VisibilityMask();
        a.Reset(0, 0, 3, 3, 7);
        b.Reset(1, 1, 3, 3, 7);
        a.Add(8);
        a.Add(9);
        b.Add(9);
        b.Add(10);
        var changes = new List<(int, bool)>();
        VisibilityMask.ApplyDifference(a, b, (i, visible) => changes.Add((i, visible)));
        Assert.Equal(new[] { (10, true), (8, false) }, changes);
    }

    [Fact]
    public void ResetClearsReusedCellsAndDoesNotAcceptOutsideCoordinates()
    {
        var mask = new VisibilityMask();
        mask.Reset(0, 0, 2, 2, 5);
        mask.Add(0);
        mask.Add(2);
        Assert.True(mask.Contains(0));
        Assert.False(mask.Contains(2));
        mask.Reset(0, 0, 1, 1, 5);
        Assert.False(mask.Contains(0));
    }

    [Fact]
    public void EmptyFootprintRemovesAllSightExactlyOnce()
    {
        var a = new VisibilityMask();
        var b = new VisibilityMask();
        a.Reset(2, 1, 2, 2, 7);
        a.Add(9);
        a.Add(17);
        var changes = new List<(int, bool)>();
        VisibilityMask.ApplyDifference(a, b, (i, v) => changes.Add((i, v)));
        Assert.Equal(new[] { (9, false), (17, false) }, changes);
    }

    [Fact]
    public void RandomFootprintsMatchGlobalSetDifferencesWithoutDuplicateCallbacks()
    {
        var random = new Random(641);
        for (int sample = 0; sample < 1024; sample++)
        {
            int mapWidth = random.Next(2, 300),
                mapHeight = random.Next(2, 80);
            var previous = new VisibilityMask();
            var next = new VisibilityMask();
            var before = Fill(previous);
            var after = Fill(next);
            var changes = new List<(int index, bool visible)>();
            VisibilityMask.ApplyDifference(previous, next, (i, v) => changes.Add((i, v)));
            Assert.Equal(
                after.Except(before).OrderBy(i => i),
                changes.Where(c => c.visible).Select(c => c.index).OrderBy(i => i)
            );
            Assert.Equal(
                before.Except(after).OrderBy(i => i),
                changes.Where(c => !c.visible).Select(c => c.index).OrderBy(i => i)
            );
            Assert.Equal(changes.Count, changes.Distinct().Count());
            // Additions precede removals, preventing temporary loss of overlapping coverage.
            Assert.Equal(changes.OrderByDescending(c => c.visible), changes);

            HashSet<int> Fill(VisibilityMask mask)
            {
                int x = random.Next(mapWidth),
                    z = random.Next(mapHeight);
                int width = random.Next(mapWidth - x + 1),
                    height = random.Next(mapHeight - z + 1);
                mask.Reset(x, z, width, height, mapWidth);
                var set = new HashSet<int>();
                for (int row = z; row < z + height; row++)
                for (int column = x; column < x + width; column++)
                    if (random.Next(3) != 0)
                    {
                        int index = row * mapWidth + column;
                        mask.Add(index);
                        set.Add(index);
                    }
                return set;
            }
        }
    }

    [Fact]
    public void DifferentGridWidthsStillCompareGlobalIndices()
    {
        var a = new VisibilityMask();
        var b = new VisibilityMask();
        a.Reset(0, 0, 7, 3, 7);
        b.Reset(0, 0, 5, 4, 5);
        a.Add(9);
        a.Add(17);
        b.Add(9);
        b.Add(18);
        var changes = new List<(int, bool)>();
        VisibilityMask.ApplyDifference(a, b, (i, v) => changes.Add((i, v)));
        Assert.Equal(new[] { (18, true), (17, false) }, changes);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    public void WordEdgesAndShiftedRowsNeverReportPaddingOrLoseSight(int width)
    {
        var previous = new VisibilityMask();
        var next = new VisibilityMask();
        previous.Reset(65, 2, width, 3, 400);
        next.Reset(0, 1, width + 130, 5, 400);
        for (int z = 2; z < 5; z++)
        for (int x = 65; x < 65 + width; x++)
            previous.AddCell(x, z);
        for (int z = 1; z < 6; z++)
        for (int x = 0; x < width + 130; x++)
            next.AddCell(x, z);
        var changes = new List<(int index, bool visible)>();
        VisibilityMask.ApplyDifference(previous, next, (i, v) => changes.Add((i, v)));
        Assert.All(changes, c => Assert.True(c.visible));
        Assert.Equal((width + 130) * 5 - width * 3, changes.Count);
        Assert.Equal(changes.Select(c => c.index).OrderBy(i => i), changes.Select(c => c.index));
        var reversed = new List<(int, bool)>();
        VisibilityMask.ApplyDifference(next, previous, (i, v) => reversed.Add((i, v)));
        Assert.Equal(changes.Select(c => (c.index, false)), reversed);
        previous.Reset(0, 0, 1, 1, 400);
        Assert.False(previous.Contains(0));
        previous.Reset(0, 0, width + 130, 5, 400);
        Assert.All(Enumerable.Range(0, previous.Area), i => Assert.False(previous.At(i)));
    }

    [Fact]
    public void PeekingIntersectionKeepsExistingSightAndOnlyAddsCellsInBothMasks()
    {
        var random = new Random(426);
        for (int sample = 0; sample < 128; sample++)
        {
            int width = random.Next(1, 300);
            var result = new VisibilityMask();
            var a = new VisibilityMask();
            var b = new VisibilityMask();
            foreach (var mask in new[] { result, a, b })
                mask.Reset(13, 7, width, 4, 400);
            var expected = new HashSet<int>();
            for (int z = 7; z < 11; z++)
            for (int x = 13; x < 13 + width; x++)
            {
                int index = z * 400 + x;
                bool existing = random.Next(3) == 0,
                    inA = random.Next(2) == 0,
                    inB = random.Next(2) == 0;
                if (existing)
                    result.AddCell(x, z);
                if (inA)
                    a.AddCell(x, z);
                if (inB)
                    b.AddCell(x, z);
                if (existing || inA && inB)
                    expected.Add(index);
            }
            result.AddIntersection(a, b);
            Assert.Equal(
                expected.OrderBy(i => i),
                Enumerable.Range(0, result.Area).Where(result.At).Select(result.GlobalIndex)
            );
        }
    }

    [Fact]
    public void PeekingIntersectionRejectsDifferentBounds()
    {
        var result = new VisibilityMask();
        var a = new VisibilityMask();
        var b = new VisibilityMask();
        result.Reset(0, 0, 65, 2, 250);
        a.Reset(0, 0, 65, 2, 250);
        b.Reset(1, 0, 65, 2, 250);
        Assert.Throws<ArgumentException>(() => result.AddIntersection(a, b));
    }
}
