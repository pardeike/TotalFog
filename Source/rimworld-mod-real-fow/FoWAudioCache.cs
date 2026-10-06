using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimWorldRealFoW;

public static class FoW_AudioCache
{
    private static readonly Dictionary<Thing, List<Sustainer>>
        map = new(); // Dictionary should run in average case O(1) time


    // Adds the Thing, and its sustainer, to the dictionary. Called by HarmonyPatches.Patch_SustainerRegister
    public static void Register(Thing thing, Sustainer sus)
    {
        if (thing == null || sus == null)
        {
            return;
        }

        if (!map.TryGetValue(thing, out var list))
        {
            list = [];
            map[thing] = list;
        }

        list.Add(sus);
    }

    // If the sustainer is removed on its own but the Thing is left behind, the dictionary entry must be removed to prevent holding a stale reference. HarmonyPatches.Patch_Sustainer_End
    public static void Unregister(Sustainer sus)
    {
        foreach (var kv in map)
        {
            if (!kv.Value.Remove(sus))
            {
                continue;
            }

            if (kv.Value.Count == 0)
            {
                map.Remove(kv.Key);
            }

            break;
        }
    }

    // Finds this Thing's sustainer in the dictionary, and ends it. Called by CompHiddenable.hide()
    public static void StopAll(Thing thing)
    {
        if (!map.TryGetValue(thing, out var list))
        {
            return;
        }

        foreach (var s in list.ToList())
        {
            if (!s.Ended)
            {
                s.End();
            }
        }

        map.Remove(thing);
    }

    public static float GetAudibilityFactor(TargetInfo maker, int maxDist)
    {
        var comp = maker.Map.GetMapComponentSeenFog();
        var origin = maker.Cell;
        var known = comp.knownCells;
        var indices = maker.Map.cellIndices;

        for (var d = 0; d <= maxDist; d++)
        {
            if (!(from c in GenRadial.RadialCellsAround(origin, d, true)
                    where c.InBounds(maker.Map)
                    select indices.CellToIndex(c)).Any(idx => known[idx]))
            {
                continue;
            }

            var raw = 1f - (d / (float)maxDist);
            var m = RfowSettings.VolumeMufflingModifier;
            var f = 1f - m + (raw * m);
            return Mathf.Clamp01(f);
        }

        return 0f;
    }
}