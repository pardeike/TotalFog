using System;
using RimWorld;
using Verse;

namespace TotalFog;

/// <summary>Optional, read-only fog queries for other mods' effects, UI and targeting.</summary>
public static class Visibility
{
    /// <summary>
    /// Whether the player can currently see a thing. Remembered appearance and
    /// player ownership do not disclose live effects outside current sight.
    /// Uses the held map and ordinary footprint or registered inspection cell,
    /// vanilla fog and Total Fog's existing bypasses.
    /// Null or mapless things follow the engine's unrestricted presentation policy.
    /// Call on the game thread; this method does not change simulation or sight.
    /// </summary>
    public static bool IsVisible(Thing thing) => Presentation.ThingVisibility.IsVisible(thing, allowMemory: false);

    /// <summary>Current cell sight, including vanilla fog, initialization and colony bypasses.</summary>
    public static bool IsVisible(Map map, IntVec3 cell) => Presentation.CellVisibility.IsCurrent(map, cell);

    /// <summary>
    /// Whether the optional enemy-fog policy permits one target cell. Uses the
    /// observing humanlike pawn's faction sight, not the player's presentation
    /// sight. Returns true for observers outside that policy or when disabled.
    /// Native hostility, line-of-sight and range checks remain required.
    /// Call on the game thread; this method does not change simulation or sight.
    /// </summary>
    public static bool AllowsTarget(Thing observer, IntVec3 cell) =>
        observer is not Pawn pawn || pawn.Faction == null || pawn.Map == null ||
        !FogSettings.AISmart || pawn.Faction == Faction.OfPlayer || !pawn.RaceProps.Humanlike ||
        pawn.Map.GetVisibility().IsShown(pawn.Faction, cell);

    /// <summary>
    /// Register the current inspection/selection cell for an exact Thing type.
    /// The callback must be bounded and read-only, and return Invalid until its
    /// authoritative core is ready. Vanilla fog and current sight are checked at
    /// that cell. This does not grant drawing or targeting of hidden body cells;
    /// a custom targeting adapter must check each clicked cell separately.
    /// Call on the game thread. Null unregisters; throwing callbacks fail closed
    /// until replaced. Unregistered types retain their ordinary footprint policy.
    /// </summary>
    public static void RegisterInspectionCell(Type thingType, Func<Thing, IntVec3> cell) =>
        Presentation.CustomInspectionCell.Register(thingType, cell);

    /// <summary>
    /// Register a draw-only gate for an exact Thing type on the game thread.
    /// Native camera/depth culling remains in force. The renderer must clip every
    /// body, effect and shadow to IsVisible(map, cell); this callback only decides
    /// whether its visible parts need drawing. It must not change simulation.
    /// Live inspection and targeting keep their separate current-sight policy.
    /// Passing null unregisters the gate. A throwing gate is suppressed until replaced.
    /// </summary>
    public static void RegisterRenderer(Type thingType, Func<Thing, bool> isVisible) =>
        Presentation.CustomRenderVisibility.Register(thingType, isVisible);
}
