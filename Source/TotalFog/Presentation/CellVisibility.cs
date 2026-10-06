using RimWorld;
using Verse;

namespace TotalFog.Presentation;

/// <summary>Current observation for live cell information, independent of exploration memory.</summary>
internal static class CellVisibility
{
    internal static bool IsCurrent(Map map, IntVec3 cell, MapVisibility fog = null)
    {
        if (map == null || !cell.InBounds(map) || map.fogGrid.IsFogged(cell))
            return false;
        if (ThingVisibility.Bypass(map))
            return true;
        fog ??= map.GetVisibility();
        return !fog.Initialized || fog.IsShown(Faction.OfPlayer, cell);
    }
}
