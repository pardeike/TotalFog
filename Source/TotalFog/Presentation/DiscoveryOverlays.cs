using System;
using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static partial class DiscoveryOverlays
{
    internal static bool[] KnownCells(Map map)
    {
        if (map == null || ThingVisibility.Bypass(map))
            return null;
        var fog = map.GetVisibility();
        return !fog.Initialized
            ? null
            : fog.GetFactionKnownCells(Faction.OfPlayer) ?? Array.Empty<bool>();
    }

    internal static bool VisibleCell(Func<int, bool> original, int index, bool[] known) =>
        (known == null || (uint)index < known.Length && known[index]) && original(index);

    internal static bool IsKnown(Map map, int index)
    {
        var known = KnownCells(map);
        return known == null || (uint)index < known.Length && known[index];
    }
}
