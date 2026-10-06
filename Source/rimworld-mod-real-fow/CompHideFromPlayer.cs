using RimWorld;
using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW;

public class CompHideFromPlayer : ThingSubComp
{
    private static readonly IntVec3 iv3Invalid = IntVec3.Invalid;

    private static readonly Rot4 r4Invalid = Rot4.Invalid;

    private bool calculated;

    private CompHiddenable compHiddenable;

    private FogGrid fogGrid;

    private bool isOneCell;

    private bool isPawn;

    private bool isSaveable;

    private IntVec3 lastPosition;

    private Rot4 lastRotation;
    private int lastUpdateTick;

    private Map map;

    private MapComponentSeenFog mapCompSeenFog;

    private bool saveCompressible;

    private bool seenByPlayer;

    private bool setupDone;

    private IntVec2 size;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        setupDone = true;
        calculated = false;
        lastPosition = iv3Invalid;
        lastRotation = r4Invalid;
        isPawn = parent.def.category == ThingCategory.Pawn;
        size = parent.def.size;
        isOneCell = size is { z: 1, x: 1 };
        isSaveable = parent.def.isSaveable;
        saveCompressible = parent.def.saveCompressible;
        compHiddenable = mainComponent.Hiddenable;
        lastUpdateTick = Find.TickManager.TicksGame;
        UpdateVisibility(false);
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref seenByPlayer, "seenByPlayer");
    }

    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        UpdateVisibility(false);
    }

    public override void CompTick()
    {
        base.CompTick();
        var tickGame = Find.TickManager.TicksGame;
        if (tickGame - lastUpdateTick != 12)
        {
            return;
        }

        lastUpdateTick = tickGame;
        UpdateVisibility(false);
    }

    public void ForceSeen()
    {
        seenByPlayer = true;
        UpdateVisibility(true, true);
    }

    public void UpdateVisibility(bool forceCheck, bool forceUpdate = false)
    {
        if (!setupDone || Current.ProgramState == ProgramState.MapInitializing)
        {
            return;
        }

        Thing thingParent = parent;
        var position = thingParent.Position;
        var rotation = thingParent.Rotation;
        if (thingParent is not { Spawned: true, Map: not null } || position == iv3Invalid ||
            !isOneCell && rotation == r4Invalid)
        {
            return;
        }

        if (map != thingParent.Map)
        {
            map = thingParent.Map;
            fogGrid = map.fogGrid;
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

        if (!forceCheck && calculated && position == lastPosition &&
            (isOneCell || rotation == lastRotation))
        {
            return;
        }

        calculated = true;
        lastPosition = position;
        lastRotation = rotation;
        if (mapCompSeenFog == null || fogGrid.IsFogged(lastPosition))
        {
            return;
        }

        var wasHidden = compHiddenable?.Hidden ?? false;

        if (isSaveable && !saveCompressible)
        {
            if (thingParent.Faction is not { IsPlayer: true })
            {
                if (isPawn && !hasPartShownToPlayer() || !isPawn && !seenByPlayer && !hasPartShownToPlayer())
                {
                    compHiddenable?.Hide();
                }
                else
                {
                    seenByPlayer = true;
                    compHiddenable?.Show();
                }
            }
            else
            {
                seenByPlayer = true;
                compHiddenable?.Show();
            }
        }
        else
        {
            if (!forceUpdate && seenByPlayer || !hasPartShownToPlayer())
            {
                return;
            }

            seenByPlayer = true;
            compHiddenable?.Show();
        }

        // Trigger pending alert if this thing was just revealed to the player
        if (!wasHidden || compHiddenable is { Hidden: true } || !RfowSettings.DelayAlertsUntilSeen)
        {
            return;
        }

        var pendingAlertManager = map.GetPendingAlertManager();
        pendingAlertManager?.TriggerPendingAlertForThing(thingParent);
    }

    private bool hasPartShownToPlayer()
    {
        var ofPlayer = Faction.OfPlayer;
        bool result;
        if (isOneCell)
        {
            result = mapCompSeenFog.IsShown(ofPlayer, lastPosition.x, lastPosition.z);
        }
        else
        {
            var cellRect = GenAdj.OccupiedRect(lastPosition, lastRotation, size);
            for (var i = cellRect.minX; i <= cellRect.maxX; i++)
            {
                for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
                {
                    if (mapCompSeenFog.IsShown(ofPlayer, i, j))
                    {
                        return true;
                    }
                }
            }

            result = false;
        }

        return result;
    }
}