using System;
using TotalFog.Core;
using Xunit;
namespace TotalFog.Tests;
public class FogAppearanceTests
{
    [Theory]
    [InlineData(true,true,1,255)] [InlineData(false,false,0,255)]
    [InlineData(false,true,0,86)] [InlineData(false,true,1,0)]
    public void SeparatesUnknownRememberedAndVisible(bool vanilla,bool known,int viewers,byte expected)
        => Assert.Equal(expected,FogAppearance.CellAlpha(vanilla,known,viewers,86));

    [Fact]
    public void UnknownDiagonalDarkensOnlyItsTouchingCorner()
    {
        var cells = new byte[9]; cells[8] = 255;
        var vertices = new byte[9]; FogAppearance.FillVertices(cells,vertices);
        Assert.Equal(255,vertices[4]);
        for(int i=0;i<9;i++) if(i!=4) Assert.Equal(0,vertices[i]);
    }
    [Fact]
    public void SharedVerticesAgreeAcrossCellAndSectionBoundaries()
    {
        var random = new Random(417);
        var samples = new byte[12]; random.NextBytes(samples);
        var left = new byte[9];var right = new byte[9];var a = new byte[9];var b = new byte[9];
        for(int z=0;z<3;z++) for(int x=0;x<3;x++) {left[z*3+x]=samples[z*4+x];right[z*3+x]=samples[z*4+x+1];}
        FogAppearance.FillVertices(left,a);FogAppearance.FillVertices(right,b);
        Assert.Equal(a[6],b[0]);Assert.Equal(a[5],b[1]);Assert.Equal(a[4],b[2]);
    }
    [Fact]
    public void BatchedSectionsMatchIndependentVertexMaximaIncludingClippedMapEdges()
    {
        int[][] neighbors = [[0,1,3,4], [3,4], [3,4,6,7], [4,7], [4,5,7,8], [4,5], [1,2,4,5], [1,4], [4]];
        var random = new Random(329);
        var samples = new byte[19 * 19];
        for (int trial = 0; trial < 512; trial++)
        {
            int mapWidth = random.Next(1, 100), mapHeight = random.Next(1, 100);
            var cells = new byte[mapWidth * mapHeight]; random.NextBytes(cells);
            int left = random.Next((mapWidth + 16) / 17) * 17, bottom = random.Next((mapHeight + 16) / 17) * 17;
            int width = Math.Min(17, mapWidth - left), height = Math.Min(17, mapHeight - bottom), stride = width + 2;
            byte Cell(int x, int z) => cells[Math.Clamp(z, 0, mapHeight - 1) * mapWidth + Math.Clamp(x, 0, mapWidth - 1)];
            for (int z = -1; z <= height; z++) for (int x = -1; x <= width; x++)
                samples[(z + 1) * stride + x + 1] = Cell(left + x, bottom + z);
            var vertices = new byte[width * height * 9];
            FogAppearance.FillSection(samples, width, height, vertices);
            int offset = 0;
            for (int x = 0; x < width; x++) for (int z = 0; z < height; z++)
            for (int vertex = 0; vertex < 9; vertex++, offset++)
            {
                byte expected = 0;
                foreach (int neighbor in neighbors[vertex])
                    expected = Math.Max(expected, Cell(left + x + neighbor % 3 - 1, bottom + z + neighbor / 3 - 1));
                Assert.Equal(expected, vertices[offset]);
            }
        }
    }
    [Fact]
    public void BatchedSectionsAllocateNothingAfterWarmup()
    {
        var samples = new byte[19 * 19]; var vertices = new byte[17 * 17 * 9];
        for (int i = 0; i < 10; i++) FogAppearance.FillSection(samples, 17, 17, vertices);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) FogAppearance.FillSection(samples, 17, 17, vertices);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
    [Fact]
    public void BatchedSectionsRejectInvalidSampleAndVertexDimensions()
    {
        Assert.Throws<ArgumentException>(() => FogAppearance.FillSection(new byte[8], 1, 1, new byte[9]));
        Assert.Throws<ArgumentException>(() => FogAppearance.FillSection(new byte[9], 1, 1, new byte[8]));
        Assert.Throws<ArgumentException>(() => FogAppearance.FillSection(new byte[9], 0, 1, new byte[0]));
    }
    [Theory]
    [InlineData(0,255,0,20,0)] [InlineData(0,255,1,20,20)]
    [InlineData(255,0,1,20,235)] [InlineData(255,86,20,20,86)]
    [InlineData(0,255,2147483647,2147483647,255)]
    [InlineData(0,255,1,0,1)]
    public void FadeClampsWithoutOverflow(byte current,byte target,int ticks,int speed,byte expected)
        => Assert.Equal(expected,FogAppearance.Advance(current,target,ticks,speed));
}
