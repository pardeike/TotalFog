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
    public void OverlayRespectsDiscoveryBypassAndTheOriginalResult(
        bool initialized,
        bool known,
        bool bypass,
        bool original,
        bool expected
    )
    {
        var map = new Map { IsPlayerHome = bypass };
        TotalFog.FogSettings.OnlyOutsideColony = bypass;
        map.Fog.Initialized = initialized;
        map.Fog.knownCells[1] = known;
        bool result = DiscoveryOverlays.VisibleCell(
            _ => original,
            1,
            DiscoveryOverlays.KnownCells(map)
        );
        TotalFog.FogSettings.OnlyOutsideColony = false;
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void InvalidIndicesNeverRevealData(int index)
    {
        var map = new Map();
        map.Fog.Initialized = true;
        bool result = DiscoveryOverlays.VisibleCell(
            _ => true,
            index,
            DiscoveryOverlays.KnownCells(map)
        );
        Assert.False(result);
    }

    [Fact]
    public void RegenerationSnapshotKeepsTheViewedFactionAndNativeFalseResult()
    {
        var first = Faction.OfPlayer;
        var second = new Faction { loadID = 2, IsPlayer = true };
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.knownCells[0] = true;
        map.Fog.FactionKnown[second] = new[] { false, true, false, false };
        try
        {
            var firstSnapshot = DiscoveryOverlays.KnownCells(map);
            Faction.OfPlayer = second;
            var secondSnapshot = DiscoveryOverlays.KnownCells(map);
            Assert.True(DiscoveryOverlays.VisibleCell(_ => true, 0, firstSnapshot));
            Assert.False(DiscoveryOverlays.VisibleCell(_ => true, 0, secondSnapshot));
            Assert.True(DiscoveryOverlays.VisibleCell(_ => true, 1, secondSnapshot));
            Assert.False(DiscoveryOverlays.VisibleCell(_ => false, 1, secondSnapshot));
            map.Fog.knownCells[2] = true;
            Assert.True(DiscoveryOverlays.VisibleCell(_ => true, 2, firstSnapshot));
        }
        finally
        {
            Faction.OfPlayer = first;
        }
    }
}
