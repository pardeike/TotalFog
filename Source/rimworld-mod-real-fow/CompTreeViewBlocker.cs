using System;
using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

public class CompTreeViewBlocker : ThingSubComp
{
    private const float GrowthThreshold = 0.5f;
    private const int UpdateIntervalTicks = 60;
    public static readonly CompProperties_TreeViewBlocker CompDef = new();

    private bool blocksSight;

    private float cachedGrowth = -1f;

    private int lastUpdateTick;

    private Map map;

    private MapComponentSeenFog mapCompSeenFog;
    private Plant plant;
    private bool registeredWithGrid;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        plant = parent as Plant;

        if (!FoWThingUtils.PlantBlocksView(plant))
        {
            return;
        }

        map = plant?.Map;
        mapCompSeenFog = map.GetMapComponentSeenFog();
        lastUpdateTick = Find.TickManager.TicksGame;
        cachedGrowth = -1f;
        blocksSight = false;
        registeredWithGrid = false;

        // Check growth and register if needed
        checkGrowthTransition();
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (FoWThingUtils.PlantBlocksView(plant))
        {
            checkGrowthTransition();
        }
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!FoWThingUtils.PlantBlocksView(plant))
        {
            return;
        }

        var tickGame = Find.TickManager.TicksGame;

        if (tickGame - lastUpdateTick < UpdateIntervalTicks)
        {
            return;
        }

        lastUpdateTick = tickGame;
        checkGrowthTransition();
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        if (!FoWThingUtils.PlantBlocksView(plant))
        {
            return;
        }

        if (this.map != map)
        {
            this.map = map;
            mapCompSeenFog = map.GetMapComponentSeenFog();
        }

        // Deregister from the grid and clear blocker cells if we were registered
        if (!registeredWithGrid || mapCompSeenFog == null)
        {
            return;
        }

        var position = plant.Position;
        mapCompSeenFog.DeregisterTreeViewBlocker(this, position.x, position.z);
        updateViewBlockerCells(false);
        registeredWithGrid = false;
    }

    private void checkGrowthTransition()
    {
        if (plant == null || mapCompSeenFog == null || map == null)
        {
            return;
        }

        // If trees blocking sight is disabled, ensure we're not registered
        if (!RfowSettings.TreesBlockSight)
        {
            if (!registeredWithGrid)
            {
                return;
            }

            var position = plant.Position;
            mapCompSeenFog.DeregisterTreeViewBlocker(this, position.x, position.z);
            registeredWithGrid = false;
            updateViewBlockerCells(false);

            return;
        }

        var currentGrowth = plant.Growth;

        // Only update if growth has changed by at least 1% to avoid floating point precision issues
        if (Math.Abs(currentGrowth - cachedGrowth) < 0.01f)
        {
            return;
        }

        cachedGrowth = currentGrowth;
        var shouldBlock = currentGrowth >= GrowthThreshold;

        if (shouldBlock == blocksSight)
        {
            return;
        }

        blocksSight = shouldBlock;

        switch (blocksSight)
        {
            case true when !registeredWithGrid:
            {
                // Register when tree reaches threshold
                var position = plant.Position;
                mapCompSeenFog.RegisterTreeViewBlocker(this, position.x, position.z);
                registeredWithGrid = true;
                updateViewBlockerCells(true);
                break;
            }
            case false when registeredWithGrid:
            {
                // Unregister when tree falls below threshold
                var position = plant.Position;
                mapCompSeenFog.DeregisterTreeViewBlocker(this, position.x, position.z);
                registeredWithGrid = false;
                updateViewBlockerCells(false);
                break;
            }
        }
    }

    private void updateViewBlockerCells(bool blockView)
    {
        if (mapCompSeenFog == null || map == null || plant == null)
        {
            return;
        }

        var viewBlockerCells = mapCompSeenFog.viewBlockerCells;
        if (viewBlockerCells == null)
        {
            return;
        }

        var mapSizeX = map.Size.x;
        var mapSizeZ = map.Size.z;
        var position = plant.Position;
        var idx = (position.z * mapSizeX) + position.x;

        // Ensure index is within bounds
        if (position is { x: >= 0, z: >= 0 } && position.x < mapSizeX && position.z < mapSizeZ)
        {
            viewBlockerCells[idx] = blockView;
            mapCompSeenFog.treeBlockerCells[idx] = blockView;
        }

        // Notify watchers that might be affected
        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        if (map == null)
        {
            return;
        }

        var fowWatchers = mapCompSeenFog.fowWatchers;
        // ReSharper disable once ForCanBeConvertedToForeach
        for (var k = 0; k < fowWatchers.Count; k++)
        {
            var compFieldOfViewWatcher = fowWatchers[k];
            var lastSightRange = compFieldOfViewWatcher.LastSightRange;
            if (lastSightRange <= 0)
            {
                continue;
            }

            var watcherPos = compFieldOfViewWatcher.parent.Position;
            var dx = position.x - watcherPos.x;
            var dz = position.z - watcherPos.z;
            if ((dx * dx) + (dz * dz) < lastSightRange * lastSightRange)
            {
                compFieldOfViewWatcher.RefreshFovTarget(ref position);
            }
        }
    }
}