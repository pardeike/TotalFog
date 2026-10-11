using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

// The native rescue scanner rejects an unreachable pawn before HasJobOnThing.
// Scan the sparse tower list instead, so automatic doctors can reach the ladder.
public sealed class WorkGiver_WatchtowerRescue : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
        pawn.Map.GetComponent<MapComponent_Watchtowers>().Towers;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (t is not Building_Watchtower tower || tower.Occupant?.Downed != true)
            return false;
        bool result = false;
        WatchtowerPatches.RescueEligibilityPrefix(pawn, tower.Occupant, forced, ref result);
        return result;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Job result = null;
        WatchtowerPatches.RescueJobPrefix(pawn, ((Building_Watchtower)t).Occupant, ref result);
        return result;
    }
}
