using RimWorld;
using Verse;

namespace RimWorldRealFoW;

public class PlaceWorker_NextToWall : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map,
        Thing thingToIgnore = null, Thing thing = null)
    {
        var c = loc - rot.FacingCell;
        var edifice = c.GetEdifice(map);
        if (!c.InBounds(map) || !loc.InBounds(map))
        {
            return false;
        }

        //Additional joy object's code
        if (
            edifice?.def == null
            || edifice.def != ThingDefOf.Wall
            && !edifice.def.IsSmoothed
            && (edifice.Faction == null
                || edifice.Faction != Faction.OfPlayer
                || edifice.def.graphicData == null
                || edifice.def.graphicData.linkFlags == LinkFlags.None
                || (LinkFlags.Wall & edifice.def.graphicData.linkFlags) == LinkFlags.None))
        {
            return new AcceptanceReport("MustBeNextToWall".Translate());
        }

        return true;
    }
}