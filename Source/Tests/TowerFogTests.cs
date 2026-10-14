using System;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public sealed class TowerFogTests
{
    [Theory]
    [InlineData(-1.15f, .5f)]
    [InlineData(2.15f, .5f)]
    [InlineData(.5f, -1.15f)]
    [InlineData(.5f, 2.15f)]
    [InlineData(.5f, 3.25f)]
    public void DrawingBoundsIncludeEveryLadderAndRaisedLookout(float x, float z) =>
        Assert.True(TowerGeometry.FogDrawingContains(100 + x, 100 + z, 100, 100));

    [Theory]
    [InlineData(-1.51f, 0)]
    [InlineData(2.51f, 0)]
    [InlineData(0, -1.51f)]
    [InlineData(0, 3.51f)]
    public void UnrelatedGroundKeepsOrdinaryFogDepth(float x, float z) =>
        Assert.False(TowerGeometry.FogDrawingContains(100 + x, 100 + z, 100, 100));

    [Theory]
    [InlineData(0, 1f)]
    [InlineData(255, 0f)]
    public void RememberedShadeDoesNotChangeOpaqueDepth(byte alpha, float expected) =>
        Assert.Equal(expected, TowerGeometry.RememberedBrightness(alpha));

    [Theory]
    [InlineData(0)]
    [InlineData(86)]
    [InlineData(255)]
    public void SolidBaseUsesOneShadeWithoutExposingAdjacentGround(byte alpha)
    {
        var samples = new byte[49];
        Array.Fill(samples, (byte)255);
        TowerGeometry.FillFogFootprint(samples, 7, 7, 0, 0, 3, 3, 20, 20, alpha);
        for (int z = 0; z < 7; z++)
        for (int x = 0; x < 7; x++)
            Assert.Equal(
                x is >= 2 and <= 4 && z is >= 2 and <= 4 ? alpha : (byte)255,
                samples[z * 7 + x]
            );
    }

    [Fact]
    public void SectionClippingUsesTheWholeTowersShadeOnBothSides()
    {
        var left = new byte[19 * 19];
        var right = new byte[19 * 19];
        Array.Fill(left, (byte)255);
        Array.Fill(right, (byte)255);
        // A tower centered at x=17 straddles native sections 0 and 1.
        TowerGeometry.FillFogFootprint(left, 19, 19, -1, -1, 17, 8, 40, 40, 0);
        TowerGeometry.FillFogFootprint(right, 19, 19, 16, -1, 17, 8, 40, 40, 0);
        for (int z = 0; z < 19; z++)
        {
            Assert.Equal(left[z * 19 + 17], right[z * 19]);
            Assert.Equal(left[z * 19 + 18], right[z * 19 + 1]);
        }
        Assert.Equal(255, left[9 * 19 + 16]);
        Assert.Equal(0, right[9 * 19 + 2]);
        Assert.Equal(255, right[9 * 19 + 3]);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 7)]
    [InlineData(7, 1)]
    [InlineData(7, 7)]
    public void MapEdgesRepeatTheNearestPhysicalCell(int towerX, int towerZ)
    {
        var samples = new byte[11 * 11];
        Array.Fill(samples, (byte)255);
        TowerGeometry.FillFogFootprint(samples, 11, 11, -1, -1, towerX, towerZ, 9, 9, 86);
        for (int z = -1; z <= 9; z++)
        for (int x = -1; x <= 9; x++)
        {
            bool inBase =
                Math.Abs(Math.Clamp(x, 0, 8) - towerX) <= 1
                && Math.Abs(Math.Clamp(z, 0, 8) - towerZ) <= 1;
            Assert.Equal(inBase ? (byte)86 : (byte)255, samples[(z + 1) * 11 + x + 1]);
        }
    }

    [Fact]
    public void ShortEdgeSectionsDoNotWriteIntoUnusedBufferRows()
    {
        var samples = new byte[19 * 19];
        Array.Fill(samples, (byte)255);
        TowerGeometry.FillFogFootprint(samples, 5, 4, 15, 15, 17, 17, 19, 18, 0);
        for (int index = 20; index < samples.Length; index++)
            Assert.Equal(255, samples[index]);
    }
}
