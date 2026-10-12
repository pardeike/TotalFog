using System;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public sealed class ListenerDistanceCacheTests
{
    [Fact]
    public void Hearing_snapshot_and_source_results_last_only_one_map_tick_and_faction()
    {
        var cache = new ListenerDistanceCache();
        Assert.True(cache.Begin(10, 1));
        cache.Add(0, 0, .5f);
        cache.Add(20, 0, 1);
        cache.Add(10, 0, 0);
        Assert.Equal(10, cache.Nearest(10, 10, 0));
        Assert.False(cache.Begin(10, 1));
        Assert.Equal(10, cache.Nearest(10, 10, 0));
        Assert.True(cache.Begin(11, 1));
        cache.Add(10, 0, 1);
        Assert.Equal(0, cache.Nearest(10, 10, 0));
        Assert.True(cache.Begin(11, 2));
        Assert.Equal(float.PositiveInfinity, cache.Nearest(10, 10, 0));
        Assert.True(cache.Begin(5, 2));
        cache.Add(13, 4, 1);
        Assert.Equal(5, cache.Nearest(10, 10, 0));
    }

    [Fact]
    public void Warmed_listener_and_source_cache_reuses_storage_without_allocating()
    {
        var cache = new ListenerDistanceCache();
        void Exercise(int tick)
        {
            cache.Begin(tick, 1);
            cache.Add(0, 0, 1);
            cache.Add(20, 0, .5f);
            for (int i = 0; i < 100; i++)
                cache.Nearest(i, i, 0);
            for (int i = 0; i < 100; i++)
                cache.Nearest(i, i, 0);
        }
        Exercise(1);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        Exercise(2);
        Assert.Equal(allocated, GC.GetAllocatedBytesForCurrentThread());
    }
}
