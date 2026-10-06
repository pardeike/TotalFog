// Modified by Andreas Pardeike for Total Fog, 2026-10-04: reference-assembly override compatibility.
using System.Collections.Generic;
using RimWorld;
using Verse.AI;

namespace TotalFog;

internal class JobDriver_MonitorVision : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        var targetA = job.targetA;
        return pawn.Reserve(targetA, job, 1, -1, null, errorOnFailed);
    }

    public override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.A);
        this.FailOn(() => !Building_VisionConsole.NeedWatcher());

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        var work = new Toil();

        work.tickIntervalAction = delegate(int delta)
        {
            var actor = work.GetActor();
            var buildingCameraConsole = job.targetA.Thing as Building_VisionConsole;
            buildingCameraConsole?.Used(delta);
            actor.GainComfortFromCellIfPossible(delta, true);
        };
        work.defaultCompleteMode = ToilCompleteMode.Never;
        work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;
    }
}