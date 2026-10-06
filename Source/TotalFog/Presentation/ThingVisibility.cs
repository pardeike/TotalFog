using RimWorld;
using Verse;
namespace TotalFog.Presentation;

/// <summary>The common information boundary for game and modded things, including held pawns.</summary>
internal static class ThingVisibility
{
    internal static bool Bypass(Map map) => FogSettings.OnlyOutsideColony && map.IsPlayerHome ||
        Compatibility.GravshipVisibility.Revealed;
    // A visibility component already owns its observation flag and a current
    // map registration. Other callers resolve those inputs through the thing.
    internal static bool IsVisible(Thing thing, bool allowMemory = true, IntVec3? renderedCell = null,
        MapVisibility registeredVisibility = null, bool? observed = null)
    {
        if (thing == null || thing is Mote_HearingCue) return true;
        var map = thing.MapHeld;
        if (map == null) return true;
        // Inspection and selection follow an explicitly registered interactive
        // core. Rendering and remembered appearance retain their own gates.
        if (!allowMemory && CustomInspectionCell.TryGetCell(thing, out var inspectionCell))
            return CellVisibility.IsCurrent(map, inspectionCell, registeredVisibility) &&
                (!renderedCell.HasValue || CellVisibility.IsCurrent(map, renderedCell.Value, registeredVisibility));
        // Visibility is a read-only query. PawnFlyer.DrawPos changes Position
        // and the thing grid; only the rendering adapter may request it.
        var position = renderedCell ?? thing.PositionHeld;
        if (!position.InBounds(map)) return false;
        if (map.fogGrid.IsFogged(position)) return false;
        bool ownObserver = thing is Pawn pawn && pawn.Faction == Faction.OfPlayer ||
            thing is PawnFlyer ownedFlyer && ownedFlyer.FlyingPawn?.Faction == Faction.OfPlayer;
        // Ownership only grants the pawn presentation exception. Live UI and
        // events require current sight; vanilla fog precedes every exception.
        if (allowMemory && ownObserver) return true;
        var fog = registeredVisibility ?? map.GetVisibility();
        if (!fog.Initialized || Bypass(map)) return true;
        bool mobile = thing is Pawn or PawnFlyer || thing.def.category == ThingCategory.Projectile || thing.def.category == ThingCategory.Mote;
        bool canRemember = allowMemory && !mobile &&
            (observed ?? thing.TryGetComp<CompFog>()?.HideFromPlayer?.SeenByPlayer) == true;
        var rect = thing.Spawned && thing is not PawnFlyer ? thing.OccupiedRect() : CellRect.SingleCell(position);
        if (rect.Area == 1)
            return canRemember && fog.knownCells[map.cellIndices.CellToIndex(position)] ||
                fog.IsShown(Faction.OfPlayer, position);
        if (renderedCell.HasValue && thing.Spawned && thing is not PawnFlyer)
            rect = rect.MovedBy(position.x - thing.Position.x, position.z - thing.Position.z);
        rect.ClipInsideMap(map);
        foreach (var cell in rect)
        {
            int index = map.cellIndices.CellToIndex(cell);
            if (canRemember && fog.knownCells[index] || fog.IsShown(Faction.OfPlayer, cell)) return true;
        }
        return false;
    }
}
