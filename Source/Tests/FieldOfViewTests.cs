using System;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class FieldOfViewTests
{
    static bool[] Cast(bool[] blockers, int w, int h, int x, int z, int radius)
    {
        var result = new bool[w*h];
        FieldOfView.Compute(w,h,x,z,radius,blockers,i=>result[i]=true);
        return result;
    }
    [Theory]
    [InlineData(0, 0)] [InlineData(0, 6)] [InlineData(10, 0)] [InlineData(10, 6)] [InlineData(5, 3)]
    public void OpenFieldMatchesCircleAtEveryEdge(int x,int z)
    {
        var seen=Cast(new bool[77],11,7,x,z,4);
        for(int i=0;i<77;i++) Assert.Equal(Math.Pow(i%11-x,2)+Math.Pow(i/11-z,2)<=16,seen[i]);
    }
    [Fact]
    public void VisibleWallOccludesCellsBehindIt()
    {
        var blocked=new bool[81];for(int z=0;z<9;z++)blocked[z*9+4]=true;
        var seen=Cast(blocked,9,9,2,4,8);
        Assert.True(seen[4*9+4]); Assert.False(seen[4*9+5]); Assert.False(seen[0*9+8]);
    }
    [Fact]
    public void RandomFloorVisibilityIsReciprocal()
    {
        var rng=new Random(435);var blocked=new bool[99];
        for(int i=0;i<99;i++)blocked[i]=rng.Next(4)==0;
        for(int a=0;a<99;a++)
        {
            if(blocked[a])continue;
            var seen=Cast(blocked,11,9,a%11,a/11,20);
            for(int b=a+1;b<99;b++)
                if(!blocked[b])
                {
                    Assert.Equal(seen[b],Cast(blocked,11,9,b%11,b/11,20)[a]);
                    Assert.Equal(seen[b],FieldOfView.CanSee(11,9,a%11,a/11,b%11,b/11,20,blocked));
                }
        }
    }
    [Fact]
    public void ZeroRangeShowsOnlyOriginAndNegativeRangeShowsNothing()
    {
        Assert.Single(Array.FindAll(Cast(new bool[25],5,5,2,2,0),v=>v));
        Assert.Empty(Array.FindAll(Cast(new bool[25],5,5,2,2,-1),v=>v));
    }
    [Fact]
    public void DoesNotChangeTheOcclusionGrid()
    {
        var blocked=new bool[81];blocked[40]=true;blocked[41]=true;
        var original=(bool[])blocked.Clone(); Cast(blocked,9,9,4,4,8);
        Assert.Equal(original,blocked);
    }
    [Fact]
    public void RepeatedCastsAllocateNothingAfterWarmup()
    {
        var blockers = new bool[250*180]; int observed = 0;
        Action<int> reveal = _ => observed++;
        for(int i=0;i<20;i++)FieldOfView.Compute(250,180,125,90,60,blockers,reveal);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++)FieldOfView.Compute(250,180,125,90,60,blockers,reveal);
        Assert.Equal(0,GC.GetAllocatedBytesForCurrentThread()-before);
        Assert.True(observed>0);
    }

    [Fact]
    public void RangeBoundaryCannotCastShadowsInsideAnOpenCircle()
    {
        for(int radius=0;radius<=20;radius++)
        foreach(var origin in new[]{(0,0),(5,8),(10,16)})
        {
            var mask=Cast(new bool[11*17],11,17,origin.Item1,origin.Item2,radius);
            for(int z=0;z<17;z++)for(int x=0;x<11;x++)
            {
                int dx=x-origin.Item1,dz=z-origin.Item2;
                Assert.Equal(dx*dx+dz*dz<=radius*radius,mask[z*11+x]);
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PointQueriesValidateOcclusionEvenForTheOrigin(bool nullGrid)
    {
        var grid = nullGrid ? null! : new bool[8];
        Assert.Throws<ArgumentException>(() => FieldOfView.CanSee(3, 3, 1, 1, 1, 1, 4, grid));
    }

    [Fact]
    public void DirectFootprintsMatchCallbackCastsAcrossClippedMapsAndRepeatedUse()
    {
        var rng = new Random(891);
        var mask = new VisibilityMask();
        for (int c = 0; c < 512; c++)
        {
            int w = rng.Next(1, 40), h = rng.Next(1, 30), x = rng.Next(w), z = rng.Next(h);
            int radius = c % 7 == 0 ? -1 : rng.Next(0, 45);
            var blockers = new bool[w * h];
            for (int i = 0; i < blockers.Length; i++) blockers[i] = rng.Next(5) == 0;
            int left = Math.Max(0, x - Math.Max(radius, 0)), top = Math.Max(0, z - Math.Max(radius, 0));
            int right = Math.Min(w - 1, x + Math.Max(radius, 0)), bottom = Math.Min(h - 1, z + Math.Max(radius, 0));
            mask.Reset(left, top, right - left + 1, bottom - top + 1, w);
            FieldOfView.ComputeMask(w, h, x, z, radius, blockers, mask);
            var expected = Cast(blockers, w, h, x, z, radius);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], mask.Contains(i));
        }
    }

    [Fact]
    public void DirectFootprintCastsAllocateNothingAfterWarmup()
    {
        var blockers = new bool[250 * 180];
        var mask = new VisibilityMask(); mask.Reset(65, 30, 121, 121, 250);
        for (int i = 0; i < 20; i++) FieldOfView.ComputeMask(250, 180, 125, 90, 60, blockers, mask);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 100; i++) FieldOfView.ComputeMask(250, 180, 125, 90, 60, blockers, mask);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(mask.Contains(90 * 250 + 125));
    }

    [Fact]
    public void DirectFootprintsMatchCellCastingInOpenRowsShadowsAndAccumulatedPeeks()
    {
        const int width = 211, height = 139;
        var random = new Random(1862);
        var mask = new VisibilityMask();
        for (int sample = 0; sample < 96; sample++)
        {
            var blockers = new bool[width * height];
            double density = new[] { 0d, .005, .03, .15, .35 }[sample % 5];
            for (int i = 0; i < blockers.Length; i++) blockers[i] = random.NextDouble() < density;
            int x = sample % 4 == 0 ? 0 : sample % 4 == 1 ? width - 1 : 105;
            int z = sample % 6 == 0 ? 0 : sample % 6 == 1 ? height - 1 : 69;
            int radius = new[] { 1, 20, 45, 60, 130, int.MaxValue }[sample % 6];
            // A late blocker rejects an otherwise open row; nearer blockers
            // produce asymmetric rational shadow sectors on following rows.
            blockers[60 * width + 160] = true;
            blockers[67 * width + 106] = true;
            var expected = Cast(blockers, width, height, x, z, radius);
            mask.Reset(0, 0, width, height, width);
            mask.AddCell(210, 138); expected[138 * width + 210] = true;
            FieldOfView.ComputeMask(width, height, x, z, radius, blockers, mask);
            int peekX = Math.Min(x + 1, width - 1);
            FieldOfView.Compute(width, height, peekX, z, radius, blockers, i => expected[i] = true);
            FieldOfView.ComputeMask(width, height, peekX, z, radius, blockers, mask);
            for (int i = 0; i < expected.Length; i++) Assert.Equal(expected[i], mask.Contains(i));
        }
    }

}
