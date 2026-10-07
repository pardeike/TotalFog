using RimWorld;
using Verse;

namespace TotalFog.Presentation;

/// <summary>Current observation for live cell information, independent of exploration memory.</summary>
internal static class CellVisibility
{
    internal static bool IsCurrent(
        Map map,
        IntVec3 cell,
        MapVisibility fog = null,
        Faction observerFaction = null
    )
    {
        if (map == null || !cell.InBounds(map) || map.fogGrid.IsFogged(cell))
            return false;
        if (ThingVisibility.Unrestricted(map, observerFaction))
            return true;
        fog ??= map.GetVisibility();
        return !fog.Initialized || fog.IsShown(observerFaction ?? Faction.OfPlayer, cell);
    }
}
