using System.Collections.Generic;
using RimWorld;
using TotalFog.Core;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

public sealed class JobDriver_Watchtower : JobDriver
{
    internal Building_Watchtower Tower => job.targetA.Thing as Building_Watchtower;
    private bool Descending => job.def == WatchtowerDefOf.TotalFog_WatchtowerDescend;
    private bool Retrieving => job.def == WatchtowerDefOf.TotalFog_WatchtowerRetrieve;
    private float startingElevation;
    private bool goingDown;
    private int duration;
    internal bool Climbing;
    internal bool HoldingWhileDrafted => job.count <= 0 && pawn.Drafted;
    internal int WatchTicksRemaining => HoldingWhileDrafted ? -1 : ticksLeftThisToil;

    internal bool HoldWhenDrafted()
    {
        if (
            !pawn.Drafted
            || pawn.Downed
            || pawn.Dead
            || pawn.InMentalState
            || job.def != WatchtowerDefOf.TotalFog_WatchtowerWatch
            || Climbing
            || Tower?.Watching != true
            || Tower.Occupant != pawn
            || Tower.Elevation != 1
            || CurToil == null
        )
            return false;
        // Drafting converts timed building use into holding this position.
        // Keep the same driver, reservation and platform occupation.
        job.count = 0;
        CurToil.defaultCompleteMode = ToilCompleteMode.Never;
        return true;
    }

    public override string GetReport() =>
        !Climbing && Tower is { Watching: false, ApproachClear: false } && Tower.HasActor(pawn)
            ? "TotalFog_TowerWaitingForExit".Translate(Tower.Label)
        : Climbing
            ? (goingDown ? "TotalFog_TowerClimbingDown" : "TotalFog_TowerClimbingUp").Translate(
                Tower.Label
            )
        : base.GetReport();

    private float TransitionElevation =>
        goingDown ? Mathf.Lerp(startingElevation, 0, Progress) : Progress;
    private float Progress =>
        duration == 0 ? 0 : Mathf.Clamp01(1f - ticksLeftThisToil / (float)duration);

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (Descending)
            return Tower?.HasActor(pawn) == true;
        if (Tower?.CanEnter(pawn, Retrieving ? job.targetC.Thing : null) != true)
            return false;
        return pawn.Reserve(Tower, job, 1, -1, null, errorOnFailed)
            && (!Retrieving || pawn.Reserve(job.targetC, job, 1, -1, null, errorOnFailed))
            && (
                !Retrieving
                || !job.targetB.HasThing
                || job.targetB.Thing is Building_Bed bed
                    && pawn.Reserve(bed, job, bed.SleepingSlotsCount, 0, null, errorOnFailed)
            );
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref startingElevation, "towerStartingElevation");
        Scribe_Values.Look(ref goingDown, "towerGoingDown");
        Scribe_Values.Look(ref duration, "towerTransitionTicks");
        Scribe_Values.Look(ref Climbing, "towerClimbing");
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        SetFinalizerJob(_ =>
            Tower?.HasActor(pawn) == true
            && Tower.ElevationFor(pawn) > 0
            && !pawn.Downed
            && !pawn.Dead
                ? JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerDescend, Tower)
                : null
        );
        AddFinishAction(_ =>
        {
            var tower = Tower;
            if (tower?.Spawned != true || !tower.HasActor(pawn))
                return;
            tower.Watching = false;
            tower.RefreshSight();
            float elevation = tower.ElevationFor(pawn.Dead ? pawn.Corpse : pawn);
            if (elevation <= 0 || (pawn.Downed || pawn.Dead) && elevation < 1)
            {
                if (pawn.Spawned)
                    Building_Watchtower.Move(pawn, tower.Approach);
                else if (pawn.Corpse?.Spawned == true)
                    pawn.Corpse.Position = tower.Approach;
                tower.Release(pawn);
            }
            // A downed pawn or corpse at the top stays there for ladder retrieval.
        });
        if (!Descending)
        {
            yield return Toils_Goto.GotoCell(Tower.Approach, PathEndMode.OnCell);
            yield return Toils_General.Do(() =>
            {
                if (!Tower.CanEnter(pawn, Retrieving ? job.targetC.Thing : null))
                    EndJobWith(JobCondition.Incompletable);
                else if (!Retrieving)
                    Tower.Claim(pawn);
                else
                    Tower.ClaimRescuer(pawn);
            });
            yield return Climb(false);
            yield return Toils_General.Do(() =>
            {
                Building_Watchtower.Move(pawn, Tower.Position);
                Climbing = false;
                if (Retrieving)
                {
                    var body = job.targetC.Thing;
                    var patient = Tower.Occupant;
                    if (
                        body != Tower.OccupantBody
                        || body?.Spawned != true
                        || pawn.carryTracker.TryStartCarry(body, 1) != 1
                    )
                    {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }
                    Tower.Release(patient);
                    Tower.Claim(pawn);
                }
                Tower.Elevation = 1;
                Tower.Watching = !Retrieving;
                Tower.RefreshSight();
            });
        }
        if (!Descending && !Retrieving)
        {
            // The native rotation tracker always calls Job.GetTarget. None is
            // not a valid index there; B is unused until the ground exit.
            rotateToFace = TargetIndex.B;
            var watch = ToilMaker.MakeToil("Watch from tower");
            watch.initAction = () => job.targetB = LocalTargetInfo.Invalid;
            // Drafted orders hold a position. Undrafted building use has a
            // bounded duration, including older/programmatic orders with no count.
            int watchTicks =
                job.count > 0 ? job.count
                : pawn.Drafted ? 0
                : GenDate.TicksPerHour;
            watch.defaultCompleteMode =
                watchTicks > 0 ? ToilCompleteMode.Delay : ToilCompleteMode.Never;
            watch.defaultDuration = watchTicks;
            watch.tickAction = () =>
            {
                // The empty target leaves combat aim to the native tracker.
                if (!pawn.stances.FullBodyBusy)
                    pawn.Rotation = new Rot4(
                        TowerGeometry.WatchFacing(
                            Find.TickManager.TicksGame - job.startTick,
                            pawn.Drafted
                        )
                    );
                if (
                    !job.playerForced
                        && !pawn.Drafted
                        && (
                            pawn.needs.food?.CurLevelPercentage < .18f
                            || pawn.needs.rest?.CurLevelPercentage < .15f
                        )
                    || pawn.Downed
                    || pawn.InMentalState
                    || pawn.IsBurning()
                )
                    ReadyForNextToil();
                else if (
                    FogSettings.TowerShootingAllowed
                    && pawn.Drafted
                    && pawn.drafter.FireAtWill
                    && !pawn.stances.FullBodyBusy
                )
                {
                    var verb = pawn.TryGetAttackVerb(null);
                    if (verb?.verbProps.IsMeleeAttack == false && pawn.IsHashIntervalTick(30))
                    {
                        var target = AttackTargetFinder.BestShootTargetFromCurrentPosition(
                            pawn,
                            TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat
                        );
                        if (target != null)
                            verb.TryStartCastOn(target.Thing);
                    }
                }
            };
            yield return watch;
        }
        // Wait for a blocked ladder; no instantaneous teleport through a new wall.
        var clear = ToilMaker.MakeToil("Wait for clear ladder");
        clear.defaultCompleteMode = ToilCompleteMode.Never;
        clear.initAction = () =>
        {
            Climbing = false;
            if (Tower.Occupant == pawn)
            {
                Tower.Watching = false;
                Tower.RefreshSight();
            }
            if (!Tower.ApproachClear)
                Messages.Message(
                    "TotalFog_TowerExitBlocked".Translate(),
                    Tower,
                    MessageTypeDefOf.RejectInput,
                    false
                );
        };
        clear.tickAction = () =>
        {
            if (pawn.Downed || pawn.Dead)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (Tower.ApproachClear)
                ReadyForNextToil();
            else if (
                pawn.needs.food?.CurLevelPercentage < .05f
                || pawn.needs.rest?.CurLevelPercentage < .05f
                || pawn.InMentalState
                || pawn.IsBurning()
            )
                Tower.TryEmergencyExit(pawn);
        };
        yield return clear;
        yield return Climb(true);
        yield return Toils_General.Do(() =>
        {
            Building_Watchtower.Move(pawn, Tower.Approach);
            Climbing = false;
            Tower.Release(pawn);
            if (Retrieving && pawn.carryTracker.CarriedThing != null)
            {
                var body = pawn.carryTracker.CarriedThing;
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Direct, out _);
                if (body is Pawn patient && patient.Downed && job.targetB.Thing is Building_Bed bed)
                {
                    var rescue = JobMaker.MakeJob(JobDefOf.Rescue, patient, bed);
                    rescue.count = 1;
                    pawn.jobs.jobQueue.EnqueueFirst(rescue);
                }
            }
        });
        if (!Retrieving)
        {
            // Finish undrafted duty away from the ladder before vanilla idle
            // jobs can choose a long Wait_Wander there. Queued work takes over
            // directly; drafted pawns keep their ordered ground position.
            yield return Toils_General.Do(() => job.targetB = ExitDestination());
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
        }
    }

    private IntVec3 ExitDestination()
    {
        if (pawn.Drafted || pawn.jobs.jobQueue.Count > 0)
            return pawn.Position;
        for (int i = 0; i < 4; i++)
        {
            var cell = Tower.Approach + new Rot4((Tower.Rotation.AsInt + i) % 4).FacingCell;
            if (
                cell.InBounds(pawn.Map)
                && !Tower.OccupiedRect().Contains(cell)
                && cell.Standable(pawn.Map)
                && cell.GetFirstPawn(pawn.Map) == null
                && pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)
            )
                return cell;
        }
        return pawn.Position;
    }

    private Toil Climb(bool descend)
    {
        var toil = ToilMaker.MakeToil(descend ? "Descend tower" : "Climb tower");
        toil.handlingFacing = true;
        toil.defaultCompleteMode = ToilCompleteMode.Delay;
        toil.defaultDuration = FogSettings.TowerClimbTicks;
        toil.initAction = () =>
        {
            pawn.pather.StopDead();
            pawn.Drawer.tweener.Notify_Teleported();
            pawn.Rotation = Tower.Rotation.Opposite;
            goingDown = descend;
            Climbing = true;
            startingElevation = descend ? Tower.ElevationFor(pawn) : 0;
            // Toils are built before the ascent. Read elevation when descent
            // starts, otherwise watch/rescue jobs incorrectly descend in one tick.
            duration = descend
                ? Mathf.Max(1, Mathf.CeilToInt(startingElevation * FogSettings.TowerClimbTicks))
                : FogSettings.TowerClimbTicks;
            ticksLeftThisToil = duration;
            if (Tower.Occupant == pawn)
            {
                Tower.Watching = false;
                Tower.RefreshSight();
            }
        };
        toil.tickAction = () =>
        {
            // A ladder is climbed backwards on descent: face its rungs both ways.
            pawn.Rotation = Tower.Rotation.Opposite;
            Tower.SetElevation(pawn, TransitionElevation);
        };
        return toil.WithProgressBar(TargetIndex.None, () => Progress, alwaysShow: true);
    }
}
