using Verse;

namespace RimWorldRealFoW;

public class PlaceWorker_UnderRoof : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
        Thing thingToIgnore = null, Thing thing = null)
    {
        if (!map.roofGrid.Roofed(loc))
        {
            return new AcceptanceReport("MustBeUnderRoof".Translate());
        }

        return true;
    }
}