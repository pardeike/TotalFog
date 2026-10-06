using System;
using System.Threading.Tasks;
using TotalFog.Presentation;
using UnityEngine;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class FleckVisibilityTests
{
    [Fact]
    public async Task AParticleFollowsCurrentSightAndPositionOnTheBatchWorkers()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.knownCells[0] = true;
        map.Fog.InSight[1] = true;
        var particle = new FleckStatic { DrawPos = new Vector3(.5f, 9, .5f) };
        FleckVisibility.BeginDrawing(new FleckManager { parent = map }, out var prior);
        try
        {
            Assert.False(await Task.Run(() => FleckVisibility.DrawPrefix(ref particle)));
            particle.DrawPos.x = 1.5f;
            Assert.True(await Task.Run(() => FleckVisibility.DrawPrefix(ref particle)));
            map.Fog.InSight[1] = false;
            Assert.False(await Task.Run(() => FleckVisibility.DrawPrefix(ref particle)));
            Assert.Equal(1.5f, particle.DrawPos.x);
            Assert.Equal(9, particle.DrawPos.y);
            // Owner resolution happens once per batch, never per particle.
            Assert.Equal(1, map.ComponentLookups);
        }
        finally { FleckVisibility.EndDrawing(prior); }
        Assert.True(FleckVisibility.DrawPrefix(ref particle));
    }

    [Fact]
    public void NestedBatchFailureRestoresTheCorrectMapWithoutUsingCurrentMap()
    {
        var outer = new Map();
        outer.Fog.Initialized = true;
        var inner = new Map();
        inner.Fog.Initialized = true;
        inner.Fog.InSight[0] = true;
        Find.CurrentMap = inner;
        var particle = new FleckStatic { DrawPos = new Vector3(.5f, 0, .5f) };
        FleckVisibility.BeginDrawing(new FleckManager { parent = outer }, out var prior);
        try
        {
            Assert.False(FleckVisibility.DrawPrefix(ref particle));
            Assert.Throws<InvalidOperationException>((Action)(() =>
            {
                FleckVisibility.BeginDrawing(new FleckManager { parent = inner }, out var nested);
                try
                {
                    Assert.True(FleckVisibility.DrawPrefix(ref particle));
                    throw new InvalidOperationException("Native draw failed");
                }
                finally { FleckVisibility.EndDrawing(nested); }
            }));
            Assert.False(FleckVisibility.DrawPrefix(ref particle));
        }
        finally { FleckVisibility.EndDrawing(prior); Find.CurrentMap = null; }
        Assert.True(FleckVisibility.DrawPrefix(ref particle));
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, true, true, false)]
    public void InitializationAndColonyFallbackStillRespectVanillaFog(bool initialized, bool colony, bool vanillaFog, bool expected)
    {
        var map = new Map { IsPlayerHome = colony };
        map.Fog.Initialized = initialized;
        map.fogGrid.Fogged = vanillaFog;
        FogSettings.OnlyOutsideColony = colony;
        var particle = new FleckStatic { DrawPos = new Vector3(.5f, 0, .5f) };
        FleckVisibility.BeginDrawing(new FleckManager { parent = map }, out var prior);
        try { Assert.Equal(expected, FleckVisibility.DrawPrefix(ref particle)); }
        finally { FleckVisibility.EndDrawing(prior); FogSettings.OnlyOutsideColony = false; }
    }

    [Fact]
    public void ParticleOutsideTheMapNeverReadsTheSightGrid()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        var particle = new FleckStatic { DrawPos = new Vector3(-.1f, 0, .5f) };
        FleckVisibility.BeginDrawing(new FleckManager { parent = map }, out var prior);
        try
        {
            Assert.False(FleckVisibility.DrawPrefix(ref particle));
            Assert.Equal(0, map.Fog.VisibilityQueries);
        }
        finally { FleckVisibility.EndDrawing(prior); }
    }
}
