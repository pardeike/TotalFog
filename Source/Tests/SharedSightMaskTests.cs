using System;
using System.Collections.Generic;
using System.Linq;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class SharedSightMaskTests
{
    [Fact]
    public void ShorterSharingRetainsObstaclesAndNeverChangesTheOwnersFullSight()
    {
        var blockers = new bool[41 * 21];
        for (int z = 0; z < 21; z++)
            blockers[z * 41 + 24] = true;
        var own = Mask(0, 0, 41, 21, 41);
        FieldOfView.ComputeMask(41, 21, 20, 10, 18, blockers, own);
        var before = Cells(own);
        var shared = Mask(12, 2, 17, 17, 41);
        shared.AddFromWithinRadius(own, 20, 10, 8);
        Assert.True(own.Contains(10 * 41 + 8));
        Assert.False(shared.Contains(10 * 41 + 8));
        Assert.True(shared.Contains(10 * 41 + 23));
        Assert.True(shared.Contains(10 * 41 + 24));
        Assert.False(shared.Contains(10 * 41 + 25));
        Assert.Equal(before, Cells(own));
    }

    [Fact]
    public void SharingMatchesIndependentShortCastsAcrossBlockersAndMapEdges()
    {
        var random = new Random(7349);
        const int width = 149,
            height = 73;
        for (int sample = 0; sample < 80; sample++)
        {
            var blockers = Enumerable
                .Range(0, width * height)
                .Select(_ => random.Next(7) == 0)
                .ToArray();
            int x = random.Next(width),
                z = random.Next(height);
            var own = Mask(0, 0, width, height, width);
            FieldOfView.ComputeMask(width, height, x, z, 70, blockers, own);
            foreach (int radius in new[] { 0, 1, 2, 7, 16, 31, 32, 33, 35 })
            {
                var expected = Mask(0, 0, width, height, width);
                FieldOfView.ComputeMask(width, height, x, z, radius, blockers, expected);
                int left = Math.Max(0, x - radius),
                    bottom = Math.Max(0, z - radius);
                var shared = Mask(
                    left,
                    bottom,
                    Math.Min(width - 1, x + radius) - left + 1,
                    Math.Min(height - 1, z + radius) - bottom + 1,
                    width
                );
                shared.AddFromWithinRadius(own, x, z, radius);
                Assert.Equal(Cells(expected), Cells(shared));
            }
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(32)]
    [InlineData(63)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(91)]
    public void ShiftedBoundsClipCircleWithoutPaddingAndPreserveExistingCells(int radius)
    {
        const int width = 300;
        var source = Mask(13, 7, 139, 15, width);
        for (int z = 7; z < 22; z++)
        for (int x = 13; x < 152; x++)
            if ((x + z) % 3 != 0)
                source.AddCell(x, z);
        var shared = Mask(7, 3, 173, 25, width);
        shared.AddCell(8, 4);
        shared.AddFromWithinRadius(source, 78, 14, radius);
        var expected = Cells(source)
            .Where(i =>
                radius >= 0
                && (long)(i % width - 78) * (i % width - 78)
                    + (long)(i / width - 14) * (i / width - 14)
                    <= (long)radius * radius
            )
            .Append(4 * width + 8)
            .OrderBy(i => i);
        Assert.Equal(expected, Cells(shared));
        // Reverse alignment: source begins before the destination, across multiple words.
        var smaller = Mask(77, 9, 67, 11, width);
        smaller.AddFromWithinRadius(source, 78, 14, radius);
        Assert.Equal(
            expected.Where(i =>
                i % width >= 77 && i % width < 144 && i / width >= 9 && i / width < 20
            ),
            Cells(smaller)
        );
    }

    [Fact]
    public void SharingReusesBuffersWithoutAllocatingAfterWarmup()
    {
        var source = Mask(0, 0, 161, 161, 200);
        source.AddHorizontalRun(0, 160, 80);
        var shared = Mask(40, 40, 81, 81, 200);
        for (int i = 0; i < 20; i++)
            Copy();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++)
            Copy();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(shared.Contains(80 * 200 + 80));

        void Copy()
        {
            shared.Reset(40, 40, 81, 81, 200);
            shared.AddFromWithinRadius(source, 80, 80, 40);
        }
    }

    private static VisibilityMask Mask(int x, int z, int width, int height, int stride)
    {
        var mask = new VisibilityMask();
        mask.Reset(x, z, width, height, stride);
        return mask;
    }

    private static int[] Cells(VisibilityMask mask)
    {
        var cells = new List<int>();
        VisibilityMask.ApplyDifference(
            new VisibilityMask(),
            mask,
            (index, visible) => cells.Add(index)
        );
        return cells.ToArray();
    }
}
