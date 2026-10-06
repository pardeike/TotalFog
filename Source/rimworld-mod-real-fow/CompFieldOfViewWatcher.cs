using System;
using System.Collections.Generic;
using RimWorld;
using RimWorldRealFoW.Utils;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimWorldRealFoW;

public class CompFieldOfViewWatcher : ThingSubComp
{
    private static readonly IntVec3 iv3Invalid = IntVec3.Invalid;

    // Clear the trees the pawn is touching, so it can see past its own cover. Not walls, that would open a window through them
    // Scratch buffers for the peek pass, shared since the FoV is only computed on the main thread
    private static bool[] scratchTreeMask;

    private static bool[] scratchPeekMap;
    //private ThingDef def;

    // Cells cleared around the pawn while its FoV is cast, see clearOwnCover
    private readonly int[] clearedCoverCells = new int[8];

    private float baseViewRange;

    private Building building;

    private bool calculated;


    private PawnCapacitiesHandler capacities;

    private int clearedCoverCount;

    private CompFlickable compFlickable;

    private CompMannable compMannable;


    private CompPowerTrader compPowerTrader;

    private CompProvideVision compProvideVision;

    private CompRefuelable compRefuelable;

    private float dayVisionEffectiveness;

    private bool disabled;


    private GlowGrid glowGrid;

    private Faction lastFaction;

    private short[] lastFactionShownCells;
    private int lastHearTick;
    private int lastHearUpdateTick;

    private int lastMovementTick;

    private IntVec3[] lastPeekDirections;

    private IntVec3 lastPosition;

    private int lastPositionUpdateTick;

    private int lastStatcheckTick;

    private Map map;

    private MapComponentSeenFog mapCompSeenFog;

    private int mapSizeX;

    private int mapSizeZ;

    private List<Pawn> nearByPawn = [];

    private float nightVisionEffectiveness;

    private Pawn pawn;

    private Pawn_PathFollower pawnPather;

    private RaceProperties raceProps;

    private RoofGrid roofGrid;
    private bool setupDone;

    private ThingType thingType;

    private Building_TurretGun turret;

    private bool[] viewMap1;

    private bool[] viewMap2;

    private bool viewMapSwitch;

    private IntVec3[] viewPositions;

    private CellRect viewRect;

    private WeatherManager weatherManager;

    public int LastSightRange { get; private set; }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setupDone = true;
        calculated = false;
        lastPosition = iv3Invalid;
        LastSightRange = 0;
        lastPeekDirections = null;
        viewMap1 = null;
        viewMap2 = null;
        viewRect = new CellRect(-1, -1, 0, 0);
        viewPositions = new IntVec3[5];

        compPowerTrader = parent.GetComp<CompPowerTrader>();
        compRefuelable = parent.GetComp<CompRefuelable>();
        compFlickable = parent.GetComp<CompFlickable>();
        compMannable = parent.GetComp<CompMannable>();
        compProvideVision = parent.GetComp<CompProvideVision>();

        pawn = parent as Pawn;
        building = parent as Building;
        turret = parent as Building_TurretGun;
        disabled = false;

        if (pawn != null)
        {
            raceProps = pawn.RaceProps;
            capacities = pawn.health.capacities;
            pawnPather = pawn.pather;

            thingType = raceProps.Animal ? ThingType.Animal : ThingType.Pawn;

            dayVisionEffectiveness = pawn.GetStatValue(FoWDef.DayVisionEffectiveness, false);
            nightVisionEffectiveness = pawn.GetStatValue(FoWDef.NightVisionEffectiveness);
            baseViewRange = RfowSettings.BaseViewRange * dayVisionEffectiveness;
        }
        else if (turret != null && compMannable == null)
        {
            thingType = ThingType.Turret;
        }
        else if (compProvideVision != null)
        {
            thingType = ThingType.VisionProvider;
            building?.def.specialDisplayRadius =
                (compProvideVision.Props.viewRadius * RfowSettings.BuildingVisionModifier) - 0.1f;
        }
        else if (building != null)
        {
            thingType = ThingType.Building;
        }
        else
        {
            RealFoWModStarter.LogMessage($"Removing unneeded FoV watcher from {parent.ThingID}");
            disabled = true;
            mainComponent.FieldOfViewWatcher = null;
        }

        initMap();
        lastMovementTick = Find.TickManager.TicksGame;

        lastPositionUpdateTick = lastMovementTick;
        lastStatcheckTick = lastMovementTick;
        lastHearTick = lastMovementTick;
        lastHearUpdateTick = lastMovementTick;
        UpdateFoV();
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref lastMovementTick, "fovLastMovementTick", Find.TickManager.TicksGame);
    }

    public override void ReceiveCompSignal(string signal)
    {
        UpdateFoV();
    }

    public override void CompTick()
    {
        if (disabled)
        {
            return;
        }

        var ticksGame = Find.TickManager.TicksGame;
        if (pawn != null)
        {
            pawnPather ??= pawn.pather;

            if (pawnPather is { Moving: true })
            {
                lastMovementTick = ticksGame;
            }

            if (RfowSettings.BaseHearingRange > 0)
            {
                var hearing = capacities.GetLevel(PawnCapacityDefOf.Hearing);
                if (hearing > 0
                    && pawn.Faction == Faction.OfPlayer
                    && thingType == ThingType.Pawn)
                {
                    if (ticksGame - lastHearTick == 100)
                    {
                        lastHearTick = ticksGame;
                        livePawnHear(pawn.Faction);
                    }

                    if (ticksGame - lastHearUpdateTick == 200)
                    {
                        lastHearUpdateTick = ticksGame;
                        updateNearbyPawn(
                            pawn,
                            RfowSettings.BaseHearingRange,
                            capacities.GetLevel(PawnCapacityDefOf.Hearing));
                    }
                }
            }

            if (lastPosition != iv3Invalid && lastPosition != parent.Position)
            {
                lastPositionUpdateTick = ticksGame;

                UpdateFoV();
            }
            else
            {
                if ((ticksGame - lastPositionUpdateTick) % 30 == 0)
                {
                    UpdateFoV();
                }
            }
        }
        else
        {
            if (lastPosition != iv3Invalid
                && lastPosition != parent.Position || ticksGame % 30 == 0)
            {
                UpdateFoV();
            }
        }
    }

    private void initMap()
    {
        if (map == parent.Map)
        {
            return;
        }

        if (map != null && lastFaction != null)
        {
            unseeSeenCells(lastFaction, lastFactionShownCells);
        }

        if (!disabled && mapCompSeenFog != null)
        {
            mapCompSeenFog.fowWatchers.Remove(this);
        }

        map = parent.Map;
        mapCompSeenFog = map.GetMapComponentSeenFog();
        glowGrid = map.glowGrid;
        roofGrid = map.roofGrid;
        weatherManager = map.weatherManager;
        lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(parent.Faction);
        if (!disabled)
        {
            mapCompSeenFog.fowWatchers.Add(this);
        }

        mapSizeX = map.Size.x;
        mapSizeZ = map.Size.z;
    }

    public void UpdateFoV(bool forceUpdate = false)
    {
        if (disabled && setupDone && Current.ProgramState != ProgramState.MapInitializing)
        {
            return;
        }

        var thingParent = parent;
        var position = thingParent.Position;
        if (thingParent is not { Spawned: true } || thingParent.Map == null || position == iv3Invalid)
        {
            return;
        }

        initMap();
        var faction = thingParent.Faction;
        if (faction != null && pawn is not { Dead: true })
        {
            switch (thingType)
            {
                case ThingType.Pawn when RfowSettings.PrisonerGiveVision && pawn.IsPrisonerOfColony:
                    livePawnCalculateFov(position, 0.2f, forceUpdate, Faction.OfPlayer);
                    break;
                case ThingType.Pawn when RfowSettings.AllyGiveVision && pawn.Faction != Faction.OfPlayer &&
                                         pawn.Faction.AllyOrNeutralTo(Faction.OfPlayer):
                    livePawnCalculateFov(position, 0.5f, forceUpdate, Faction.OfPlayer);
                    break;
                case ThingType.Pawn:
                case ThingType.Animal when RealFoWModStarter.CanBeServant &&
                                           pawn.health?.hediffSet?.hediffs.Any(hediff =>
                                               hediff.def.defName.StartsWith("DE_Servant")) == true:
                    livePawnCalculateFov(position, 1, forceUpdate, faction);
                    break;
                case ThingType.Animal when (pawn.playerSettings?.Master == null
                                            || pawn.training == null
                                            || !pawn.training.HasLearned(TrainableDefOf.Release)):
                    livePawnCalculateFov(position,
                        0,
                        forceUpdate,
                        faction);
                    break;
                case ThingType.Animal:
                    livePawnCalculateFov(
                        position,
                        RfowSettings.AnimalVisionModifier * Math.Max(raceProps.baseBodySize * 0.7f, 0.4f),
                        forceUpdate,
                        faction);
                    break;
                case ThingType.Turret:
                {
                    //Turret is more sensor based so reduced vision range, still can feed back some info

                    var sightRange = Mathf.RoundToInt(turret.GunCompEq.PrimaryVerb.verbProps.range *
                                                      RfowSettings.TurretVisionModifier);

                    if (compPowerTrader is { PowerOn: false }
                        || compRefuelable is { HasFuel: false }
                        || compFlickable is { SwitchIsOn: false }
                        //|| !this.mapCompSeenFog.workingCameraConsole
                       )
                    {
                        sightRange = 0;
                    }

                    if (calculated
                        && !forceUpdate
                        && faction == lastFaction
                        && position == lastPosition
                        && sightRange == LastSightRange)
                    {
                        return;
                    }

                    calculated = true;
                    lastPosition = position;
                    LastSightRange = sightRange;
                    if (lastFaction != faction)
                    {
                        if (lastFaction != null)
                        {
                            unseeSeenCells(lastFaction, lastFactionShownCells);
                        }

                        lastFaction = faction;
                        lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(faction);
                    }

                    if (sightRange != 0)
                    {
                        CalculateFoV(thingParent, sightRange, null);
                    }
                    else
                    {
                        unseeSeenCells(lastFaction, lastFactionShownCells);
                        revealOccupiedCells();
                    }

                    break;
                }
                case ThingType.VisionProvider:
                {
                    var viewRadius = Mathf.RoundToInt(compProvideVision.Props.viewRadius *
                                                      RfowSettings.BuildingVisionModifier);
                    if (compPowerTrader is { PowerOn: false }
                        || compRefuelable is { HasFuel: false }
                        || compFlickable is { SwitchIsOn: false }
                        || compProvideVision.Props.needManned && !mapCompSeenFog.workingCameraConsole
                       )
                    {
                        viewRadius = 0;
                    }

                    if (calculated
                        && !forceUpdate
                        && faction == lastFaction
                        && position == lastPosition
                        && viewRadius == LastSightRange)
                    {
                        return;
                    }

                    calculated = true;
                    lastPosition = position;
                    LastSightRange = viewRadius;
                    if (lastFaction != faction)
                    {
                        if (lastFaction != null)
                        {
                            unseeSeenCells(lastFaction, lastFactionShownCells);
                        }

                        lastFaction = faction;
                        lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(faction);
                    }

                    if (viewRadius != 0)
                    {
                        CalculateFoV(thingParent, viewRadius, null);
                    }
                    else
                    {
                        unseeSeenCells(lastFaction, lastFactionShownCells);
                        revealOccupiedCells();
                    }

                    break;
                }
                case ThingType.Building:
                {
                    var sightRange = 0;
                    if (calculated
                        && !forceUpdate
                        && faction == lastFaction
                        && position == lastPosition
                        && sightRange == LastSightRange)
                    {
                        return;
                    }

                    calculated = true;
                    lastPosition = position;
                    LastSightRange = sightRange;
                    if (lastFaction != faction)
                    {
                        if (lastFaction != null)
                        {
                            unseeSeenCells(lastFaction, lastFactionShownCells);
                        }

                        lastFaction = faction;
                        lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(faction);
                    }

                    unseeSeenCells(lastFaction, lastFactionShownCells);
                    revealOccupiedCells();
                    break;
                }
                default:
                    Log.Warning($"Non disabled thing... {parent.ThingID}");
                    disabled = true;
                    break;
            }
        }
        else
        {
            if (faction == lastFaction)
            {
                return;
            }

            if (lastFaction != null)
            {
                unseeSeenCells(lastFaction, lastFactionShownCells);
            }

            lastFaction = faction;
            lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(faction);
        }
    }

    public float CalcPawnSightRange(IntVec3 position, bool forTargeting, bool shouldMove)
    {
        if (pawn == null)
        {
            RealFoWModStarter.LogMessage("calcPawnSightRange performed on non pawn thing");
            return 0f;
        }

        var viewRange = baseViewRange;
        initMap();
        var gameTick = Find.TickManager.TicksGame;

        var visionAffectingBuilding = mapCompSeenFog.compAffectVisionGrid[(position.z * mapSizeX) + position.x];
        var ignoreWeather = false;
        var ignoreDarkness = false;
        var sleeping = pawn.CurJob != null && pawn.jobs.curDriver.asleep;

        if (!forTargeting && sleeping)
        {
            viewRange = 8f * capacities.GetLevel(PawnCapacityDefOf.Hearing);
        }
        else
        {
            if (!shouldMove && pawnPather is not { Moving: true })
            {
                Verb attackVerb = null;
                if (pawn.CurJob != null)
                {
                    var jobDef = pawn.CurJob.def;

                    //Get manned turret sight range.
                    if (jobDef == JobDefOf.ManTurret)
                    {
                        if (pawn.CurJob.targetA.Thing is Building_Turret building_Turret)
                        {
                            attackVerb = building_Turret.AttackVerb;
                        }
                    }
                    else
                    {
                        //Standing still increase view range
                        if (jobDef == JobDefOf.AttackStatic
                            || jobDef == JobDefOf.AttackMelee
                            || jobDef == JobDefOf.Wait_Combat
                            || jobDef == JobDefOf.Hunt)
                        {
                            var primary = pawn.equipment?.Primary;
                            if (primary != null && primary.def.IsRangedWeapon)
                            {
                                attackVerb = primary.GetComp<CompEquippable>().PrimaryVerb;
                            }
                        }
                    }
                }

                if (attackVerb != null
                    && attackVerb.verbProps.range > viewRange
                    && attackVerb.verbProps.requireLineOfSight
                    && attackVerb.EquipmentSource.def.IsRangedWeapon)

                {
                    viewRange = attackVerb.verbProps.range;
                }
            }

            var rangeModifier = capacities.GetLevel(PawnCapacityDefOf.Sight);
            foreach (var visionAffecter in visionAffectingBuilding)
            {
                if (visionAffecter.Props.denyDarkness)
                {
                    var cpt = visionAffecter.parent.GetComp<CompPowerTrader>();
                    if (cpt is { PowerOn: true })
                    {
                        ignoreDarkness = true;
                    }
                }

                if (visionAffecter.Props.denyWeather)
                {
                    ignoreWeather = true;
                }

                rangeModifier *= visionAffecter.Props.fovMultiplier;
            }

            var currGlow = glowGrid.GroundGlowAt(position);
            if (gameTick - lastStatcheckTick == 40)
            {
                lastStatcheckTick = gameTick;
                nightVisionEffectiveness = pawn.GetStatValue(FoWDef.NightVisionEffectiveness);
            }

            if (currGlow < 1)
            {
                if (nightVisionEffectiveness < 1)
                {
                    if (!ignoreDarkness)
                    {
                        rangeModifier *= Mathf.Lerp(nightVisionEffectiveness, 1f, currGlow);
                    }
                }
                else
                {
                    rangeModifier *= nightVisionEffectiveness;
                }
            }

            if (!roofGrid.Roofed(position.x, position.z) && !ignoreWeather)
            {
                var curWeatherAccuracyMultiplier = weatherManager.CurWeatherAccuracyMultiplier;
                if (!curWeatherAccuracyMultiplier.Equals(1f))
                {
                    rangeModifier *= Mathf.Lerp(0.5f, 1f, curWeatherAccuracyMultiplier);
                }
            }

            viewRange *= rangeModifier;
        }


        if (viewRange < 1f)
        {
            return 8f * capacities.GetLevel(PawnCapacityDefOf.Hearing);
        }

        return viewRange;
    }

    public override void PostDeSpawn(Map map)
    {
        base.PostDeSpawn(map);
        if (!disabled && mapCompSeenFog != null)
        {
            mapCompSeenFog.fowWatchers.Remove(this);
        }

        if (lastFaction != null)
        {
            unseeSeenCells(lastFaction, lastFactionShownCells);
        }
    }

    private void CalculateFoV(Thing thing, int intRadius, IntVec3[] peekDirections)
    {
        if (!setupDone)
        {
            return;
        }

        var sizeX = mapSizeX;
        var mapSizeY = mapSizeZ;

        var oldMapView = viewMapSwitch ? viewMap1 : viewMap2;
        var newMapView = viewMapSwitch ? viewMap2 : viewMap1;

        var position = thing.Position;

        var faction = lastFaction;

        var factionShownCells = lastFactionShownCells;

        var peekRadius = peekDirections != null ? intRadius + 1 : intRadius;

        var occupiedRect = thing.OccupiedRect();

        var newViewRecMinX = Math.Min(position.x - peekRadius, occupiedRect.minX);
        var newViewRecMaxX = Math.Max(position.x + peekRadius, occupiedRect.maxX);
        var newViewRecMinZ = Math.Min(position.z - peekRadius, occupiedRect.minZ);
        var newViewRecMaxZ = Math.Max(position.z + peekRadius, occupiedRect.maxZ);

        var newViewWidth = newViewRecMaxX - newViewRecMinX + 1;
        var newViewArea = newViewWidth * (newViewRecMaxZ - newViewRecMinZ + 1);

        var oldViewRecMinZ = viewRect.minZ;
        var oldViewRecMaxZ = viewRect.maxZ;
        var oldViewRecMinX = viewRect.minX;
        var oldViewRecMaxX = viewRect.maxX;

        var oldViewWidth = viewRect.Width;
        var oldViewArea = viewRect.Area;

        if (newMapView == null || newMapView.Length < newViewArea)
        {
            newMapView = new bool[(int)(newViewArea * 1.2f)];
            if (viewMapSwitch)
            {
                viewMap2 = newMapView;
            }
            else
            {
                viewMap1 = newMapView;
            }
        }

        int occupiedX;
        for (occupiedX = occupiedRect.minX; occupiedX <= occupiedRect.maxX; occupiedX++)
        {
            int occupiedZ;
            for (occupiedZ = occupiedRect.minZ; occupiedZ <= occupiedRect.maxZ; occupiedZ++)
            {
                newMapView[((occupiedZ - newViewRecMinZ) * newViewWidth) + (occupiedX - newViewRecMinX)] = true;

                if (oldMapView == null
                    || occupiedX < oldViewRecMinX
                    || occupiedZ < oldViewRecMinZ
                    || occupiedX > oldViewRecMaxX
                    || occupiedZ > oldViewRecMaxZ)
                {
                    mapCompSeenFog.IncrementSeen(faction, factionShownCells, (occupiedZ * sizeX) + occupiedX);
                }
                else
                {
                    var oldViewRecInx = ((occupiedZ - oldViewRecMinZ) * oldViewWidth) +
                                        (occupiedX - oldViewRecMinX);
                    ref var oldViewMapValue = ref oldMapView[oldViewRecInx];
                    if (!oldViewMapValue)
                    {
                        mapCompSeenFog.IncrementSeen(faction, factionShownCells,
                            (occupiedZ * sizeX) + occupiedX);
                    }
                    else
                    {
                        oldViewMapValue = false;
                    }
                }
            }
        }

        if (intRadius > 0)
        {
            var viewBlockerCells = mapCompSeenFog.viewBlockerCells;
            var treeBlockerCells = mapCompSeenFog.treeBlockerCells;
            var coverCleared = peekDirections != null && clearOwnCover(viewBlockerCells, treeBlockerCells, position);

            var mapWidth = map.Size.x - 1;
            var mapHeight = map.Size.z - 1;

            try
            {
                if (position is { x: >= 0, z: >= 0 } && position.x <= mapWidth && position.z <= mapHeight)
                {
                    ShadowCaster.computeFieldOfViewWithShadowCasting(position.x, position.z, intRadius,
                        viewBlockerCells,
                        sizeX, mapSizeY, true, mapCompSeenFog, faction, factionShownCells, newMapView,
                        newViewRecMinX, newViewRecMinZ, newViewWidth, oldMapView, oldViewRecMinX, oldViewRecMaxX,
                        oldViewRecMinZ, oldViewRecMaxZ, oldViewWidth);
                }

                if (peekDirections != null)
                {
                    // Peek around cover, but only reveal cells that no tree hides from the pawn itself
                    var treeMask = getScratch(ref scratchTreeMask, newViewArea);
                    var peekMap = getScratch(ref scratchPeekMap, newViewArea);

                    ShadowCaster.computeFieldOfViewWithShadowCasting(position.x, position.z, intRadius,
                        treeBlockerCells, sizeX, mapSizeY, false, null, null, null, treeMask,
                        newViewRecMinX, newViewRecMinZ, newViewWidth, null, 0, 0, 0, 0, 0);

                    var peeked = false;
                    foreach (var intVec3 in peekDirections)
                    {
                        var peekPos = position + intVec3;
                        if (peekPos is not { x: >= 0, z: >= 0 } || peekPos.x > mapWidth || peekPos.z > mapHeight
                            || !peekPos.IsInside(thing) && viewBlockerCells[(peekPos.z * sizeX) + peekPos.x])
                        {
                            continue;
                        }

                        peeked = true;
                        ShadowCaster.computeFieldOfViewWithShadowCasting(peekPos.x, peekPos.z, intRadius,
                            viewBlockerCells, sizeX, mapSizeY, false, null, null, null, peekMap,
                            newViewRecMinX, newViewRecMinZ, newViewWidth, null, 0, 0, 0, 0, 0);
                    }

                    if (peeked)
                    {
                        mergePeekedCells(peekMap, treeMask, newMapView, newViewArea, newViewRecMinX, newViewRecMinZ,
                            newViewWidth, sizeX, mapSizeY, faction, factionShownCells, oldMapView, oldViewRecMinX,
                            oldViewRecMaxX, oldViewRecMinZ, oldViewRecMaxZ, oldViewWidth);
                    }
                }
            }
            finally
            {
                if (coverCleared)
                {
                    restoreOwnCover(viewBlockerCells, treeBlockerCells);
                }
            }
        }

        if (oldMapView != null)
        {
            for (var m = 0; m < oldViewArea; m++)
            {
                ref var ptr3 = ref oldMapView[m];
                if (!ptr3)
                {
                    continue;
                }

                ptr3 = false;
                var num14 = oldViewRecMinX + (m % oldViewWidth);
                var num15 = oldViewRecMinZ + (m / oldViewWidth);
                if (num15 >= 0 && num15 <= mapSizeY && num14 >= 0 && num14 <= sizeX)
                {
                    mapCompSeenFog.DecrementSeen(faction, factionShownCells, (num15 * sizeX) + num14);
                }
            }
        }

        viewMapSwitch = !viewMapSwitch;

        viewRect.maxX = newViewRecMaxX;
        viewRect.minX = newViewRecMinX;
        viewRect.maxZ = newViewRecMaxZ;
        viewRect.minZ = newViewRecMinZ;
    }

    private static bool[] getScratch(ref bool[] buffer, int area)
    {
        if (buffer == null || buffer.Length < area)
        {
            buffer = new bool[(int)(area * 1.2f)];
        }
        else
        {
            Array.Clear(buffer, 0, area);
        }

        return buffer;
    }

    // Add the peeked cells the tree mask allows, with the same seen-cell bookkeeping ShadowCaster does inline
    private void mergePeekedCells(bool[] peekMap, bool[] treeMask, bool[] newMapView, int area,
        int newViewRecMinX, int newViewRecMinZ, int newViewWidth, int sizeX, int mapSizeY, Faction faction,
        short[] factionShownCells, bool[] oldMapView, int oldViewRecMinX, int oldViewRecMaxX, int oldViewRecMinZ,
        int oldViewRecMaxZ, int oldViewWidth)
    {
        for (var i = 0; i < area; i++)
        {
            if (!peekMap[i] || !treeMask[i] || newMapView[i])
            {
                continue;
            }

            var x = newViewRecMinX + (i % newViewWidth);
            var z = newViewRecMinZ + (i / newViewWidth);
            if (x < 0 || z < 0 || x >= sizeX || z >= mapSizeY)
            {
                continue;
            }

            newMapView[i] = true;
            var cellIdx = (z * sizeX) + x;

            if (oldMapView == null || x < oldViewRecMinX || z < oldViewRecMinZ || x > oldViewRecMaxX ||
                z > oldViewRecMaxZ)
            {
                mapCompSeenFog.IncrementSeen(faction, factionShownCells, cellIdx);
                continue;
            }

            ref var oldValue = ref oldMapView[((z - oldViewRecMinZ) * oldViewWidth) + (x - oldViewRecMinX)];
            if (!oldValue)
            {
                mapCompSeenFog.IncrementSeen(faction, factionShownCells, cellIdx);
            }
            else
            {
                oldValue = false;
            }
        }
    }

    private bool clearOwnCover(bool[] viewBlockerCells, bool[] treeBlockerCells, IntVec3 position)
    {
        clearedCoverCount = 0;

        var maxX = mapSizeX - 1;
        var maxZ = mapSizeZ - 1;
        for (var dz = -1; dz <= 1; dz++)
        {
            var z = position.z + dz;
            if (z < 0 || z > maxZ)
            {
                continue;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0)
                {
                    continue;
                }

                var x = position.x + dx;
                if (x < 0 || x > maxX)
                {
                    continue;
                }

                var idx = (z * mapSizeX) + x;
                if (!viewBlockerCells[idx] || !mapCompSeenFog.IsTreeViewBlocker(idx))
                {
                    continue;
                }

                viewBlockerCells[idx] = false;
                treeBlockerCells[idx] = false;
                clearedCoverCells[clearedCoverCount++] = idx;
            }
        }

        return clearedCoverCount != 0;
    }

    private void restoreOwnCover(bool[] viewBlockerCells, bool[] treeBlockerCells)
    {
        for (var i = 0; i < clearedCoverCount; i++)
        {
            var idx = clearedCoverCells[i];
            viewBlockerCells[idx] = true;
            treeBlockerCells[idx] = true;
        }

        clearedCoverCount = 0;
    }

    public void RefreshFovTarget(ref IntVec3 targetPos)
    {
        if (!setupDone)
        {
            return;
        }

        Thing thingParent = parent;

        var oldViewMap = viewMapSwitch ? viewMap1 : viewMap2;
        var newViewMap = viewMapSwitch ? viewMap2 : viewMap1;
        // Peeking pawns need the tree mask from CalculateFoV, which this incremental refresh cannot express
        if (oldViewMap == null || lastPosition != parent.Position || lastPeekDirections != null)
        {
            UpdateFoV(true);
            return;
        }

        var radius = LastSightRange;

        var num = mapSizeX;
        var num2 = mapSizeZ;

        var position = thingParent.Position;
        var faction = lastFaction;
        var factionShownCells = lastFactionShownCells;

        var cellRect = thingParent.OccupiedRect();

        var minZ = viewRect.minZ;
        var maxZ = viewRect.maxZ;
        var minX = viewRect.minX;
        var maxX = viewRect.maxX;

        var width = viewRect.Width;
        var area = viewRect.Area;

        if (newViewMap == null || newViewMap.Length < area)
        {
            newViewMap = new bool[(int)(area * 1.2f)];
            if (viewMapSwitch)
            {
                viewMap2 = newViewMap;
            }
            else
            {
                viewMap1 = newViewMap;
            }
        }

        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                var num3 = ((j - minZ) * width) + (i - minX);
                newViewMap[num3] = true;
                oldViewMap[num3] = false;
            }
        }

        var viewBlockerCells = mapCompSeenFog.viewBlockerCells;
        viewPositions[0] = position;

        // peekDirection is always null here, peeking pawns took the full recompute above
        const int sightRange = 1;

        var num5 = map.Size.x - 1;
        var num6 = map.Size.z - 1;
        var q1Updated = false;
        var q2Updated = false;
        var q3Updated = false;
        var q4Updated = false;
        for (var l = 0; l < sightRange; l++)
        {
            ref var ptr = ref viewPositions[l];
            if (ptr is not { x: >= 0, z: >= 0 }
                || ptr.x > num5 || ptr.z > num6
                || l != 0 && !ptr.IsInside(thingParent) && viewBlockerCells[(ptr.z * num) + ptr.x])
            {
                continue;
            }

            if (ptr.x <= targetPos.x)
            {
                if (ptr.z <= targetPos.z)
                {
                    q1Updated = true;
                }
                else
                {
                    q4Updated = true;
                }
            }
            else
            {
                if (ptr.z <= targetPos.z)
                {
                    q2Updated = true;
                }
                else
                {
                    q3Updated = true;
                }
            }
        }

        for (var m = 0; m < sightRange; m++)
        {
            ref var ptr2 = ref viewPositions[m];
            if (ptr2 is not { x: >= 0, z: >= 0 } || ptr2.x > num5 || ptr2.z > num6 ||
                m != 0 && !ptr2.IsInside(thingParent) && viewBlockerCells[(ptr2.z * num) + ptr2.x])
            {
                continue;
            }

            if (q1Updated)
            {
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 0);
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 1);
            }

            if (q2Updated)
            {
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 2);
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 3);
            }

            if (q3Updated)
            {
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 4);
                ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                    num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                    oldViewMap, minX, maxX, minZ, maxZ, width, 5);
            }

            if (!q4Updated)
            {
                continue;
            }

            ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                oldViewMap, minX, maxX, minZ, maxZ, width, 6);
            ShadowCaster.computeFieldOfViewWithShadowCasting(ptr2.x, ptr2.z, radius, viewBlockerCells, num,
                num2, true, mapCompSeenFog, faction, factionShownCells, newViewMap, minX, minZ, width,
                oldViewMap, minX, maxX, minZ, maxZ, width, 7);
        }

        for (var n = 0; n < area; n++)
        {
            ref var ptr3 = ref oldViewMap[n];
            if (!ptr3)
            {
                continue;
            }

            ptr3 = false;
            var num7 = minX + (n % width);
            var num8 = minZ + (n / width);
            byte b;
            if (position.x <= num7)
            {
                b = position.z <= num8 ? (byte)1 : (byte)4;
            }
            else
            {
                b = position.z <= num8 ? (byte)2 : (byte)3;
            }

            if (!q1Updated && b == 1 || !q2Updated && b == 2 || !q3Updated && b == 3 ||
                !q4Updated && b == 4)
            {
                newViewMap[n] = true;
            }
            else
            {
                if (num8 >= 0 && num8 <= num2 && num7 >= 0 && num7 <= num)
                {
                    mapCompSeenFog.DecrementSeen(faction, factionShownCells, (num8 * num) + num7);
                }
            }
        }

        viewMapSwitch = !viewMapSwitch;
    }

    private void unseeSeenCells(Faction faction, short[] factionShownCells)
    {
        var array = viewMapSwitch ? viewMap1 : viewMap2;
        if (array == null)
        {
            return;
        }

        var minZ = viewRect.minZ;
        var minX = viewRect.minX;
        var x = map.Size.x;
        var z = map.Size.z;
        var width = viewRect.Width;
        var area = viewRect.Area;
        for (var i = 0; i < area; i++)
        {
            if (!array[i])
            {
                continue;
            }

            array[i] = false;
            var num = minX + (i % width);
            var num2 = minZ + (i / width);
            if (num2 >= 0 && num2 <= z && num >= 0 && num <= x)
            {
                mapCompSeenFog.DecrementSeen(faction, factionShownCells, (num2 * x) + num);
            }
        }

        viewRect.maxX = -1;
        viewRect.minX = -1;
        viewRect.maxZ = -1;
        viewRect.minZ = -1;
    }

    private void revealOccupiedCells()
    {
        if (parent.Faction != Faction.OfPlayer)
        {
            return;
        }

        var cellRect = parent.OccupiedRect();
        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                mapCompSeenFog.RevealCell((j * mapSizeX) + i);
            }
        }
    }

    private void updateNearbyPawn(Pawn thisPawn, float range, float rangeMod)
    {
        if (thisPawn.Map != null)
        {
            nearByPawn =
                MapUtils.GetPawnsAround(thisPawn.Position, (int)(range * rangeMod), thisPawn.Map) as List<Pawn>;
        }
        else
        {
            nearByPawn.Clear();
        }
    }

    private void livePawnHear(Faction faction)
    {
        foreach (var other in nearByPawn)
        {
            if (other.Faction == faction
                || other.pather is not { Moving: true }
                || mapCompSeenFog.IsShown(faction, other.Position))
            {
                continue;
            }

            var otherSize = other.BodySize;
            MapUtils.MakeSoundWave(
                other.Position.ToVector3() + new Vector3(otherSize * 0.5f, 0, otherSize * 0.5f),
                map,
                Mathf.Lerp(1.5f, 3.5f, otherSize / 4),
                Mathf.Lerp(1f, 2.5f, otherSize / 4));
        }
    }


    private void livePawnCalculateFov(
        IntVec3 position,
        float sightRangeMod,
        bool forceUpdate,
        Faction faction
    )
    {
        IntVec3[] peekDirection = null;
        var sightRange = -1;

        if (sightRangeMod != 0)
        {
            sightRange = Mathf.RoundToInt(sightRangeMod * CalcPawnSightRange(position, false, false));
        }

        if (sightRange != -1)
        {
            if (pawnPather is not { Moving: true }
                && pawn.CurJob != null)
            {
                var jobDef = pawn.CurJob.def;
                if (
                    jobDef == JobDefOf.AttackStatic
                    || jobDef == JobDefOf.AttackMelee
                    || jobDef == JobDefOf.Wait_Combat
                    || jobDef == JobDefOf.Hunt)
                {
                    peekDirection = GenAdj.CardinalDirections;
                }
                else if (
                    jobDef == JobDefOf.Mine
                    && pawn.CurJob.targetA != null
                    && pawn.CurJob.targetA.Cell != IntVec3.Invalid)
                {
                    peekDirection = FoWThingUtils.GetPeekArray(pawn.CurJob.targetA.Cell - parent.Position);
                }
            }

            if (calculated
                && !forceUpdate
                && faction == lastFaction
                && position == lastPosition
                && sightRange == LastSightRange
                && peekDirection == lastPeekDirections)
            {
                return;
            }

            calculated = true;
            lastPosition = position;
            LastSightRange = sightRange;
            lastPeekDirections = peekDirection;
            if (lastFaction != faction)
            {
                if (lastFaction != null)
                {
                    unseeSeenCells(lastFaction, lastFactionShownCells);
                }

                lastFaction = faction;
                lastFactionShownCells = mapCompSeenFog.GetFactionShownCells(faction);
            }

            CalculateFoV(parent, sightRange, peekDirection);
        }
        else
        {
            unseeSeenCells(lastFaction, lastFactionShownCells);
        }
    }

    private enum ThingType
    {
        Turret,
        Building,
        VisionProvider,
        Pawn,
        Animal
    }
}