using System;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class FieldOfViewCornerTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(7)]
    public void BoundaryCandidatesDoNotRevealAWallBehindAnotherWall(int distance)
    {
        const int width = 19,
            center = 9;
        foreach (var direction in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var blockers = new bool[width * width];
            blockers[(center + direction.Item2 * 2) * width + center + direction.Item1 * 2] = true;
            int x = center + direction.Item1 * distance,
                z = center + direction.Item2 * distance;
            blockers[z * width + x] = true;
            Check(blockers, width, center, center, x, z, false);
        }
    }

    [Fact]
    public void EverySmallWallLayoutKeepsFloorSightReciprocalAndPointQueriesEqual()
    {
        const int width = 3;
        for (int layout = 0; layout < 512; layout++)
        {
            var blockers = new bool[width * width];
            for (int i = 0; i < blockers.Length; i++)
                blockers[i] = (layout & (1 << i)) != 0;
            for (int from = 0; from < blockers.Length; from++)
            {
                if (blockers[from])
                    continue;
                var seen = Cast(blockers, width, from % width, from / width);
                for (int to = 0; to < blockers.Length; to++)
                {
                    Assert.Equal(
                        seen[to],
                        FieldOfView.CanSee(
                            width,
                            width,
                            from % width,
                            from / width,
                            to % width,
                            to / width,
                            40,
                            blockers
                        )
                    );
                    if (blockers[to])
                        continue;
                    Assert.Equal(
                        seen[to],
                        FieldOfView.CanSee(
                            width,
                            width,
                            to % width,
                            to / width,
                            from % width,
                            from / width,
                            40,
                            blockers
                        )
                    );
                }
            }
        }
    }

    [Fact]
    public void TouchingWallCornersBlockSightInEveryDirection()
    {
        const int width = 17;
        for (int dx = -1; dx <= 1; dx += 2)
        for (int dz = -1; dz <= 1; dz += 2)
        {
            var blockers = new bool[width * width];
            blockers[8 * width + 8 + dx] = true;
            blockers[(8 + dz) * width + 8] = true;
            Check(blockers, width, 8, 8, 8 + dx, 8 + dz, false);
            Check(blockers, width, 8 + dx, 8 + dz, 8, 8, false);

            // A single corner remains open. Neither touching blocker may
            // prevent a genuine view around the other wall when removed.
            blockers[8 * width + 8 + dx] = false;
            Check(blockers, width, 8, 8, 8 + dx, 8 + dz, true);
            blockers[8 * width + 8 + dx] = true;
            blockers[(8 + dz) * width + 8] = false;
            Check(blockers, width, 8, 8, 8 + dx, 8 + dz, true);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    public void DiagonalWallsEncloseARoomFromInsideAndOutside(int radius)
    {
        const int width = 19,
            center = 9;
        var blockers = new bool[width * width];
        for (int z = 0; z < width; z++)
        for (int x = 0; x < width; x++)
            blockers[z * width + x] = Math.Abs(x - center) + Math.Abs(z - center) == radius;
        foreach (
            var observer in new[] { (center, center), (center - 1, center), (center, center + 1) }
        )
        {
            var seen = Cast(blockers, width, observer.Item1, observer.Item2);
            for (int z = 0; z < width; z++)
            for (int x = 0; x < width; x++)
            {
                int distance = Math.Abs(x - center) + Math.Abs(z - center);
                if (distance > radius)
                {
                    Assert.False(
                        seen[z * width + x],
                        $"Outside cell {x},{z} leaked from {observer}"
                    );
                    Assert.False(
                        FieldOfView.CanSee(
                            width,
                            width,
                            x,
                            z,
                            observer.Item1,
                            observer.Item2,
                            40,
                            blockers
                        )
                    );
                }
                else if (distance == radius)
                    Assert.True(seen[z * width + x], "The room's enclosing walls stay visible");
            }
        }
        blockers[center * width + center + radius] = false;
        Check(blockers, width, center, center, center + radius + 1, center, true);
    }

    private static void Check(
        bool[] blockers,
        int width,
        int x,
        int z,
        int tx,
        int tz,
        bool expected
    )
    {
        Assert.Equal(expected, Cast(blockers, width, x, z)[tz * width + tx]);
        Assert.Equal(expected, FieldOfView.CanSee(width, width, x, z, tx, tz, 40, blockers));
    }

    private static bool[] Cast(bool[] blockers, int width, int x, int z)
    {
        var cells = new bool[blockers.Length];
        FieldOfView.Compute(width, width, x, z, 40, blockers, i => cells[i] = true);
        var mask = new VisibilityMask();
        mask.Reset(0, 0, width, width, width);
        FieldOfView.ComputeMask(width, width, x, z, 40, blockers, mask);
        for (int i = 0; i < cells.Length; i++)
            Assert.Equal(cells[i], mask.Contains(i));
        return cells;
    }
}
