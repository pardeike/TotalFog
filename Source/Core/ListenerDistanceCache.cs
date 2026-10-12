using System;
using System.Collections.Generic;

namespace TotalFog.Core;

/// <summary>One map's listener snapshot and source distances for one faction and map tick.</summary>
public sealed class ListenerDistanceCache
{
    private readonly List<Listener> listeners = new();
    private readonly Dictionary<int, float> distances = new();
    private int tick,
        faction;
    private bool sampled;

    public bool Begin(int mapTick, int factionId)
    {
        if (sampled && tick == mapTick && faction == factionId)
            return false;
        sampled = true;
        tick = mapTick;
        faction = factionId;
        listeners.Clear();
        distances.Clear();
        return true;
    }

    public void Add(int x, int z, float hearing)
    {
        if (hearing > 0)
            listeners.Add(new Listener(x, z, hearing));
    }

    public float Nearest(int cellIndex, int x, int z)
    {
        if (distances.TryGetValue(cellIndex, out float cached))
            return cached;
        float nearest = float.PositiveInfinity;
        foreach (var listener in listeners)
        {
            double dx = listener.X - x,
                dz = listener.Z - z;
            float distance = (float)Math.Sqrt(dx * dx + dz * dz) / listener.Hearing;
            nearest = Math.Min(nearest, distance);
        }
        distances.Add(cellIndex, nearest);
        return nearest;
    }

    private readonly struct Listener
    {
        internal readonly int X,
            Z;
        internal readonly float Hearing;

        internal Listener(int x, int z, float hearing)
        {
            X = x;
            Z = z;
            Hearing = hearing;
        }
    }
}
