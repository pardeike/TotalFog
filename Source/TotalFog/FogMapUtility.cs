using System.Runtime.CompilerServices;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog;

public static class FogMapUtility
{
    // Map owns its component for its lifetime. Weak keys also handle the
    // component's reference back to its map without retaining unloaded maps.
    private static readonly ConditionalWeakTable<Map, MapVisibility> visibility = new();

    public static MapVisibility GetVisibility(this Map map) =>
        visibility.GetValue(map, FindOrCreate);

    private static MapVisibility FindOrCreate(Map map)
    {
        var mapComponentSeenFog = map.GetComponent<MapVisibility>();
        if (mapComponentSeenFog != null)
        {
            return mapComponentSeenFog;
        }

        mapComponentSeenFog = new MapVisibility(map);
        map.components.Add(mapComponentSeenFog);

        return mapComponentSeenFog;
    }

    public static void MakeSoundWave(Vector3 loc, Map map, float size, float velocity) =>
        MakeSoundWave(loc, map, size, velocity, Faction.OfPlayer);

    public static void MakeSoundWave(
        Vector3 loc,
        Map map,
        float size,
        float velocity,
        Faction observerFaction
    )
    {
        var moteSoundWave = (Mote_HearingCue)ThingMaker.MakeThing(FogDefOf.Mote_SoundWave);
        moteSoundWave.Initialize(loc, size, velocity, observerFaction);
        GenSpawn.Spawn(moteSoundWave, loc.ToIntVec3(), map);
    }
}
