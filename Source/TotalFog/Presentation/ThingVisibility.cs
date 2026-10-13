using RimWorld;
using Verse;

namespace TotalFog.Presentation;

/// <summary>The common information boundary for game and modded things, including held pawns.</summary>
internal static class ThingVisibility
{
    // Owned pawns stay under the player's control even when they supply no
    // sight, such as untrained animals. This UI exception never grants sight
    // to effects, enemy targets, discovered entities or neighbouring cells.
    internal static bool CanInspect(Thing thing, IntVec3? renderedCell = null) =>
        IsVisible(
            thing,
            allowMemory: thing is Pawn pawn && pawn.Faction == Faction.OfPlayer,
            renderedCell: renderedCell
        );

    internal static bool Unrestricted(Map map, Faction observerFaction = null) =>
        FogSettings.OnlyOutsideColony && IsHomeFor(map, observerFaction);

    internal static bool IsHomeFor(Map map, Faction observerFaction = null)
    {
        observerFaction ??= Faction.OfPlayer;
        if (observerFaction == Faction.OfPlayer || observerFaction?.IsPlayer != true)
            return map.IsPlayerHome;
        // Match native home-map rules for an explicit player observer without
        // pushing shared faction/map data for each visibility query.
        return map.wasSpawnedViaGravShipLanding
            || map.ParentFaction == observerFaction && map.Parent?.def.canBePlayerHome == true
            || GravshipUtility.PlayerHasGravEngine(map);
    }

    internal static bool Bypass(Map map) =>
        Unrestricted(map) || Compatibility.GravshipVisibility.Revealed;

    // Ownership grants work access, not live effects or notification discovery.
    internal static bool IsActionable(Thing thing, Faction observerFaction)
    {
        if (thing == null || IsVisible(thing, observerFaction: observerFaction))
            return true;
        var map = thing.MapHeld;
        if (
            map == null
            || !thing.PositionHeld.InBounds(map)
            || map.fogGrid.IsFogged(thing.PositionHeld)
        )
            return false;
        var fog = map.GetVisibility();
        var cell = thing.PositionHeld;
        if (KnownActionable(thing, fog, cell, observerFaction))
            return true;
        if (
            observerFaction == null
            || !fog.IsKnown(observerFaction, map.cellIndices.CellToIndex(cell))
            || thing.def.category != ThingCategory.Item
            || !thing.def.EverHaulable
        )
            return false;
        // An owned construction order also authorizes clearing its footprint.
        // Keep this work permission separate from remembered rendering: no
        // haul designation or observation is invented for the obstruction.
        var things = cell.GetThingList(map);
        for (int i = 0; i < things.Count; i++)
        {
            var plan = things[i];
            if (
                plan.Faction == observerFaction
                && (plan.def.IsBlueprint || plan.def.IsFrame)
                && GenConstruct.BlocksConstruction(plan, thing)
            )
                return true;
        }
        return false;
    }

    private static bool KnownActionable(
        Thing thing,
        MapVisibility fog,
        IntVec3 cell,
        Faction observerFaction
    )
    {
        if (
            observerFaction == null
            || !fog.IsKnown(observerFaction, thing.MapHeld.cellIndices.CellToIndex(cell))
        )
            return false;
        if (thing is Corpse corpse && corpse.InnerPawn?.Faction == observerFaction)
            return true;
        return fog.HomeArea[cell] && (thing is Fire || thing.Faction == observerFaction);
    }

    // A visibility component already owns its observation flag and a current
    // map registration. Other callers resolve those inputs through the thing.
    internal static bool IsVisible(
        Thing thing,
        bool allowMemory = true,
        IntVec3? renderedCell = null,
        MapVisibility registeredVisibility = null,
        bool? observed = null,
        Faction observerFaction = null
    )
    {
        if (thing == null)
            return true;
        observerFaction ??= Faction.OfPlayer;
        if (thing is Mote_HearingCue cue)
            return cue.ObserverFactionId == 0 || cue.ObserverFactionId == observerFaction?.loadID;
        var map = thing.MapHeld;
        if (map == null)
            return true;
        // Inspection and selection follow an explicitly registered interactive
        // core. Rendering and remembered appearance retain their own gates.
        if (!allowMemory && CustomInspectionCell.TryGetCell(thing, out var inspectionCell))
            return CellVisibility.IsCurrent(
                    map,
                    inspectionCell,
                    registeredVisibility,
                    observerFaction
                )
                && (
                    !renderedCell.HasValue
                    || CellVisibility.IsCurrent(
                        map,
                        renderedCell.Value,
                        registeredVisibility,
                        observerFaction
                    )
                );
        // Visibility is a read-only query. PawnFlyer.DrawPos changes Position
        // and the thing grid; only the rendering adapter may request it.
        var position = renderedCell ?? thing.PositionHeld;
        if (!position.InBounds(map))
            return false;
        if (map.fogGrid.IsFogged(position))
            return false;
        bool ownObserver =
            thing is Pawn pawn && pawn.Faction == observerFaction
            || thing is PawnFlyer ownedFlyer && ownedFlyer.FlyingPawn?.Faction == observerFaction;
        // Ownership only grants the pawn presentation exception. Live UI and
        // events require current sight; vanilla fog precedes every exception.
        bool ownPlan = thing.def.IsBlueprint && thing.Faction == observerFaction;
        if (allowMemory && (ownObserver || ownPlan))
            return true;
        var fog = registeredVisibility ?? map.GetVisibility();
        // Landing previews can draw hidden geometry, but must not grant current
        // sight to effects, target information or saved notification state.
        if (
            !fog.Initialized
            || Unrestricted(map, observerFaction)
            || allowMemory && Compatibility.GravshipVisibility.Revealed
        )
            return true;
        if (allowMemory && KnownActionable(thing, fog, position, observerFaction))
            return true;
        bool mobile =
            thing is Pawn or PawnFlyer
            || thing.def.category == ThingCategory.Projectile
            || thing.def.category == ThingCategory.Mote;
        bool canRemember =
            allowMemory
            && !mobile
            && thing is not Corpse
            && (
                (
                    observed
                    ?? thing.TryGetComp<CompFog>()?.HideFromPlayer?.WasSeenBy(observerFaction)
                ) == true
                // Plain vanilla chunks cannot save an observation component.
                // An existing haul order retains the player's explicit intent;
                // the faction's known terrain still gates it below.
                || thing is not ThingWithComps
                    && thing.def.designateHaulable
                    && map.designationManager.DesignationOn(thing, DesignationDefOf.Haul) != null
            );
        // The queried cell belongs to the footprint, including when rendering
        // offsets it. Most queries are single-cell pawns/items; avoid building
        // their occupied rectangle, and finish remembered anchor hits here.
        if (canRemember && fog.IsKnown(observerFaction, map.cellIndices.CellToIndex(position)))
            return true;
        if (!thing.Spawned || thing is PawnFlyer || thing.def.size.x == 1 && thing.def.size.z == 1)
            return fog.IsShown(observerFaction, position);
        var rect =
            thing.Spawned && thing is not PawnFlyer
                ? thing.OccupiedRect()
                : CellRect.SingleCell(position);
        if (rect.Area == 1)
            return canRemember
                    && fog.IsKnown(observerFaction, map.cellIndices.CellToIndex(position))
                || fog.IsShown(observerFaction, position);
        if (renderedCell.HasValue && thing.Spawned && thing is not PawnFlyer)
            rect = rect.MovedBy(position.x - thing.Position.x, position.z - thing.Position.z);
        rect.ClipInsideMap(map);
        foreach (var cell in rect)
        {
            int index = map.cellIndices.CellToIndex(cell);
            if (
                canRemember && fog.IsKnown(observerFaction, index)
                || fog.IsShown(observerFaction, cell)
            )
                return true;
        }
        return false;
    }
}
