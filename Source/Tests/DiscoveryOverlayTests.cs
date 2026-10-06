using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;
public sealed class DiscoveryOverlayTests
{
    [Theory]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, true, true, false, false)]
    public void OverlayRespectsDiscoveryBypassAndTheOriginalResult(bool initialized, bool known, bool bypass, bool original, bool expected)
    {
        var map = new Map { IsPlayerHome = bypass };
        TotalFog.FogSettings.OnlyOutsideColony = bypass;
        map.Fog.Initialized = initialized; map.Fog.knownCells[1] = known;
        bool result = original;
        DiscoveryOverlays.OverlayPostfix(1, map, ref result);
        TotalFog.FogSettings.OnlyOutsideColony = false;
        Assert.Equal(expected, result);
    }
    [Theory]
    [InlineData(-1)] [InlineData(4)]
    public void InvalidIndicesNeverRevealData(int index)
    {
        var map = new Map(); map.Fog.Initialized = true;
        bool result = true;
        DiscoveryOverlays.OverlayPostfix(index, map, ref result);
        Assert.False(result);
    }
}
