using RimWorldRealFoW.Utils;
using Verse;
using Verse.AI;

namespace RimWorldRealFoW.Detours;

public static class HaulAIUtility
{
    public static bool HaulToStorageJob_Prefix(Verse.Pawn p, Thing t, Job __result)
    {
        return !(p.Faction is { IsPlayer: true } && !t.FowIsVisible());
    }
}