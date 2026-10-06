using Verse;

namespace RimWorldRealFoW;

public class CompViewBlockerWatcher : ThingSubComp
{
    private Building b;

    private bool blockLight;

    private bool lastIsViewBlocker;
    private int lastUpdateTick;

    private Map map;

    private MapComponentSeenFog mapCompSeenFog;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        blockLight = parent.def.blockLight;
        b = parent as Building;
        lastUpdateTick = Find.TickManager.TicksGame;
        if (blockLight && b != null)
        {
            updateIsViewBlocker();
        }
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (blockLight && b != null)
        {
            updateIsViewBlocker();
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        var tickGame = Find.TickManager.TicksGame;

        if (!blockLight || b == null || tickGame - lastUpdateTick != 30)
        {
            return;
        }

        lastUpdateTick = tickGame;
        updateIsViewBlocker();
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        if (!lastIsViewBlocker)
        {
            return;
        }

        if (this.map != map)
        {
            this.map = map;
            mapCompSeenFog = map.GetMapComponentSeenFog();
        }

        updateViewBlockerCells(false);
    }

    private void updateIsViewBlocker()
    {
        if (lastIsViewBlocker == (blockLight && !b.CanBeSeenOver()))
        {
            return;
        }

        lastIsViewBlocker = blockLight && !b.CanBeSeenOver();
        if (map != parent.Map)
        {
            map = parent.Map;
            mapCompSeenFog = map.GetMapComponentSeenFog();
        }

        updateViewBlockerCells(blockLight && !b.CanBeSeenOver());
    }

    private void updateViewBlockerCells(bool blockView)
    {
        var viewBlockerCells = mapCompSeenFog.viewBlockerCells;
        var z = map.Size.z;
        var x = map.Size.x;
        var cellRect = parent.OccupiedRect();
        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                if (i >= 0 && j >= 0 && i <= x && j <= z)
                {
                    viewBlockerCells[(j * z) + i] = blockView;
                }
            }
        }

        var position = parent.Position;
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

            var position2 = compFieldOfViewWatcher.parent.Position;
            var num = position.x - position2.x;
            var num2 = position.z - position2.z;
            if ((num * num) + (num2 * num2) < lastSightRange * lastSightRange)
            {
                compFieldOfViewWatcher.RefreshFovTarget(ref position);
            }
        }
    }
}