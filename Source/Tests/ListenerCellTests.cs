using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public sealed class ListenerCellTests
{
    [Fact]
    public void EmptySingletonAndSharedCellsPreserveOrderAndSuppressDuplicates()
    {
        var cell = new ListenerCell<object>();
        var a = new object();
        var b = new object();
        var c = new object();
        Assert.Equal(0, cell.Count);
        Assert.True(cell.Add(a));
        Assert.False(cell.Add(a));
        Assert.True(cell.Add(b));
        Assert.True(cell.Add(c));
        Assert.False(cell.Add(b));
        Assert.Equal(3, cell.Count);
        Assert.Same(a, cell[0]);
        Assert.Same(b, cell[1]);
        Assert.Same(c, cell[2]);
        Assert.True(cell.Contains(a));
        Assert.False(cell.Contains(new object()));
    }

    [Fact]
    public void RemovingTheFirstPromotesOverflowInInsertionOrder()
    {
        var cell = new ListenerCell<object>();
        var a = new object();
        var b = new object();
        var c = new object();
        cell.Add(a);
        cell.Add(b);
        cell.Add(c);
        Assert.True(cell.Remove(a));
        Assert.False(cell.Remove(a));
        Assert.Equal(2, cell.Count);
        Assert.Same(b, cell[0]);
        Assert.Same(c, cell[1]);
        Assert.True(cell.Remove(b));
        Assert.Equal(1, cell.Count);
        Assert.Same(c, cell[0]);
        Assert.True(cell.Remove(c));
        Assert.Equal(0, cell.Count);
        Assert.False(cell.Remove(c));
        Assert.True(cell.Add(a));
        Assert.Same(a, cell[0]);
    }

    [Fact]
    public void RemovingMiddleAndLastPreservesTheRemainingListeners()
    {
        var cell = new ListenerCell<object>();
        var a = new object();
        var b = new object();
        var c = new object();
        cell.Add(a);
        cell.Add(b);
        cell.Add(c);
        Assert.False(cell.Remove(new object()));
        Assert.True(cell.Remove(b));
        Assert.Equal(2, cell.Count);
        Assert.Same(a, cell[0]);
        Assert.Same(c, cell[1]);
        Assert.True(cell.Remove(c));
        Assert.Equal(1, cell.Count);
        Assert.Same(a, cell[0]);
        Assert.True(cell.Add(b));
        Assert.Equal(2, cell.Count);
        Assert.Same(b, cell[1]);
    }

    [Fact]
    public void InvalidIndicesAndNullListenersFailWithoutChangingTheCell()
    {
        var cell = new ListenerCell<object>();
        Assert.Throws<ArgumentOutOfRangeException>(() => cell[0]);
        Assert.Throws<ArgumentNullException>(() => cell.Add(null));
        Assert.Equal(0, cell.Count);
        cell.Add(new object());
        Assert.Throws<ArgumentOutOfRangeException>(() => cell[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => cell[1]);
        Assert.Equal(1, cell.Count);
    }

    [Fact]
    public void DefaultEqualityMatchesListSemantics()
    {
        var cell = new ListenerCell<string>();
        string a = new(new[] { 'a' }),
            equal = new(new[] { 'a' });
        Assert.True(cell.Add(a));
        Assert.False(cell.Add(equal));
        Assert.True(cell.Contains(equal));
        Assert.True(cell.Remove(equal));
        Assert.Equal(0, cell.Count);
    }

    [Fact]
    public void MixedRegistrationSequenceMatchesAnOrderedUniqueList()
    {
        var random = new Random(519);
        var values = new object[17];
        for (int i = 0; i < values.Length; i++)
            values[i] = new object();
        var expected = new List<object>();
        var cell = new ListenerCell<object>();
        for (int iteration = 0; iteration < 10000; iteration++)
        {
            var value = values[random.Next(values.Length)];
            if (random.Next(2) == 0)
            {
                bool added = !expected.Contains(value);
                if (added)
                    expected.Add(value);
                Assert.Equal(added, cell.Add(value));
            }
            else
                Assert.Equal(expected.Remove(value), cell.Remove(value));
            Assert.Equal(expected.Count, cell.Count);
            for (int i = 0; i < expected.Count; i++)
                Assert.Same(expected[i], cell[i]);
            foreach (var item in values)
                Assert.Equal(expected.Contains(item), cell.Contains(item));
        }
    }

    [Fact]
    public void RepeatedSingletonUseAllocatesNothingAfterWarmup()
    {
        var cell = new ListenerCell<object>();
        var value = new object();
        for (int i = 0; i < 100; i++)
        {
            cell.Add(value);
            cell.Remove(value);
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++)
        {
            cell.Add(value);
            cell.Remove(value);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(0, cell.Count);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void ArrayElementChangesStayInTheirOwnCell()
    {
        var cells = new ListenerCell<object>[2];
        var a = new object();
        var b = new object();
        cells[1].Add(a);
        cells[1].Add(b);
        cells[1].Remove(a);
        Assert.Equal(0, cells[0].Count);
        Assert.Equal(1, cells[1].Count);
        Assert.Same(b, cells[1][0]);
        cells[1].Remove(b);
        Assert.Equal(0, cells[1].Count);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (ListenerCell<object>, WeakReference<object>[]) ReleaseSharedCell()
    {
        var cell = new ListenerCell<object>();
        var a = new object();
        var b = new object();
        var c = new object();
        var references = new[]
        {
            new WeakReference<object>(a),
            new WeakReference<object>(b),
            new WeakReference<object>(c),
        };
        cell.Add(a);
        cell.Add(b);
        cell.Add(c);
        cell.Remove(a);
        cell.Remove(c);
        cell.Remove(b);
        return (cell, references);
    }

    [Fact]
    public void EmptyCellsReleaseAllTheirListenerReferences()
    {
        var (cell, references) = ReleaseSharedCell();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        foreach (var reference in references)
            Assert.False(reference.TryGetTarget(out _));
        Assert.Equal(0, cell.Count);
        GC.KeepAlive(cell);
    }
}
