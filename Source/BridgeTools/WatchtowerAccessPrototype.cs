using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>
/// Non-shipping footprint experiment. Ordinary navigation sees one solid 3x3
/// edifice. Only this job crosses its ladder; no native pathfinding is patched.
/// </summary>
public sealed class WatchtowerAccessPrototype : WatchtowerVisualPrototype
{
    internal Pawn Occupant;
    internal IntVec3 Approach => Position + Rotation.FacingCell * 2;

    internal bool CanEnter(Pawn pawn, Pawn takee = null) =>
        Spawned
        && Approach.InBounds(Map)
        && Approach.Walkable(Map)
        && pawn.CanReach(Approach, PathEndMode.OnCell, Danger.Deadly)
        && (Occupant == null || Occupant == takee && takee.Downed && !takee.Dead);

    internal void Ground(Pawn pawn)
    {
        if (!pawn.Spawned || pawn.Map != Map)
            return;
        var destination = Approach;
        if (!destination.InBounds(Map) || !destination.Walkable(Map))
        {
            // Emergency cleanup only. A normal blocked exit needs a separate
            // gameplay policy before this experiment can become a player job.
            destination = IntVec3.Invalid;
            foreach (var cell in GenRadial.RadialCellsAround(Position, 5, true))
                if (cell.InBounds(Map) && cell.Walkable(Map))
                {
                    destination = cell;
                    break;
                }
            if (!destination.IsValid)
                throw new InvalidOperationException("No ground cell for tower prototype cleanup.");
        }
        Move(pawn, destination);
        if (Occupant == pawn)
            Occupant = null;
    }

    internal static void Move(Pawn pawn, IntVec3 cell)
    {
        pawn.pather.StopDead();
        pawn.Position = cell;
        pawn.Notify_Teleported(endCurrentJob: false);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        if (Occupant?.Spawned == true)
            Ground(Occupant);
        Occupant = null;
        base.DeSpawn(mode);
    }
}

public sealed class JobDriver_WatchtowerAccessPrototype : JobDriver
{
    internal const int ClimbTicks = 120;
    internal string Phase = "approach";
    private WatchtowerAccessPrototype Tower => job.targetA.Thing as WatchtowerAccessPrototype;
    private Pawn Takee => job.targetC.Thing as Pawn;
    internal float Progress => Mathf.Clamp01(1f - ticksLeftThisToil / (float)ClimbTicks);

    internal Vector3 DrawingOffset
    {
        get
        {
            var tower = Tower;
            if (tower == null)
                return Vector3.zero;
            var top = new Vector3(0, 0, WatchtowerVisualPrototype.OccupantDrawProjection);
            var ladder = (tower.Position - tower.Approach).ToVector3();
            return Phase switch
            {
                "ascent" => (ladder + top) * Progress,
                "watch" => top,
                "descent" => top * (1 - Progress) - ladder * Progress,
                _ => Vector3.zero,
            };
        }
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        Tower?.CanEnter(pawn, Takee) == true
        && pawn.Reserve(Tower, job, 1, -1, null, errorOnFailed)
        && (Takee == null || pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed));

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref Phase, "towerPrototypePhase", "approach");
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        AddFinishAction(condition =>
        {
            var tower = Tower;
            if (tower?.Spawned != true)
                return;
            // Keep a helpless occupant at the top for the retrieval control.
            if (pawn.Downed && tower.Occupant == pawn)
                return;
            if (pawn.Position == tower.Position)
                tower.Ground(pawn);
            if (pawn.carryTracker.CarriedThing != null)
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out _);
            if (tower.Occupant == pawn)
                tower.Occupant = null;
            Phase = "ground";
        });
        yield return Toils_Goto.GotoCell(Tower.Approach, PathEndMode.OnCell);
        yield return Climb("ascent");
        yield return Toils_General.Do(() =>
        {
            WatchtowerAccessPrototype.Move(pawn, Tower.Position);
            if (Takee != null)
            {
                if (!Takee.Downed || pawn.carryTracker.TryStartCarry(Takee, 1) != 1)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
            }
            Tower.Occupant = pawn;
            Phase = "watch";
        });
        if (Takee == null)
        {
            var watch = Toils_General.Wait(job.count);
            watch.tickAction = () => pawn.Rotation = Tower.Rotation.Opposite;
            yield return watch;
        }
        yield return Climb("descent");
        yield return Toils_General.Do(() =>
        {
            Tower.Ground(pawn);
            if (pawn.carryTracker.CarriedThing != null)
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out _);
            Phase = "ground";
        });
    }

    private Toil Climb(string phase)
    {
        var toil = ToilMaker.MakeToil("Watchtower prototype " + phase);
        toil.defaultCompleteMode = ToilCompleteMode.Delay;
        toil.defaultDuration = ClimbTicks;
        toil.initAction = () =>
        {
            pawn.pather.StopDead();
            pawn.Drawer.tweener.Notify_Teleported();
            Phase = phase;
            pawn.Rotation = phase == "ascent" ? Tower.Rotation.Opposite : Tower.Rotation;
        };
        toil.tickAction = () =>
            pawn.Rotation = phase == "ascent" ? Tower.Rotation.Opposite : Tower.Rotation;
        return toil.WithProgressBar(TargetIndex.A, () => Progress, alwaysShow: true);
    }
}
