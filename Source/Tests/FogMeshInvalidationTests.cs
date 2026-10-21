using RimWorld;
using TotalFog.Presentation;
using UnityEngine;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class FogMeshInvalidationTests
{
    [Theory]
    [InlineData(DrawerType.MapMeshOnly)]
    [InlineData(DrawerType.MapMeshAndRealTime)]
    public void PlainStaticThingsRefreshWhenSightChanges(DrawerType drawer)
    {
        var map = new Map();
        map.Things.Add(new Thing { def = new ThingDef { drawerType = drawer } });
        Assert.NotEqual(
            0UL,
            FogMeshInvalidation.FlagsForSightChange(map, 0) & (ulong)MapMeshFlagDefOf.Things
        );
    }

    [Theory]
    [InlineData(DrawerType.RealtimeOnly)]
    [InlineData(DrawerType.None)]
    public void PlainRealtimeThingsDoNotInvalidateStaticMeshes(DrawerType drawer)
    {
        var map = new Map();
        map.Things.Add(new Thing { def = new ThingDef { drawerType = drawer } });
        Assert.Equal(
            0UL,
            FogMeshInvalidation.FlagsForSightChange(map, 0) & (ulong)MapMeshFlagDefOf.Things
        );
    }

    [Fact]
    public void ComponentBearingThingsKeepTheirExistingTransitionInvalidation()
    {
        var map = new Map();
        map.Things.Add(
            new ThingWithComps { def = new ThingDef { drawerType = DrawerType.MapMeshOnly } }
        );
        Assert.Equal(
            0UL,
            FogMeshInvalidation.FlagsForSightChange(map, 0) & (ulong)MapMeshFlagDefOf.Things
        );
    }

    [Fact]
    public void Unlit_sight_changes_preserve_fog_and_gas_without_rebuilding_lighting()
    {
        var map = new Map();
        ulong flags = FogMeshInvalidation.FlagsForSightChange(map, 0);
        Assert.Equal(FogDefOf.RealFogOfWar | (ulong)MapMeshFlagDefOf.Gas, flags);
        Assert.Equal(1, map.glowGrid.Reads);
    }

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 0, 0, 1)]
    public void Every_native_glow_channel_requires_a_lighting_refresh(
        byte r,
        byte g,
        byte b,
        byte a
    )
    {
        var map = new Map();
        map.glowGrid.Glow = new Color32(r, g, b, a);
        ulong flags = FogMeshInvalidation.FlagsForSightChange(map, 0);
        Assert.Equal(
            FogDefOf.RealFogOfWar
                | (ulong)MapMeshFlagDefOf.Gas
                | (ulong)MapMeshFlagDefOf.GroundGlow,
            flags
        );
        Assert.Equal(r, map.glowGrid.Glow.r);
        Assert.Equal(g, map.glowGrid.Glow.g);
        Assert.Equal(b, map.glowGrid.Glow.b);
        Assert.Equal(a, map.glowGrid.Glow.a);
    }
}
