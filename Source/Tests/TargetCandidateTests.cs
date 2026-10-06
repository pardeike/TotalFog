using System.Collections.Generic;
using RimWorld;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class TargetCandidateTests
{
    private static Thing At(Map map, int x) => new() { Map = map, PositionHeld = new(x, 0) };

    [Fact]
    public void VisibleCandidatesRetainTheirListAndOrder()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.InSight[0] = true;
        var first = At(map, 0);
        var second = At(map, 0);
        var candidates = new List<Thing> { first, second };
        Assert.Same(candidates, InterfaceVisibility.FilterTargetThings(candidates, new(0, 0)));
        Assert.Equal(new[] { first, second }, candidates);
    }

    [Fact]
    public void HiddenCandidatesAreRemovedWithoutReorderingVisibleAlternativesOrChangingTheGrid()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.InSight[1] = true;
        var hidden = At(map, 0);
        var first = At(map, 1);
        var second = At(map, 1);
        map.Things.AddRange(new[] { hidden, first, second });
        var candidates = new List<Thing> { hidden, first, hidden, second, hidden };
        Assert.Same(candidates, InterfaceVisibility.FilterTargetThings(candidates, new(0, 0)));
        Assert.Equal(new[] { first, second }, candidates);
        Assert.Equal(new[] { hidden, first, second }, map.Things);
    }

    [Fact]
    public void NoHiddenCandidateRemainsWhenOnlyThingsAreAllowed()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        var candidates = new List<Thing> { At(map, 0), At(map, 1) };
        Assert.Empty(InterfaceVisibility.FilterTargetThings(candidates, new(0, 0)));
    }

    [Fact]
    public void VisibleProxyDoesNotOfferItsUnseenSelectionTarget()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.InSight[1] = true;
        var target = At(map, 0);
        var proxy = At(map, 1);
        proxy.Proxy = new CompSelectProxy { thingToSelect = target };
        Assert.Empty(InterfaceVisibility.FilterTargetThings(new() { proxy }, proxy.Position));
        map.Fog.InSight[0] = true;
        Assert.Equal(new[] { proxy }, InterfaceVisibility.FilterTargetThings(new() { proxy }, proxy.Position));
    }

    [Fact]
    public void UninitializedFogRetainsNativeCandidates()
    {
        var map = new Map();
        var thing = At(map, 0);
        Assert.Equal(new[] { thing }, InterfaceVisibility.FilterTargetThings(new() { thing }, thing.Position));
    }
}
