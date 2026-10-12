using Verse;

namespace TotalFog.Watchtowers;

public sealed class PlaceWorker_Watchtower : PlaceWorker
{
    public override AcceptanceReport AllowsPlacing(
        BuildableDef checkingDef,
        IntVec3 loc,
        Rot4 rot,
        Map map,
        Thing thingToIgnore = null,
        Thing thing = null
    )
    {
        var approach = loc + rot.FacingCell * 2;
        if (!approach.InBounds(map) || !approach.Walkable(map))
            return "TotalFog_TowerBlockedLadder".Translate();
        foreach (var planned in approach.GetThingList(map))
            if (planned.def.entityDefToBuild is ThingDef { passability: Traversability.Impassable })
                return "TotalFog_TowerBlockedLadder".Translate();
        foreach (var cell in GenAdj.OccupiedRect(loc, rot, new IntVec2(3, 3)))
            if (cell.InBounds(map) && cell.Roofed(map))
                return "TotalFog_TowerNoRoof".Translate();
        return true;
    }
}
