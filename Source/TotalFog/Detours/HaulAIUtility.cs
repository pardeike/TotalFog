using HarmonyLib;
using Verse;

namespace TotalFog.Detours;

public static class HaulAIUtility
{
    [HarmonyPriority(Priority.Last)]
    public static bool HaulToStorageJob_Prefix(Verse.Pawn p, Thing t)
    {
        return !(
            p.Faction is { IsPlayer: true }
            && !Presentation.ThingVisibility.IsActionable(t, p.Faction)
        );
    }
}
