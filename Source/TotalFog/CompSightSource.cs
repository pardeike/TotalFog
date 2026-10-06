// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Utils;
using TotalFog.Core;
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
    private VisibilityMask current = new(), next = new(), treeMask = new(), peekMask = new();
    private readonly Action<int, bool> change;
    private IntVec3 position = IntVec3.Invalid;
    private IntVec3[] peekDirections;
    private int nextCheck, nextHearing, lastMovement;
    private bool setup;
    public int LastSightRange { get; private set; }
    internal LinkedListNode<CompSightSource> PendingRefresh;
    public CompSightSource()
    {
        change = (index, visible) =>
        {
            if (visible) fog.IncrementSeen(faction, null, index); else fog.DecrementSeen(faction, null, index);
        };
    }
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        setup = true; pawn = parent as Pawn; position = IntVec3.Invalid;
        lastMovement = Find.TickManager.TicksGame; nextCheck = lastMovement; nextHearing = lastMovement + 100;
        UpdateFoV(true);
    }
    public override void PostExposeData() => Scribe_Values.Look(ref lastMovement, "fovLastMovementTick", 0);
    public override void ReceiveCompSignal(string signal) => UpdateFoV(true);
    public override void CompTick()
    {
        if (!parent.Spawned) return;
        int tick = Find.TickManager.TicksGame;
        if (pawn?.pather?.Moving == true) lastMovement = tick;
        if (parent.Position != position || tick >= nextCheck)
        {
            nextCheck = tick + 30; UpdateFoV();
        }
        if (pawn != null && tick >= nextHearing)
        {
            nextHearing = tick + 100; EmitHearingCues();
        }
    }
    public override void PostDeSpawn(Map previousMap)
    {
        fog?.UnregisterSource(this); LastSightRange = -1; Clear();
        map = null; fog = null; faction = null; position = IntVec3.Invalid;
    }
    private void Clear()
    {
        if (faction == null || fog == null) return;
        for (int i = 0; i < current.Area; i++) if (current.At(i)) fog.DecrementSeen(faction, null, current.GlobalIndex(i));
        current.Reset(0, 0, 0, 0, 1);
    }
    public void UpdateFoV(bool forceUpdate = false)
    {
        if (!setup || !parent.Spawned || parent.Map == null) return;
        if (map != parent.Map)
        {
            fog?.UnregisterSource(this); LastSightRange = -1; Clear();
            map = parent.Map; fog = map.GetVisibility();
            if (!fog.fowWatchers.Contains(this)) fog.fowWatchers.Add(this);
            forceUpdate = true;
        }
        var effectiveFaction = parent.Faction;
        float modifier = 1;
        if (pawn != null)
        {
            if (pawn.Dead) effectiveFaction = null;
            else if (pawn.IsPrisonerOfColony && FogSettings.PrisonerGiveVision) { effectiveFaction = Faction.OfPlayer; modifier = .2f; }
            else if (effectiveFaction != null && effectiveFaction != Faction.OfPlayer && FogSettings.AllyGiveVision && effectiveFaction.AllyOrNeutralTo(Faction.OfPlayer)) { effectiveFaction = Faction.OfPlayer; modifier = .5f; }
            else if (pawn.RaceProps.Animal)
                modifier = Compatibility.AnimalVision.Modifier(pawn);
        }
        // Player/ally/prisoner sight drives presentation. Other faction grids
        // are needed only when enemy fog targeting is explicitly enabled.
        if (effectiveFaction != Faction.OfPlayer && !FogSettings.AISmart) effectiveFaction = null;
        if (effectiveFaction == null)
        {
            fog.CancelRefresh(this); SetSightRange(-1);
            Clear(); faction = null;
            position = parent.Position; peekDirections = null;
            return;
        }
        int radius = modifier == 0 ? -1 : Mathf.RoundToInt(pawn != null ? CalcPawnSightRange(parent.Position, false, false) * modifier : BuildingRange());
        var peeking = GetPeeking();
        if (!forceUpdate && position == parent.Position && faction == effectiveFaction && radius == LastSightRange && peekDirections == peeking) return;
        fog.CancelRefresh(this);
        if (faction != effectiveFaction) { Clear(); faction = effectiveFaction; }
        position = parent.Position; SetSightRange(radius); peekDirections = peeking;
        int margin = Math.Min(Math.Max(map.Size.x, map.Size.z), Math.Max(0, radius)) + (peeking == null ? 0 : 1);
        var rect = new CellRect(position.x - margin, position.z - margin, margin * 2 + 1, margin * 2 + 1).Encapsulate(parent.OccupiedRect());
        rect.ClipInsideMap(map);
        next.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        if (faction != null && VisibilityPolicy.IsSightSource(pawn != null, radius))
        {
            foreach (var cell in parent.OccupiedRect()) if (cell.InBounds(map)) next.Add(map.cellIndices.CellToIndex(cell));
            FieldOfView.ComputeMask(map.Size.x, map.Size.z, position.x, position.z, radius, fog.viewBlockerCells, next);
            if (peeking != null) CastPeeking(rect, radius, peeking);
        }
        if (faction != null) VisibilityMask.ApplyDifference(current, next, change);
        (current, next) = (next, current);
    }
    private void SetSightRange(int radius)
    {
        fog.ChangeBlockerSourceRange(this, radius);
        LastSightRange = radius;
    }
    private void CastPeeking(CellRect rect, int radius, IntVec3[] directions)
    {
        treeMask.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        peekMask.Reset(rect.minX, rect.minZ, rect.Width, rect.Height, map.Size.x);
        // No shared blocker mutation: peeking never temporarily opens walls for other queries.
        FieldOfView.ComputeMask(map.Size.x, map.Size.z, position.x, position.z, radius, fog.treeBlockerCells, treeMask);
        foreach (var direction in directions)
        {
            var origin = position + direction;
            if (!origin.InBounds(map) || fog.viewBlockerCells[map.cellIndices.CellToIndex(origin)] && !origin.IsInside(parent)) continue;
            FieldOfView.ComputeMask(map.Size.x, map.Size.z, origin.x, origin.z, radius, fog.viewBlockerCells, peekMask);
        }
        next.AddIntersection(peekMask, treeMask);
    }
    private IntVec3[] GetPeeking()
    {
        if (pawn?.pather?.Moving == true || pawn?.CurJob == null) return null;
        var job = pawn.CurJob;
        if (job.def == JobDefOf.AttackStatic || job.def == JobDefOf.AttackMelee || job.def == JobDefOf.Wait_Combat || job.def == JobDefOf.Hunt) return GenAdj.CardinalDirections;
        return job.def == JobDefOf.Mine && job.targetA.Cell.IsValid ? FogThingUtility.GetPeekArray(job.targetA.Cell - parent.Position) : null;
    }
    public void RefreshFovTarget(ref IntVec3 targetPos) => UpdateFoV(true);
    private float BuildingRange()
    {
        if (parent.TryGetComp<CompPowerTrader>() is { PowerOn: false } || parent.TryGetComp<CompRefuelable>() is { HasFuel: false } || parent.TryGetComp<CompFlickable>() is { SwitchIsOn: false }) return 0;
        var vision = parent.TryGetComp<CompBuildingSight>();
        if (vision != null)
            return vision.Props.needManned && !fog.workingCameraConsole ? 0 : vision.Props.viewRadius * FogSettings.BuildingVisionModifier;
        if (parent is Building_Turret turret && parent.TryGetComp<CompMannable>() == null)
            return (turret.AttackVerb?.verbProps.range ?? 0) * FogSettings.TurretVisionModifier;
        return 0;
    }
    public float CalcPawnSightRange(IntVec3 cell, bool forTargeting, bool shouldMove)
    {
        if (pawn == null || !cell.InBounds(parent.Map)) return 0;
        var capacities = pawn.health.capacities;
        float hearing = 8 * capacities.GetLevel(PawnCapacityDefOf.Hearing);
        if (!forTargeting && pawn.jobs?.curDriver?.asleep == true) return hearing;
        float range = FogSettings.BaseViewRange * pawn.GetStatValue(FogDefOf.DayVisionEffectiveness, false);
        if (!shouldMove && pawn.pather?.Moving != true)
        {
            Verse.Verb attack = null;
            var job = pawn.CurJob;
            if (job?.def == JobDefOf.ManTurret && job.targetA.Thing is Building_Turret turret) attack = turret.AttackVerb;
            else if (job != null && (job.def == JobDefOf.AttackStatic || job.def == JobDefOf.AttackMelee || job.def == JobDefOf.Wait_Combat || job.def == JobDefOf.Hunt))
                attack = pawn.equipment?.Primary?.GetComp<CompEquippable>()?.PrimaryVerb;
            if (attack?.verbProps.requireLineOfSight == true && attack.EquipmentSource?.def.IsRangedWeapon == true) range = Math.Max(range, attack.verbProps.range);
        }
        float modifier = capacities.GetLevel(PawnCapacityDefOf.Sight);
        bool ignoreDarkness = false, ignoreWeather = false;
        foreach (var affecter in parent.Map.GetVisibility().VisionAffectersAt(cell))
        {
            if (affecter.parent.TryGetComp<CompPowerTrader>() is { PowerOn: false }) continue;
            ignoreDarkness |= affecter.Props.denyDarkness; ignoreWeather |= affecter.Props.denyWeather; modifier *= affecter.Props.fovMultiplier;
        }
        if (!ignoreDarkness)
        {
            float night = pawn.GetStatValue(FogDefOf.NightVisionEffectiveness);
            modifier *= night < 1 ? Mathf.Lerp(night, 1, parent.Map.glowGrid.GroundGlowAt(cell)) : night;
        }
        if (!ignoreWeather && !parent.Map.roofGrid.Roofed(cell)) modifier *= Mathf.Lerp(.5f, 1, parent.Map.weatherManager.CurWeatherAccuracyMultiplier);
        range *= modifier;
        return range < 1 ? hearing : range;
    }
    private void EmitHearingCues()
    {
        if (!FogSettings.ShowHearingCues || pawn.Faction != Faction.OfPlayer || !pawn.RaceProps.Humanlike || FogSettings.BaseHearingRange <= 0) return;
        float range = FogSettings.BaseHearingRange * pawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
        foreach (var other in map.mapPawns.AllPawnsSpawned)
        {
            if (other.Faction == faction || other.pather?.Moving != true || !other.Position.InHorDistOf(pawn.Position, range) || fog.IsShown(Faction.OfPlayer, other.Position)) continue;
            float size = other.BodySize;
            FogMapUtility.MakeSoundWave(other.Position.ToVector3() + new Vector3(size * .5f, 0, size * .5f), map, Mathf.Lerp(1.5f, 3.5f, size / 4), Mathf.Lerp(1, 2.5f, size / 4));
        }
    }
}
