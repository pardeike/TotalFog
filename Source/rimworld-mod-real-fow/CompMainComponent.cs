using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

public class CompMainComponent : ThingComp
{
    public static readonly CompProperties CompDef = new(typeof(CompMainComponent));
    private CompTreeViewBlocker compTreeViewBlocker;
    private CompViewBlockerWatcher compViewBlockerWatcher;
    private bool setup;
    public CompComponentsPositionTracker ComponentsPositionTracker { get; private set; }
    public CompFieldOfViewWatcher FieldOfViewWatcher { get; set; }
    public CompHiddenable Hiddenable { get; private set; }
    public CompHideFromPlayer HideFromPlayer { get; private set; }
    private bool IsPlant { get; set; }
    private bool IsTreePlant { get; set; }

    private void performSetup()
    {
        if (setup)
        {
            return;
        }

        setup = true;

        var category = parent.def.category;
        IsPlant = category == ThingCategory.Plant;
        IsTreePlant = IsPlant && parent is Plant plant && FoWThingUtils.PlantBlocksView(plant);

        ComponentsPositionTracker = new CompComponentsPositionTracker
        {
            parent = parent,
            mainComponent = this
        };

        Hiddenable = new CompHiddenable
        {
            parent = parent,
            mainComponent = this
        };

        HideFromPlayer = new CompHideFromPlayer
        {
            parent = parent,
            mainComponent = this
        };

        if (category == ThingCategory.Building)
        {
            compViewBlockerWatcher = new CompViewBlockerWatcher
            {
                parent = parent,
                mainComponent = this
            };
        }

        if (
            category is ThingCategory.Pawn or ThingCategory.Building
            //||category == ThingCategory.Projectile
        )
        {
            FieldOfViewWatcher = new CompFieldOfViewWatcher
            {
                parent = parent,
                mainComponent = this
            };
        }

        // Always create tree view blocker for trees - the setting is checked at runtime
        if (IsTreePlant)
        {
            compTreeViewBlocker = new CompTreeViewBlocker
            {
                parent = parent,
                mainComponent = this
            };
        }
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        performSetup();

        ComponentsPositionTracker.PostSpawnSetup(respawningAfterLoad);

        Hiddenable.PostSpawnSetup(respawningAfterLoad);

        HideFromPlayer.PostSpawnSetup(respawningAfterLoad);

        compViewBlockerWatcher?.PostSpawnSetup(respawningAfterLoad);
        FieldOfViewWatcher?.PostSpawnSetup(respawningAfterLoad);
        compTreeViewBlocker?.PostSpawnSetup(respawningAfterLoad);
    }

    public override void CompTick()
    {
        performSetup();

        ComponentsPositionTracker.CompTick();
        Hiddenable.CompTick();
        HideFromPlayer.CompTick();
        compViewBlockerWatcher?.CompTick();
        FieldOfViewWatcher?.CompTick();
        compTreeViewBlocker?.CompTick();
    }

    public override void CompTickRare()
    {
        performSetup();

        ComponentsPositionTracker.CompTickRare();
        Hiddenable.CompTickRare();
        HideFromPlayer.CompTickRare();
        compViewBlockerWatcher?.CompTickRare();
        FieldOfViewWatcher?.CompTickRare();
        compTreeViewBlocker?.CompTickRare();
    }

    public override void ReceiveCompSignal(string signal)
    {
        performSetup();

        ComponentsPositionTracker.ReceiveCompSignal(signal);
        Hiddenable.ReceiveCompSignal(signal);
        HideFromPlayer.ReceiveCompSignal(signal);
        compViewBlockerWatcher?.ReceiveCompSignal(signal);
        FieldOfViewWatcher?.ReceiveCompSignal(signal);
        compTreeViewBlocker?.ReceiveCompSignal(signal);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        performSetup();

        ComponentsPositionTracker.PostDeSpawn(map);
        Hiddenable.PostDeSpawn(map);
        HideFromPlayer.PostDeSpawn(map);
        compViewBlockerWatcher?.PostDeSpawn(map);
        FieldOfViewWatcher?.PostDeSpawn(map);
        compTreeViewBlocker?.PostDeSpawn(map);
    }

    public override void PostExposeData()
    {
        performSetup();

        ComponentsPositionTracker.PostExposeData();
        Hiddenable.PostExposeData();
        HideFromPlayer.PostExposeData();

        compViewBlockerWatcher?.PostExposeData();
        FieldOfViewWatcher?.PostExposeData();
        compTreeViewBlocker?.PostExposeData();
        if (!Scribe.saver.savingForDebug)
        {
            return;
        }

        var hasCompComponentsPositionTracker = ComponentsPositionTracker != null;
        var hasCompHiddenable = Hiddenable != null;
        var hasCompHideFromPlayer = HideFromPlayer != null;
        var hasCompViewBlockerWatcher = compViewBlockerWatcher != null;
        var hasCompFieldOfViewWatcher = FieldOfViewWatcher != null;
        var hasCompTreeViewBlocker = compTreeViewBlocker != null;
        Scribe_Values.Look(ref hasCompComponentsPositionTracker, "hasCompComponentsPositionTracker");
        Scribe_Values.Look(ref hasCompHiddenable, "hasCompHiddenable");
        Scribe_Values.Look(ref hasCompHideFromPlayer, "hasCompHideFromPlayer");
        Scribe_Values.Look(ref hasCompViewBlockerWatcher, "hasCompViewBlockerWatcher");
        Scribe_Values.Look(ref hasCompFieldOfViewWatcher, "hasCompFieldOfViewWatcher");
        Scribe_Values.Look(ref hasCompTreeViewBlocker, "hasCompTreeViewBlocker");
    }
}