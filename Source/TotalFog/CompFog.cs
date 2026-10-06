// Lifecycle dispatch rewritten for Total Fog, 2026-10-04.
using RimWorld;
using TotalFog.Utils;
using Verse;

namespace TotalFog;

public class CompFog : ThingComp
{
    public static readonly CompProperties CompDef = new(typeof(CompFog));
    private FogSubcomponent[] parts;
    public CompCellRegistration ComponentsPositionTracker { get; private set; }
    public CompSightSource FieldOfViewWatcher { get; set; }
    public CompPresentationState Hiddenable { get; private set; }
    public CompVisibility HideFromPlayer { get; private set; }

    private void Setup()
    {
        if (parts != null)
            return;
        ComponentsPositionTracker = new();
        Hiddenable = new();
        HideFromPlayer = new();
        bool building = parent.def.category == ThingCategory.Building;
        if (building || parent is Pawn)
            FieldOfViewWatcher = new();
        parts =
        [
            ComponentsPositionTracker,
            Hiddenable,
            HideFromPlayer,
            building ? new CompWallOcclusion() : null,
            FieldOfViewWatcher,
            parent is Plant plant && FogThingUtility.PlantBlocksView(plant)
                ? new CompTreeOcclusion()
                : null,
        ];
        foreach (var part in parts)
            if (part != null)
            {
                part.parent = parent;
                part.mainComponent = this;
            }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        Setup();
        foreach (var part in parts)
            part?.PostSpawnSetup(respawningAfterLoad);
    }

    public override void CompTick()
    {
        Setup();
        ComponentsPositionTracker.CompTick();
        HideFromPlayer.CompTick();
        parts[3]?.CompTick();
        parts[4]?.CompTick();
        parts[5]?.CompTick();
    }

    public override void CompTickRare()
    {
        Setup();
        ComponentsPositionTracker.CompTickRare();
        HideFromPlayer.CompTickRare();
        parts[3]?.CompTickRare();
        parts[4]?.CompTickRare();
        parts[5]?.CompTickRare();
    }

    public override void CompTickLong() => CompTick();

    public override void ReceiveCompSignal(string signal)
    {
        Setup();
        foreach (var part in parts)
            part?.ReceiveCompSignal(signal);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        Setup();
        foreach (var part in parts)
            part?.PostDeSpawn(map);
    }

    public override void PostExposeData()
    {
        Setup();
        foreach (var part in parts)
            part?.PostExposeData();
    }
}
