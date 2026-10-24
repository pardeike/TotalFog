// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Core;
using TotalFog.Utils;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog;

/// <summary>One sight source. Game state is sampled here; casting and footprint differences are pure.</summary>
public class CompSightSource : FogSubcomponent
{
    private Map map;
    private MapVisibility fog;
    private Pawn pawn;
    private Faction faction;
    private VisibilityMask current = new(),
        next = new(),
        treeMask = new(),
        peekMask = new();
    private readonly Action<int, bool> change;
    private Faction sharedFaction;
    private VisibilityMask sharedCurrent,
        sharedNext;
    private Action<int, bool> sharedChange;
    private int lastSharedRadius = -1;
    private IntVec3 position = IntVec3.Invalid;
    private IntVec3[] peekDirections;
    private int nextCheck,
        nextHearing,
        lastMovement;
    private bool setup;
    private Watchtowers.Building_Watchtower elevatedTower;
    internal bool NeedsGroundBlockerRefresh => elevatedTower == null || elevatedTower.SightDirty;
    public int LastSightRange { get; private set; }
    internal LinkedListNode<CompSightSource> PendingRefresh;

    public CompSightSource()
    {
        change = (index, visible) =>
        {
            if (visible)
                fog.IncrementSeen(faction, index);
            else
                fog.DecrementSeen(faction, index);
        };
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setup = true;
        pawn = parent as Pawn;
        position = IntVec3.Invalid;
        lastMovement = Compatibility.MultiplayerIntegration.TicksFor(parent.Map);
        nextCheck = lastMovement;
        nextHearing = lastMovement + FogTiming.HearingCueInterval;
        UpdateFoV(true);
    }

    public override void PostExposeData() =>
        Scribe_Values.Look(ref lastMovement, "fovLastMovementTick", 0);

    public override void ReceiveCompSignal(string signal) => UpdateFoV(true);

    public override void CompTick()
    {
        if (!parent.Spawned)
            return;
        int tick = Find.TickManager.TicksGame;
        if (pawn?.pather?.Moving == true)
            lastMovement = tick;
        if (parent.Position != position || tick >= nextCheck)
        {
            nextCheck = tick + FogTiming.SourceRefreshInterval;
            UpdateFoV();
        }
        if (pawn != null && tick >= nextHearing)
        {
            nextHearing = tick + FogTiming.HearingCueInterval;
            EmitHearingCues();
        }
    }

    public override void PostDeSpawn(Map previousMap)
    {
        fog?.UnregisterSource(this);
        LastSightRange = -1;
        Clear();
        map = null;
        fog = null;
        faction = null;
        position = IntVec3.Invalid;
    }

    private void Clear()
    {
        try
        {
            if (faction != null && fog != null)
            {
                next.Reset(0, 0, 0, 0, 1);
                VisibilityMask.ApplyAndSwap(ref current, ref next, change);
            }
        }
        finally
        {
            ClearShared();
        }
    }

    private void ClearShared()
    {
        if (sharedFaction == null)
            return;
        try
        {
            sharedNext.Reset(0, 0, 0, 0, 1);
            VisibilityMask.ApplyAndSwap(ref sharedCurrent, ref sharedNext, sharedChange);
        }
        finally
        {
            sharedFaction = null;
            lastSharedRadius = -1;
        }
    }

    public void UpdateFoV(bool forceUpdate = false)
    {
        if (!setup || !parent.Spawned || parent.Map == null)
            return;
        if (map != parent.Map)
        {
            fog?.UnregisterSource(this);
            LastSightRange = -1;
            Clear();
            map = parent.Map;
            fog = map.GetVisibility();
            if (!fog.fowWatchers.Contains(this))
                fog.fowWatchers.Add(this);
            forceUpdate = true;
        }
        // Registration during loading precedes walls. Publish neither temporary
        // coverage nor permanent discovery until every blocker is present.
        if (!fog.Initialized)
            return;
        var effectiveFaction = parent.Faction;
        Faction shareWith = null;
        float modifier = 1;
        if (pawn != null)
        {
            if (pawn.Dead)
                effectiveFaction = null;
            else if (pawn.IsPrisonerOfColony && FogSettings.PrisonerGiveVision)
            {
                effectiveFaction = pawn.HostFaction;
                modifier = .2f;
            }
            else if (
                effectiveFaction != null
                && !effectiveFaction.IsPlayer
                && FogSettings.AllyGiveVision
                && effectiveFaction.AllyOrNeutralTo(fog.PrimaryPlayerFaction)
            )
            {
                if (FogSettings.AISmart)
                    shareWith = fog.PrimaryPlayerFaction;
                else
                {
                    effectiveFaction = fog.PrimaryPlayerFaction;
                    modifier = .5f;
                }
            }
            else if (pawn.RaceProps.Animal)
                modifier = Compatibility.AnimalVision.Modifier(pawn);
        }
        // Player/ally/prisoner sight drives presentation. Other faction grids
        // are needed only when enemy fog targeting is explicitly enabled.
        if (effectiveFaction?.IsPlayer != true && !FogSettings.AISmart)
            effectiveFaction = null;
        if (effectiveFaction == null)
        {
            fog.CancelRefresh(this);
            SetSightRange(-1);
            Clear();
            faction = null;
            position = parent.Position;
            peekDirections = null;
            return;
        }
        var tower = pawn == null ? null : Watchtowers.TowerPresentation.TowerFor(pawn);
        var elevated = tower?.Occupant == pawn && tower?.ProvidesSight == true ? tower : null;
        forceUpdate |= elevatedTower != elevated || elevated?.SightDirty == true;
        elevatedTower = elevated;
        float range =
            modifier == 0 ? 0
            : pawn != null ? CalcPawnSightRange(parent.Position, false, false)
            : BuildingRange();
        int radius = modifier == 0 ? -1 : Mathf.RoundToInt(range * modifier);
        int sharedRadius = shareWith == null ? -1 : Mathf.RoundToInt(range * .5f);
        if (elevated != null && radius > 0)
            radius = TowerGeometry.SightRadius(
                radius,
                FogSettings.TowerVisionMultiplier,
                FogSettings.TowerMaxSightRange
            );
        if (elevated != null && sharedRadius > 0)
            sharedRadius = TowerGeometry.SightRadius(
                sharedRadius,
                FogSettings.TowerVisionMultiplier,
                FogSettings.TowerMaxSightRange
            );
        var peeking = elevated == null ? GetPeeking() : null;
        if (
            !forceUpdate
            && position == parent.Position
            && faction == effectiveFaction
            && radius == LastSightRange
            && peekDirections == peeking
            && sharedFaction == shareWith
            && lastSharedRadius == sharedRadius
        )
            return;
        fog.CancelRefresh(this);
        if (faction != effectiveFaction)
        {
            Clear();
            faction = effectiveFaction;
        }
        if (sharedFaction != shareWith)
        {
            ClearShared();
            if (shareWith != null)
            {
                // Sharing is a second contribution, never a replacement for
                // the visitor's own sight. Ordinary sources allocate none of it.
                sharedCurrent ??= new VisibilityMask();
                sharedNext ??= new VisibilityMask();
                sharedChange ??= (index, visible) =>
                {
                    if (visible)
                        fog.IncrementSeen(sharedFaction, index);
                    else
                        fog.DecrementSeen(sharedFaction, index);
                };
            }
            sharedFaction = shareWith;
        }
        position = parent.Position;
        SetSightRange(radius);
        peekDirections = peeking;
        var rect = SightRect(radius, peeking != null);
        ComputeSightMask(next, rect, radius, peeking, elevated);
        if (shareWith != null)
        {
            var sharedRect = SightRect(sharedRadius, peeking != null);
            if (peeking != null)
                // A peek is centered on the adjacent cell, so clipping only
                // around the pawn would extend the old shared range at corners.
                ComputeSightMask(sharedNext, sharedRect, sharedRadius, peeking, elevated);
            else
            {
                sharedNext.Reset(
                    sharedRect.minX,
                    sharedRect.minZ,
                    sharedRect.Width,
                    sharedRect.Height,
                    map.Size.x
                );
                sharedNext.AddFromWithinRadius(next, position.x, position.z, sharedRadius);
                foreach (var cell in parent.OccupiedRect())
                    if (cell.InBounds(map) && next.Contains(map.cellIndices.CellToIndex(cell)))
                        sharedNext.Add(map.cellIndices.CellToIndex(cell));
            }
            lastSharedRadius = sharedRadius;
        }
        try
        {
            VisibilityMask.ApplyAndSwap(ref current, ref next, change);
        }
        finally
        {
            if (shareWith != null)
                VisibilityMask.ApplyAndSwap(ref sharedCurrent, ref sharedNext, sharedChange);
        }
    }

    private CellRect SightRect(int radius, bool peeking)
    {
        int margin =
            Math.Min(Math.Max(map.Size.x, map.Size.z), Math.Max(0, radius)) + (peeking ? 1 : 0);
        var rect = new CellRect(
            position.x - margin,
            position.z - margin,
            margin * 2 + 1,
            margin * 2 + 1
        ).Encapsulate(parent.OccupiedRect());
        rect.ClipInsideMap(map);
        return rect;
    }

    private void ComputeSightMask(
        VisibilityMask destination,
        CellRect rect,
        int radius,
        IntVec3[] peeking,
        Watchtowers.Building_Watchtower elevated
    )
    {
        destination.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        if (faction != null && VisibilityPolicy.IsSightSource(pawn != null, radius))
        {
            foreach (var cell in parent.OccupiedRect())
                if (cell.InBounds(map))
                    destination.Add(map.cellIndices.CellToIndex(cell));
            FieldOfView.ComputeMask(
                map.Size.x,
                map.Size.z,
                position.x,
                position.z,
                radius,
                elevated == null ? fog.viewBlockerCells : elevated.SightWindow(rect),
                destination
            );
            if (elevated != null)
            {
                foreach (var cell in rect)
                    if (cell.Roofed(map))
                        destination.Remove(map.cellIndices.CellToIndex(cell));
                elevated.SightDirty = false;
            }
            if (peeking != null)
                CastPeeking(destination, rect, radius, peeking);
            // A ground observer can see a raised person without seeing the
            // hidden terrain behind the wall below them. Publish only that
            // platform's logical cell, keeping the rest of the ground cast.
            foreach (var platform in fog.TowerComponent.Towers)
                if (
                    platform.OccupantBody?.Spawned == true
                    && platform.Elevation > 0
                    && position.InHorDistOf(platform.Position, radius)
                    && Watchtowers.TowerCombat.ClearLine(
                        map,
                        position,
                        platform.Position,
                        elevated,
                        platform,
                        elevated == null
                            ? TowerGeometry.GroundAimHeight
                            : TowerGeometry.PlatformAimHeight,
                        Watchtowers.TowerCombat.Height(platform.OccupantBody),
                        forSight: true
                    )
                )
                    destination.Add(map.cellIndices.CellToIndex(platform.Position));
        }
    }

    private void SetSightRange(int radius)
    {
        fog.ChangeBlockerSourceRange(this, radius);
        LastSightRange = radius;
    }

    private void CastPeeking(
        VisibilityMask destination,
        CellRect rect,
        int radius,
        IntVec3[] directions
    )
    {
        treeMask.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        peekMask.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        // No shared blocker mutation: peeking never temporarily opens walls for other queries.
        FieldOfView.ComputeMask(
            map.Size.x,
            map.Size.z,
            position.x,
            position.z,
            radius,
            fog.treeBlockerCells,
            treeMask
        );
        foreach (var direction in directions)
        {
            var origin = position + direction;
            if (
                !origin.InBounds(map)
                || fog.viewBlockerCells[map.cellIndices.CellToIndex(origin)]
                    && !origin.IsInside(parent)
            )
                continue;
            FieldOfView.ComputeMask(
                map.Size.x,
                map.Size.z,
                origin.x,
                origin.z,
                radius,
                fog.viewBlockerCells,
                peekMask
            );
        }
        destination.AddIntersection(peekMask, treeMask);
    }

    private IntVec3[] GetPeeking()
    {
        if (pawn?.pather?.Moving == true || pawn?.CurJob == null)
            return null;
        var job = pawn.CurJob;
        if (
            job.def == JobDefOf.AttackStatic
            || job.def == JobDefOf.AttackMelee
            || job.def == JobDefOf.Wait_Combat
            || job.def == JobDefOf.Hunt
        )
            return GenAdj.CardinalDirections;
        return job.def == JobDefOf.Mine && job.targetA.Cell.IsValid
            ? FogThingUtility.GetPeekArray(job.targetA.Cell - parent.Position)
            : null;
    }

    public void RefreshFovTarget(ref IntVec3 targetPos) => UpdateFoV(true);

    private float BuildingRange()
    {
        if (
            parent.TryGetComp<CompPowerTrader>() is { PowerOn: false }
            || parent.TryGetComp<CompRefuelable>() is { HasFuel: false }
            || parent.TryGetComp<CompFlickable>() is { SwitchIsOn: false }
        )
            return 0;
        var vision = parent.TryGetComp<CompBuildingSight>();
        if (vision != null)
            return vision.Props.needManned && !fog.HasWorkingCameraConsole(parent.Faction)
                ? 0
                : vision.Props.viewRadius * FogSettings.BuildingVisionModifier;
        if (parent is Building_Turret turret && parent.TryGetComp<CompMannable>() == null)
            return (turret.AttackVerb?.verbProps.range ?? 0) * FogSettings.TurretVisionModifier;
        return 0;
    }

    public float CalcPawnSightRange(IntVec3 cell, bool forTargeting, bool shouldMove)
    {
        if (pawn == null || !cell.InBounds(parent.Map))
            return 0;
        var capacities = pawn.health.capacities;
        float hearing = 8 * capacities.GetLevel(PawnCapacityDefOf.Hearing);
        if (!forTargeting && pawn.jobs?.curDriver?.asleep == true)
            return hearing;
        float range =
            FogSettings.BaseViewRange * pawn.GetStatValue(FogDefOf.DayVisionEffectiveness, false);
        if (!shouldMove && pawn.pather?.Moving != true)
        {
            Verse.Verb attack = null;
            var job = pawn.CurJob;
            if (job?.def == JobDefOf.ManTurret && job.targetA.Thing is Building_Turret turret)
                attack = turret.AttackVerb;
            else if (
                job != null
                && (
                    job.def == JobDefOf.AttackStatic
                    || job.def == JobDefOf.AttackMelee
                    || job.def == JobDefOf.Wait_Combat
                    || job.def == JobDefOf.Hunt
                )
            )
                attack = pawn.equipment?.Primary?.GetComp<CompEquippable>()?.PrimaryVerb;
            if (
                attack?.verbProps.requireLineOfSight == true
                && !attack.ProjectileFliesOverhead()
                && attack.EquipmentSource?.def.IsRangedWeapon == true
            )
                range = Math.Max(range, attack.verbProps.range);
        }
        float modifier = capacities.GetLevel(PawnCapacityDefOf.Sight);
        bool ignoreDarkness = false,
            ignoreWeather = false,
            onTelescope = false;
        foreach (var affecter in parent.Map.GetVisibility().VisionAffectersAt(cell))
        {
            if (affecter.parent.TryGetComp<CompPowerTrader>() is { PowerOn: false })
                continue;
            ignoreDarkness |= affecter.Props.denyDarkness;
            ignoreWeather |= affecter.Props.denyWeather;
            onTelescope |= affecter.parent.def == FogDefOf.WatchTelescope;
            modifier *= affecter.Props.fovMultiplier;
        }
        if (!ignoreDarkness)
        {
            float night = pawn.GetStatValue(FogDefOf.NightVisionEffectiveness);
            modifier *=
                night < 1 ? Mathf.Lerp(night, 1, parent.Map.glowGrid.GroundGlowAt(cell)) : night;
        }
        if (!ignoreWeather && !parent.Map.roofGrid.Roofed(cell))
            modifier *= Mathf.Lerp(.5f, 1, parent.Map.weatherManager.CurWeatherAccuracyMultiplier);
        range *= modifier;
        range = range < 1 ? hearing : range;
        if (pawn.IsColonyMech)
            range *= FogSettings.ColonyMechVisionModifier;
        return onTelescope ? Math.Min(range, FogSettings.TelescopeMaxSightRange) : range;
    }

    private void EmitHearingCues()
    {
        if (
            !FogSettings.ShowHearingCues
            || pawn.Faction?.IsPlayer != true
            || !pawn.RaceProps.Humanlike
            || FogSettings.BaseHearingRange <= 0
        )
            return;
        float range =
            FogSettings.BaseHearingRange
            * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
        foreach (var other in map.mapPawns.AllPawnsSpawned)
        {
            if (
                other.Faction == faction
                || other.pather?.Moving != true
                || !other.Position.InHorDistOf(pawn.Position, range)
                || fog.IsShown(pawn.Faction, other.Position)
            )
                continue;
            float size = other.BodySize;
            FogMapUtility.MakeSoundWave(
                other.Position.ToVector3() + new Vector3(size * .5f, 0, size * .5f),
                map,
                Mathf.Lerp(1.5f, 3.5f, size / 4),
                Mathf.Lerp(1, 2.5f, size / 4),
                pawn.Faction
            );
        }
    }
}
