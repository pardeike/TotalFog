using Verse;

namespace RimWorldRealFoW;

public class CompComponentsPositionTracker : ThingSubComp
{
    private static readonly IntVec3 iv3Invalid = IntVec3.Invalid;

    private static readonly Rot4 r4Invalid = Rot4.Invalid;

    private bool calculated;

    private CompAffectVision compAffectVision;

    private CompHideFromPlayer compHideFromPlayer;

    private bool isOneCell;

    private IntVec3 lastPosition;
    private int lastPositionUpdateTick;

    private Rot4 lastRotation;

    private Map map;

    private MapComponentSeenFog mapCompSeenFog;

    private bool setupDone;

    private IntVec2 size;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setupDone = true;
        var def = parent.def;
        size = def.size;
        isOneCell = size is { z: 1, x: 1 };
        compHideFromPlayer = mainComponent.HideFromPlayer;
        compAffectVision = parent.TryGetComp<CompAffectVision>();
        lastPosition = iv3Invalid;
        lastRotation = r4Invalid;
        lastPositionUpdateTick = Find.TickManager.TicksGame;
        updatePosition();
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        updatePosition();
    }

    public override void CompTick()
    {
        base.CompTick();
        var ticksGame = Find.TickManager.TicksGame;
        if (ticksGame - lastPositionUpdateTick != 12)
        {
            return;
        }

        lastPositionUpdateTick = ticksGame;
        updatePosition();
    }

    public void updatePosition()
    {
        if (!setupDone)
        {
            return;
        }

        Thing thingParent = parent;
        var position = thingParent.Position;
        var rotation = thingParent.Rotation;
        if (thingParent is not { Spawned: true, Map: not null } || position == iv3Invalid ||
            !isOneCell && rotation == r4Invalid ||
            compHideFromPlayer == null && compAffectVision == null)
        {
            return;
        }

        if (map != thingParent.Map)
        {
            map = thingParent.Map;
            mapCompSeenFog = thingParent.Map.GetMapComponentSeenFog();
        }
        else
        {
            mapCompSeenFog ??= thingParent.Map.GetMapComponentSeenFog();
        }

        if (mapCompSeenFog == null)
        {
            return;
        }

        if (calculated && position == lastPosition && (isOneCell || rotation == lastRotation))
        {
            return;
        }

        calculated = true;
        if (isOneCell)
        {
            if (compHideFromPlayer != null)
            {
                mapCompSeenFog.DeregisterCompHideFromPlayerPosition(compHideFromPlayer, lastPosition.x,
                    lastPosition.z);
                mapCompSeenFog.RegisterCompHideFromPlayerPosition(compHideFromPlayer, position.x,
                    position.z);
            }

            if (compAffectVision != null)
            {
                mapCompSeenFog.DeregisterCompAffectVisionPosition(compAffectVision, lastPosition.x,
                    lastPosition.z);
                mapCompSeenFog.RegisterCompAffectVisionPosition(compAffectVision, position.x,
                    position.z);
            }
        }
        else
        {
            if (lastPosition != iv3Invalid && lastRotation != r4Invalid)
            {
                var cellRect = GenAdj.OccupiedRect(lastPosition, lastRotation, size);
                for (var i = cellRect.minZ; i <= cellRect.maxZ; i++)
                {
                    for (var j = cellRect.minX; j <= cellRect.maxX; j++)
                    {
                        if (compHideFromPlayer != null)
                        {
                            mapCompSeenFog.DeregisterCompHideFromPlayerPosition(compHideFromPlayer, j,
                                i);
                        }

                        if (compAffectVision != null)
                        {
                            mapCompSeenFog.DeregisterCompAffectVisionPosition(compAffectVision, j, i);
                        }
                    }
                }
            }

            if (position != iv3Invalid && rotation != r4Invalid)
            {
                var cellRect2 = GenAdj.OccupiedRect(position, rotation, size);
                for (var i = cellRect2.minZ; i <= cellRect2.maxZ; i++)
                {
                    for (var j = cellRect2.minX; j <= cellRect2.maxX; j++)
                    {
                        if (compHideFromPlayer != null)
                        {
                            mapCompSeenFog.RegisterCompHideFromPlayerPosition(compHideFromPlayer, j, i);
                        }

                        if (compAffectVision != null)
                        {
                            mapCompSeenFog.RegisterCompAffectVisionPosition(compAffectVision, j, i);
                        }
                    }
                }
            }
        }

        lastPosition = position;
        if (size.x != 1 || size.z != 1)
        {
            lastRotation = rotation;
        }
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        if (mapCompSeenFog == null || compHideFromPlayer == null && compAffectVision == null)
        {
            return;
        }

        if (isOneCell)
        {
            mapCompSeenFog.DeregisterCompHideFromPlayerPosition(compHideFromPlayer, lastPosition.x, lastPosition.z);
            mapCompSeenFog.DeregisterCompAffectVisionPosition(compAffectVision, lastPosition.x, lastPosition.z);
        }
        else
        {
            if (lastPosition == iv3Invalid || lastRotation == r4Invalid)
            {
                return;
            }

            var cellRect = GenAdj.OccupiedRect(lastPosition, lastRotation, size);
            for (var i = cellRect.minZ; i <= cellRect.maxZ; i++)
            {
                for (var j = cellRect.minX; j <= cellRect.maxX; j++)
                {
                    if (compHideFromPlayer != null)
                    {
                        mapCompSeenFog.DeregisterCompHideFromPlayerPosition(compHideFromPlayer, j, i);
                    }

                    if (compAffectVision != null)
                    {
                        mapCompSeenFog.DeregisterCompAffectVisionPosition(compAffectVision, j, i);
                    }
                }
            }
        }
    }
}