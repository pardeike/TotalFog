using System;
using System.Collections.Generic;
using Verse;

namespace TotalFog.Presentation;

/// <summary>Explicit inspection anchors for things whose interactive core moves independently.</summary>
internal static class CustomInspectionCell
{
    private sealed class Provider(Func<Thing, IntVec3> query)
    {
        internal readonly Func<Thing, IntVec3> Query = query;
        internal bool Failed;
    }

    private static readonly Dictionary<Type, Provider> providers = new();

    internal static void Register(Type type, Func<Thing, IntVec3> query)
    {
        if (type == null || !typeof(Thing).IsAssignableFrom(type))
            throw new ArgumentException("Register an exact Thing type.", nameof(type));
        if (query == null)
            providers.Remove(type);
        else
            providers[type] = new Provider(query);
    }

    internal static bool TryGetCell(Thing thing, out IntVec3 cell)
    {
        cell = IntVec3.Invalid;
        if (providers.Count == 0 || !providers.TryGetValue(thing.GetType(), out var provider))
            return false;
        if (provider.Failed)
            return true;
        try
        {
            cell = provider.Query(thing);
        }
        catch (Exception error)
        {
            provider.Failed = true;
            Log.Warning($"Total Fog suppressed inspection of {thing.GetType().FullName}: {error}");
        }
        return true;
    }
}
