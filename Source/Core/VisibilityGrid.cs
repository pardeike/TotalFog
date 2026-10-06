#nullable enable
using System;
using System.Collections.Generic;

namespace TotalFog.Core;

/// <summary>Coverage is transient; discovery is durable. No game types or DLC rules.</summary>
public sealed class VisibilityGrid
{
    private readonly Dictionary<int, int[]> factions = new();
    private int[]? playerCounts;
    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public bool[] Known { get; }
    public int FactionCount => factions.Count + (playerCounts == null ? 0 : 1);

    public VisibilityGrid(int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        CellCount = checked(width * height);
        Known = new bool[CellCount];
    }

    public bool InBounds(int x, int z) => (uint)x < Width && (uint)z < Height;

    public int Index(int x, int z)
    {
        if (!InBounds(x, z))
            throw new ArgumentOutOfRangeException(nameof(x));
        return z * Width + x;
    }

    public int X(int index) => index % Width;

    public int Z(int index) => index / Width;

    public int[] Counts(int faction)
    {
        // Player coverage is the common rendering and sight-update path.
        // Keep it directly; the dictionary is only for optional NPC grids.
        if (faction == 0)
            return playerCounts ??= new int[CellCount];
        if (!factions.TryGetValue(faction, out var counts))
            factions.Add(faction, counts = new int[CellCount]);
        return counts;
    }

    public bool IsVisible(int faction, int x, int z)
    {
        if (!InBounds(x, z))
            return false;
        var counts = playerCounts;
        if (faction != 0 && !factions.TryGetValue(faction, out counts))
            return false;
        return counts != null && counts[z * Width + x] > 0;
    }

    public bool Add(int faction, int index)
    {
        CheckIndex(index);
        var counts = Counts(faction);
        counts[index] = checked(counts[index] + 1);
        if (faction == 0)
            Known[index] = true;
        return counts[index] == 1;
    }

    public bool Remove(int faction, int index)
    {
        CheckIndex(index);
        var counts = playerCounts;
        if (faction != 0 && !factions.TryGetValue(faction, out counts))
            return false;
        if (counts == null || counts[index] == 0)
            return false;
        return --counts[index] == 0;
    }

    public void LoadKnown(bool[] saved)
    {
        if (saved == null || saved.Length != CellCount)
            throw new ArgumentException("Discovery dimensions do not match the map", nameof(saved));
        Array.Copy(saved, Known, CellCount);
    }

    private void CheckIndex(int index)
    {
        if ((uint)index >= CellCount)
            throw new ArgumentOutOfRangeException(nameof(index));
    }
}
