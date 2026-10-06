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

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setup = true;
        if (seenByPlayer && firstObserverFactionId == 0)
            firstObserverFactionId = parent.Map.GetVisibility().PrimaryPlayerFactionId;
        nextCheck = 0;
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
        nextCheck = Find.TickManager.TicksGame + 12;
        var fog =
            mainComponent.ComponentsPositionTracker?.CurrentVisibility
            ?? parent.Map.GetVisibility();
        if (fog.OtherPlayerFactions.Count > 0)
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
        // Remembered geometry must not keep a live inspector open. Coverage
        // transitions call this immediately; periodic checks cover other changes.
        if (
            Find.Selector.IsSelected(parent)
            && !ThingVisibility.IsVisible(parent, allowMemory: false)
        )
            Find.Selector.Deselect(parent);
        // Before initialization, permissive rendering protects engine setup but
        // does not mean the player has observed every spawned object.
        if (visible)
        {
            if (
                !WasSeenBy(Faction.OfPlayer)
                && fog.OtherPlayerFactions.Count == 0
                && fog.Initialized
                && (
                    !Compatibility.GravshipVisibility.Revealed
                    || ThingVisibility.IsVisible(
                        parent,
                        allowMemory: false,
                        registeredVisibility: fog,
                        observed: false
                    )
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
            || WasSeenBy(faction)
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
        if (faction == null || WasSeenBy(faction))
            return;
        if (!seenByPlayer)
        {
            seenByPlayer = true;
            firstObserverFactionId = faction.loadID;
        }
        else
            (otherObserverFactionIds ??= new()).Add(faction.loadID);
    }
}
