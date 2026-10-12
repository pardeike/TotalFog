using System;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class TowerGeometryTests
{
    [Theory]
    [InlineData(-.2f, 0f)]
    [InlineData(0f, 0f)]
    [InlineData(.25f, .5f)]
    [InlineData(.5f, 1f)]
    [InlineData(1f, 2f)]
    [InlineData(1.2f, 2f)]
    public void PhysicalPlatformLiftFollowsClimbingAndDescending(float elevation, float lift) =>
        Assert.Equal(lift, TowerGeometry.PlatformLift(elevation));

    [Theory]
    [InlineData(1f, 1f, .5f, false)]
    [InlineData(3f, 1f, .1f, true)]
    [InlineData(3f, 1f, .9f, true)]
    [InlineData(1f, 3f, .1f, true)]
    [InlineData(1f, 3f, .9f, true)]
    [InlineData(3f, 3f, .5f, true)]
    public void ElevatedIncomingAndOutgoingShotsClearLowWalls(
        float a,
        float b,
        float t,
        bool clears
    ) => Assert.Equal(clears, TowerGeometry.ClearsLowWall(a, b, t));

    [Fact]
    public void RangeIsFiniteAndScalesWithThePawn()
    {
        Assert.Equal(40, TowerGeometry.SightRadius(20, 2, 96));
        Assert.Equal(96, TowerGeometry.SightRadius(100, 2, 96));
        Assert.Equal(0, TowerGeometry.SightRadius(0, 2, 96));
    }

    [Fact]
    public void PrivateFootprintExemptionDoesNotOpenAnotherTowerOrTheSharedGrid()
    {
        const int width = 25;
        var shared = new bool[width * width];
        for (int z = 3; z <= 5; z++)
        for (int x = 3; x <= 5; x++)
            shared[z * width + x] = true;
        for (int z = 3; z <= 5; z++)
        for (int x = 10; x <= 12; x++)
            shared[z * width + x] = true;
        var before = (bool[])shared.Clone();
        var own = new bool[shared.Length];
        TowerGeometry.CopySightWindow(shared, own, width, 0, 0, 24, 24, 4, 4);
        Assert.Equal(before, shared);
        Assert.False(own[4 * width + 5]);
        Assert.True(own[4 * width + 10]);
        Assert.True(FieldOfView.CanSee(width, width, 4, 4, 9, 4, 20, own));
        Assert.False(FieldOfView.CanSee(width, width, 4, 4, 13, 4, 20, own));
    }

    [Fact]
    public void RefreshClosesAndOpensAnObstacleWithoutLeavingOldSnapshotBits()
    {
        var source = new bool[81];
        var destination = new bool[81];
        source[4 * 9 + 7] = true;
        TowerGeometry.CopySightWindow(source, destination, 9, 0, 0, 8, 8, 4, 4);
        Assert.True(destination[4 * 9 + 7]);
        source[4 * 9 + 7] = false;
        TowerGeometry.CopySightWindow(source, destination, 9, 0, 0, 8, 8, 4, 4);
        Assert.False(destination[4 * 9 + 7]);
    }

    [Fact]
    public void AscentDescentAndProjectileDrawingHaveExactEndpoints()
    {
        Assert.Equal(0f, TowerGeometry.Interpolate(0f, TowerGeometry.PawnProjection, 0f));
        Assert.Equal(
            TowerGeometry.PawnProjection,
            TowerGeometry.Interpolate(0f, TowerGeometry.PawnProjection, 1f)
        );
        Assert.Equal(0f, TowerGeometry.Interpolate(TowerGeometry.PawnProjection, 0f, 1f));
        Assert.Equal(.825f, TowerGeometry.Interpolate(TowerGeometry.PawnProjection, 0f, .5f));
    }
}
