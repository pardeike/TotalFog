using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Detours;

internal static class Verb
{
    internal static void CanHitCellFromCellIgnoringRange_Postfix(
        this Verse.Verb __instance,
        ref bool __result,
        IntVec3 sourceSq,
        IntVec3 targetLoc,
        bool includeCorners = false
    )
    {
        if (!__result || !__instance.verbProps.requireLineOfSight)
        {
            return;
        }

        var caster = __instance.caster;
        if (caster == null || caster.Faction?.IsPlayer != true && !FogSettings.AISmart)
        {
            return;
        }

        // Coverage and blockers can change while the game is paused or within
        // one tick. Always evaluate current sight; a per-tick cache is stale.
        __result =
            caster.Faction != null && SeenByFaction(caster, targetLoc)
            || FovLineOfSight(sourceSq, targetLoc, caster);
    }

    internal static void TryFindShootLineFromTo_Postfix(
        Verse.Verb __instance,
        LocalTargetInfo targ,
        ref bool __result
    )
    {
        if (
            __result
            && targ.HasThing
            && __instance.caster?.Faction?.IsPlayer == true
            && __instance.ProjectileFliesOverhead()
        )
            __result = SeenByFaction(__instance.caster, targ.Thing.PositionHeld);
    }

    internal static void TurretTargetPostfix(
        Building_TurretGun __instance,
        Thing t,
        ref bool __result
    )
    {
        if (__result && t != null && __instance.Faction?.IsPlayer == true)
            __result = SeenByFaction(__instance, t.PositionHeld);
    }

    private static bool SeenByFaction(Thing thing, IntVec3 targetLoc)
    {
        if (thing?.Map == null || thing.Faction == null)
        {
            return true;
        }

        var mapComponentSeenFog = thing.Map.GetVisibility();
        return mapComponentSeenFog == null || mapComponentSeenFog.IsShown(thing.Faction, targetLoc);
    }

    private static bool FovLineOfSight(IntVec3 sourceSq, IntVec3 targetLoc, Thing thing)
    {
        if (thing == null)
        {
            return true;
        }

        var compMannable = thing.TryGetComp<CompMannable>();
        if (compMannable != null)
        {
            var manningPawn = compMannable.ManningPawn;
            if (manningPawn == null)
            {
                return true;
            }

            thing = manningPawn;
            sourceSq += thing.Position - thing.InteractionCell;
        }

        if (thing is not Verse.Pawn)
        {
            // Unmanned turrets use their faction's current coverage above.
            // Other non-pawn casters retain their native policy.
            return thing is not Building_Turret;
        }

        var map = thing.Map;

        var mapComponentSeenFog = map?.GetVisibility();
        if (map == null || mapComponentSeenFog == null)
        {
            return true;
        }

        var compMainComponent = thing.TryGetComp<CompFog>();
        var compFieldOfViewWatcher = compMainComponent?.FieldOfViewWatcher;
        if (compFieldOfViewWatcher == null)
        {
            return true;
        }

        var shouldMove = sourceSq != thing.Position && !thing.Position.AdjacentToCardinal(sourceSq);
        var num = Mathf.RoundToInt(
            compFieldOfViewWatcher.CalcPawnSightRange(sourceSq, true, shouldMove)
        );
        if (!sourceSq.InHorDistOf(targetLoc, num))
        {
            return false;
        }

        return TotalFog.Core.FieldOfView.CanSee(
            map.Size.x,
            map.Size.z,
            sourceSq.x,
            sourceSq.z,
            targetLoc.x,
            targetLoc.z,
            num,
            mapComponentSeenFog.viewBlockerCells
        );
    }
}
