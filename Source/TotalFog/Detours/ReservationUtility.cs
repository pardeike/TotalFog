using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class ReservationUtility
{
    extension(Verse.Pawn p)
    {
        public void CanReserve_Postfix(ref bool __result, LocalTargetInfo target)
        {
            if (
                __result
                && p.Faction is { IsPlayer: true }
                && target.HasThing
                && target.Thing.def.category != ThingCategory.Pawn
            )
            {
                __result = target.Thing.IsFogVisible();
            }
        }

        public void CanReserveAndReach_Postfix(ref bool __result, LocalTargetInfo target)
        {
            if (
                __result
                && p.Faction is { IsPlayer: true }
                && target.HasThing
                && target.Thing.def.category != ThingCategory.Pawn
            )
            {
                __result = target.Thing.IsFogVisible();
            }
        }
    }
}
