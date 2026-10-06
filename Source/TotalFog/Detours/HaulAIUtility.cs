using TotalFog.Utils;
using Verse;
using Verse.AI;

namespace TotalFog.Detours;

public static class HaulAIUtility
{
    public static bool HaulToStorageJob_Prefix(Verse.Pawn p, Thing t, Job __result)
    {
        return !(
            p.Faction is { IsPlayer: true }
            && !Presentation.ThingVisibility.IsVisible(t, observerFaction: p.Faction)
        );
    }
}
