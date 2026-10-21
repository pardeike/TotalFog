// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System.Collections.Generic;
using RimWorld;
using TotalFog.Presentation;
using Verse;

namespace TotalFog;

/// <summary>Presentation side effects occur only when the common visibility policy changes.</summary>
public class CompVisibility : FogSubcomponent
{
    private bool seenByPlayer;
    private int firstObserverFactionId;
    private List<int> otherObserverFactionIds;
    private int observedMapId = -1;
    private int firstObservedCell = -1;
    private Dictionary<int, int> otherObservedCells;
    private int arrivalObserverFactionId;
    public bool SeenByPlayer => WasSeenBy(Faction.OfPlayer);

    public bool WasSeenBy(Faction faction) =>
        faction != null
        && (
            seenByPlayer
                && (
                    firstObserverFactionId == faction.loadID
                    || firstObserverFactionId == 0
                        && parent.MapHeld != null
                        && parent.MapHeld.GetVisibility().PrimaryPlayerFactionId == faction.loadID
                )
            || otherObserverFactionIds?.Contains(faction.loadID) == true
        );

    private int nextCheck;
    private bool setup;

    // Location knowledge permits remembered interaction. Do not snapshot appearance or
    // resample stationary objects merely to keep this record up to date.
    internal bool WasSeenAtCurrentLocation(Faction faction) =>
        CanRememberLocation
        && WasSeenBy(faction)
        && observedMapId == parent.MapHeld?.uniqueID
        && (
            faction.loadID == firstObserverFactionId
                ? firstObservedCell == parent.MapHeld.cellIndices.CellToIndex(parent.PositionHeld)
                : otherObservedCells != null
                    && otherObservedCells.TryGetValue(faction.loadID, out int cell)
                    && cell == parent.MapHeld.cellIndices.CellToIndex(parent.PositionHeld)
        );

    private bool CanRememberLocation =>
        parent is not Pawn
        && parent.def.category is not (ThingCategory.Projectile or ThingCategory.Mote)
        && !parent.def.IsBlueprint
        && !parent.def.IsFrame;

    private bool NeedsObservation(Faction faction) =>
        !WasSeenBy(faction) || CanRememberLocation && !WasSeenAtCurrentLocation(faction);

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setup = true;
        if (seenByPlayer && firstObserverFactionId == 0)
            firstObserverFactionId = parent.Map.GetVisibility().PrimaryPlayerFactionId;
        nextCheck = 0;
        if (arrivalObserverFactionId != 0)
        {
            var observer = Find.FactionManager.AllFactionsListForReading.Find(f =>
                f.loadID == arrivalObserverFactionId
            );
            arrivalObserverFactionId = 0;
            if (observer?.IsPlayer == true && CanRememberLocation && WasSeenBy(observer))
            {
                var fog = parent.Map.GetVisibility();
                foreach (var cell in parent.OccupiedRect())
                {
                    parent.Map.fogGrid.Unfog(cell);
                    fog.RevealCell(observer, parent.Map.cellIndices.CellToIndex(cell));
                }
                Remember(observer);
            }
        }
        UpdateVisibility(true);
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref seenByPlayer, "seenByPlayer");
        Scribe_Values.Look(ref firstObserverFactionId, "totalFogFirstObserverFaction");
        Scribe_Collections.Look(
            ref otherObserverFactionIds,
            "totalFogOtherObservers",
            LookMode.Value
        );
        Scribe_Values.Look(ref observedMapId, "totalFogObservedMap", -1);
        Scribe_Values.Look(ref firstObservedCell, "totalFogFirstObservedCell", -1);
        Scribe_Values.Look(ref arrivalObserverFactionId, "totalFogTransportObserverFaction");
        Scribe_Collections.Look(
            ref otherObservedCells,
            "totalFogOtherObservedCells",
            LookMode.Value,
            LookMode.Value
        );
    }

    public override void ReceiveCompSignal(string signal) => UpdateVisibility(true);

    public override void CompTick()
    {
        int tick = Find.TickManager.TicksGame;
        if (tick < nextCheck)
            return;
        UpdateVisibility(false);
    }

    public void ForceSeen()
    {
        Remember(Faction.OfPlayer);
        UpdateVisibility(true, true);
    }

    internal void RememberArrival(Faction faction)
    {
        // An explicit player transport may relocate existing knowledge. It
        // cannot establish an observation of cargo the player has never seen.
        if (!parent.Spawned || !CanRememberLocation || !WasSeenBy(faction))
            return;
        Remember(faction);
        UpdateVisibility(true);
    }

    internal void RememberNextArrival(Faction faction)
    {
        if (faction?.IsPlayer == true && CanRememberLocation && WasSeenBy(faction))
            arrivalObserverFactionId = faction.loadID;
    }

    public void UpdateVisibility(bool forceCheck, bool forceUpdate = false)
    {
        if (
            !setup
            || !parent.Spawned
            || parent.Map == null
            || Current.ProgramState == ProgramState.MapInitializing
        )
            return;
        // Coverage, movement and signals already reconcile presentation. The
        // periodic fallback is needed only after twelve ticks without a check.
        nextCheck = Compatibility.MultiplayerIntegration.TicksFor(parent.Map) + 12;
        var fog =
            mainComponent.ComponentsPositionTracker?.CurrentVisibility
            ?? parent.Map.GetVisibility();
        bool primaryView = Faction.OfPlayer?.loadID == fog.PrimaryPlayerFactionId;
        // The primary observer can see a newly spawned thing before a foreign
        // viewer has any sources. Record that knowledge in either client view.
        if (!primaryView || fog.OtherPlayerFactions.Count > 0)
        {
            RecordObservation(fog.PrimaryPlayerFaction, fog);
            foreach (var faction in fog.OtherPlayerFactions)
                RecordObservation(faction, fog);
        }
        bool visible = ThingVisibility.IsVisible(
            parent,
            registeredVisibility: fog,
            observed: WasSeenBy(Faction.OfPlayer)
        );
        if (forceUpdate && parent is not Pawn)
            visible = true;
        // Selection follows the user's remembered-object preference. Coverage
        // transitions call this immediately; movement also invalidates location.
        if (Find.Selector.IsSelected(parent) && !ThingVisibility.CanInspect(parent))
            Find.Selector.Deselect(parent);
        // Before initialization, permissive rendering protects engine setup but
        // does not mean the player has observed every spawned object.
        if (visible)
        {
            if (
                NeedsObservation(Faction.OfPlayer)
                && primaryView
                && fog.OtherPlayerFactions.Count == 0
                && fog.Initialized
                && ThingVisibility.IsVisible(
                    parent,
                    allowMemory: false,
                    registeredVisibility: fog,
                    observed: false
                )
            )
                Remember(Faction.OfPlayer);
            mainComponent.Hiddenable?.Show();
        }
        else
            mainComponent.Hiddenable?.Hide();
    }

    internal void RecordObservation(Faction faction, MapVisibility fog)
    {
        if (
            !setup
            || !parent.Spawned
            || faction == null
            || !faction.IsPlayer
            || !fog.Initialized
            || !NeedsObservation(faction)
        )
            return;
        if (
            ThingVisibility.IsVisible(
                parent,
                allowMemory: false,
                registeredVisibility: fog,
                observerFaction: faction
            )
        )
            Remember(faction);
    }

    private void Remember(Faction faction)
    {
        if (faction == null)
            return;
        bool previouslySeen = WasSeenBy(faction);
        if (!seenByPlayer)
        {
            seenByPlayer = true;
            firstObserverFactionId = faction.loadID;
        }
        else if (!previouslySeen)
            (otherObserverFactionIds ??= new()).Add(faction.loadID);
        if (CanRememberLocation && parent.MapHeld != null)
        {
            if (observedMapId != parent.MapHeld.uniqueID)
            {
                observedMapId = parent.MapHeld.uniqueID;
                firstObservedCell = -1;
                otherObservedCells?.Clear();
            }
            int cell = parent.MapHeld.cellIndices.CellToIndex(parent.PositionHeld);
            if (faction.loadID == firstObserverFactionId)
                firstObservedCell = cell;
            else
                (otherObservedCells ??= new())[faction.loadID] = cell;
        }
        if (!previouslySeen)
            Notifications.EntityDiscovery.Observed(parent);
    }
}
