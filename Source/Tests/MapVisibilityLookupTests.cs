using System;
using System.Runtime.CompilerServices;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class MapVisibilityLookupTests
{
    [Fact]
    public void RepeatedQueriesKeepTheEngineComponentWithoutRepeatedListSearches()
    {
        var map = new Map();
        for (int i = 0; i < 100; i++)
            Assert.Same(map.Fog, map.GetVisibility());
        Assert.Equal(1, map.ComponentLookups);
        Assert.Single(map.components);
    }

    [Fact]
    public void SwitchingMapsNeverReturnsAnotherMapsCoverage()
    {
        var first = new Map();
        var second = new Map();
        for (int i = 0; i < 100; i++)
        {
            Assert.Same(first.Fog, first.GetVisibility());
            Assert.Same(second.Fog, second.GetVisibility());
        }
        Assert.Equal(1, first.ComponentLookups);
        Assert.Equal(1, second.ComponentLookups);
    }

    [Fact]
    public void MissingComponentIsCreatedAndRegisteredExactlyOnce()
    {
        var map = new Map();
        map.components.Clear();
        var fog = map.GetVisibility();
        Assert.Same(map, fog.Owner);
        Assert.NotSame(map.Fog, fog);
        Assert.Same(fog, map.GetVisibility());
        Assert.Same(fog, Assert.Single(map.components));
    }

    [Fact]
    public void LookupDoesNotKeepAnUnloadedMapOrItsComponentAlive()
    {
        var map = QueryDiscardedMap();
        for (int i = 0; i < 3 && map.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
        Assert.False(map.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference QueryDiscardedMap()
    {
        var map = new Map();
        Assert.Same(map.Fog, map.GetVisibility());
        return new WeakReference(map);
    }
}
