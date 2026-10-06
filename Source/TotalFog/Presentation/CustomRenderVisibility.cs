using System;
using System.Collections.Generic;
using Verse;

namespace TotalFog.Presentation;

/// <summary>Opt-in draw gates for renderers whose footprint is not their root cell.</summary>
internal static class CustomRenderVisibility
{
    private sealed class Provider(Func<Thing, bool> query)
    {
        internal readonly Func<Thing, bool> Query = query;
        internal bool Failed;
    }

    private static readonly Dictionary<Type, Provider> providers = new();

    internal static void Register(Type type, Func<Thing, bool> query)
    {
        if (type == null || !typeof(Thing).IsAssignableFrom(type))
            throw new ArgumentException("Register an exact Thing type.", nameof(type));
        if (query == null) providers.Remove(type);
        else providers[type] = new Provider(query);
    }

    internal static bool TryQuery(Thing thing, out bool visible)
    {
        visible = false;
        if (providers.Count == 0 || !providers.TryGetValue(thing.GetType(), out var provider)) return false;
        if (provider.Failed) return true;
        try { visible = provider.Query(thing); }
        catch (Exception error)
        {
            // A broken optional provider must not reveal its complete body via
            // the ordinary root gate. Suppress it and report only the first fault.
            provider.Failed = true;
            Log.Warning($"Total Fog suppressed the custom renderer for {thing.GetType().FullName}: {error}");
        }
        return true;
    }
}
