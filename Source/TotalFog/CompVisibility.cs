// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using TotalFog.Presentation;
using Verse;
namespace TotalFog;

/// <summary>Presentation side effects occur only when the common visibility policy changes.</summary>
public class CompVisibility : FogSubcomponent
{
    private bool seenByPlayer;
    public bool SeenByPlayer => seenByPlayer;
    private int nextCheck;
    private bool setup;
    public override void PostSpawnSetup(bool respawningAfterLoad) { setup = true; nextCheck = 0; UpdateVisibility(true); }
    public override void PostExposeData() => Scribe_Values.Look(ref seenByPlayer, "seenByPlayer");
    public override void ReceiveCompSignal(string signal) => UpdateVisibility(true);
    public override void CompTick()
    {
        int tick = Find.TickManager.TicksGame;
        if (tick < nextCheck) return;
        UpdateVisibility(false);
    }
    public void ForceSeen() { seenByPlayer = true; UpdateVisibility(true, true); }
    public void UpdateVisibility(bool forceCheck, bool forceUpdate = false)
    {
        if (!setup || !parent.Spawned || parent.Map == null || Current.ProgramState == ProgramState.MapInitializing) return;
        // Coverage, movement and signals already reconcile presentation. The
        // periodic fallback is needed only after twelve ticks without a check.
        nextCheck = Find.TickManager.TicksGame + 12;
        var fog = mainComponent.ComponentsPositionTracker?.CurrentVisibility;
        bool visible = ThingVisibility.IsVisible(parent, registeredVisibility: fog, observed: seenByPlayer);
        if (forceUpdate && parent is not Pawn) visible = true;
        // Remembered geometry must not keep a live inspector open. Coverage
        // transitions call this immediately; periodic checks cover other changes.
        if (Find.Selector.IsSelected(parent) && !ThingVisibility.IsVisible(parent, allowMemory: false))
            Find.Selector.Deselect(parent);
        // Before initialization, permissive rendering protects engine setup but
        // does not mean the player has observed every spawned object.
        if (visible)
        {
            if (!seenByPlayer && (fog ?? parent.Map.GetVisibility()).Initialized) seenByPlayer = true;
            mainComponent.Hiddenable?.Show();
        }
        else mainComponent.Hiddenable?.Hide();
    }
}
