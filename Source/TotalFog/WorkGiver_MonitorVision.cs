using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog;

public class WorkGiver_MonitorVision : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

    public override ThingRequest PotentialWorkThingRequest =>
        ThingRequest.ForDef(FogDefOf.CameraConsole);

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerBuildings.AllBuildingsColonistOfDef(FogDefOf.CameraConsole);
    }

    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !pawn
            .Map.listerBuildings.AllBuildingsColonistOfDef(FogDefOf.CameraConsole)
            .OfType<Building_VisionConsole>()
            .Any(x => x.WorkingNow);
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!Building_VisionConsole.NeedWatcher())
        {
            return false;
        }

        LocalTargetInfo target = t;
        return pawn.CanReserve(target, 1, -1, null, forced);
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        return new Job(FogDefOf.SurveilCameraConsole, t, 1500, true);
    }
}
