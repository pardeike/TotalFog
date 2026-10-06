using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RimWorld;
using RimWorldRealFoW.Utils;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW.Detours;

internal static class Verb
{
    private static readonly Dictionary<HitCheckKey, bool> hitCheckCache = new();
    private static int hitCheckCacheTick = int.MinValue;

    internal static void CanHitCellFromCellIgnoringRange_Postfix(this Verse.Verb __instance, ref bool __result,
        IntVec3 sourceSq, IntVec3 targetLoc, bool includeCorners = false)
    {
        if (!__result || !__instance.verbProps.requireLineOfSight)
        {
            return;
        }

        var caster = __instance.caster;
        if (caster == null)
        {
            return;
        }

        var tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        if (tick != hitCheckCacheTick || hitCheckCache.Count > 16384)
        {
            hitCheckCache.Clear();
            hitCheckCacheTick = tick;
        }

        var key = new HitCheckKey(__instance, sourceSq, targetLoc, includeCorners);
        if (hitCheckCache.TryGetValue(key, out var cachedResult))
        {
            __result = cachedResult;
            return;
        }

        __result = caster.Faction != null && seenByFaction(caster, targetLoc) ||
                   fovLineOfSight(sourceSq, targetLoc, caster);
        hitCheckCache[key] = __result;
    }

    private static bool seenByFaction(Thing thing, IntVec3 targetLoc)
    {
        if (thing?.Map == null || thing.Faction == null)
        {
            return true;
        }

        var mapComponentSeenFog = thing.Map.GetMapComponentSeenFog();
        return mapComponentSeenFog == null || mapComponentSeenFog.IsShown(thing.Faction, targetLoc);
    }

    private static bool fovLineOfSight(IntVec3 sourceSq, IntVec3 targetLoc, Thing thing)
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
            return true;
        }

        var map = thing.Map;

        var mapComponentSeenFog = map?.GetMapComponentSeenFog();
        if (mapComponentSeenFog == null)
        {
            return true;
        }

        var compMainComponent = (CompMainComponent)thing.TryGetCompLocal(CompMainComponent.CompDef);
        var compFieldOfViewWatcher = compMainComponent?.FieldOfViewWatcher;
        if (compFieldOfViewWatcher == null)
        {
            return true;
        }

        var shouldMove = sourceSq != thing.Position && !thing.Position.AdjacentToCardinal(sourceSq);
        var num = Mathf.RoundToInt(compFieldOfViewWatcher.CalcPawnSightRange(sourceSq, true, shouldMove));
        if (!sourceSq.InHorDistOf(targetLoc, num))
        {
            return false;
        }

        var intVec = targetLoc - sourceSq;
        byte specificOctant;
        if (intVec.x >= 0)
        {
            if (intVec.z >= 0)
            {
                specificOctant = intVec.x >= intVec.z ? (byte)0 : (byte)1;
            }
            else
            {
                specificOctant = intVec.x >= -intVec.z ? (byte)7 : (byte)6;
            }
        }
        else if (intVec.z >= 0)
        {
            specificOctant = -intVec.x >= intVec.z ? (byte)3 : (byte)2;
        }
        else
        {
            specificOctant = -intVec.x >= -intVec.z ? (byte)4 : (byte)5;
        }

        var array = new bool[1];
        if (map != null)
        {
            ShadowCaster.computeFieldOfViewWithShadowCasting(sourceSq.x, sourceSq.z, num,
                mapComponentSeenFog.viewBlockerCells, map.Size.x, map.Size.z, false, null, null, null, array, 0, 0,
                0, null, 0, 0, 0, 0, 0, specificOctant, targetLoc.x, targetLoc.z);
        }

        return array[0];
    }

    private readonly struct HitCheckKey(Verse.Verb verb, IntVec3 src, IntVec3 tgt, bool corners)
        : IEquatable<HitCheckKey>
    {
        private readonly Verse.Verb verb = verb;
        private readonly IntVec3 src = src;
        private readonly IntVec3 tgt = tgt;
        private readonly bool corners = corners;

        public bool Equals(HitCheckKey other)
        {
            return ReferenceEquals(verb, other.verb) && src == other.src && tgt == other.tgt &&
                   corners == other.corners;
        }

        public override bool Equals(object obj)
        {
            return obj is HitCheckKey key && Equals(key);
        }

        public override int GetHashCode()
        {
            var hashCode = RuntimeHelpers.GetHashCode(verb);
            hashCode = (hashCode * 397) ^ src.GetHashCode();
            hashCode = (hashCode * 397) ^ tgt.GetHashCode();
            return (hashCode * 397) ^ (corners ? 1 : 0);
        }
    }
}