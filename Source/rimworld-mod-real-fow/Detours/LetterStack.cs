using RimWorld.Planet;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class LetterStack
{
    public static void ReceiveLetter_Prefix(ref LookTargets lookTargets)
    {
        if (lookTargets is not { PrimaryTarget.HasThing: true })
        {
            return;
        }

        var thing = lookTargets.PrimaryTarget.Thing;
        if (thing is { Faction: null } || !thing.Faction.IsPlayer)
        {
            lookTargets = new GlobalTargetInfo(thing.Position, thing.Map);
        }
    }
}