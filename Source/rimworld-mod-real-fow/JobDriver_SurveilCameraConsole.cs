using System.Collections.Generic;
using RimWorld;
using Verse.AI;

namespace RimWorldRealFoW;

internal class JobDriver_SurveilCameraConsole : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        var targetA = job.targetA;
        return pawn.Reserve(targetA, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnBurningImmobile(TargetIndex.A);
        this.FailOn(() => !Building_CameraConsole.NeedWatcher());

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

        var work = new Toil();

        work.tickIntervalAction = delegate(int delta)
        {
            var actor = work.GetActor();
            var buildingCameraConsole = job.targetA.Thing as Building_CameraConsole;
            buildingCameraConsole?.Used(delta);
            actor.GainComfortFromCellIfPossible(delta, true);
        };
        work.defaultCompleteMode = ToilCompleteMode.Never;
        work.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
        work.activeSkill = () => SkillDefOf.Intellectual;
        yield return work;
    }
}