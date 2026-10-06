using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW;

public static class MapUtils
{
    public static MapComponentSeenFog GetMapComponentSeenFog(this Map map)
    {
        var mapComponentSeenFog = map.GetComponent<MapComponentSeenFog>();
        if (mapComponentSeenFog != null)
        {
            return mapComponentSeenFog;
        }

        mapComponentSeenFog = new MapComponentSeenFog(map);
        map.components.Add(mapComponentSeenFog);

        return mapComponentSeenFog;
    }


    public static void MakeSoundWave(Vector3 loc, Map map, float size, float velocity)
    {
        var moteSoundWave = (MoteSoundWave)ThingMaker.MakeThing(FoWDef.Mote_SoundWave);
        moteSoundWave.Initialize(loc, size, velocity);
        GenSpawn.Spawn(moteSoundWave, loc.ToIntVec3(), map);
    }

    public static IEnumerable<Pawn> GetPawnsAround(IntVec3 center, int radius, Map map)
    {
        var numCells = GenRadial.NumCellsInRadius(radius);
        var pawnList = new List<Pawn>();
        var thingGrid = map.thingGrid;
        for (var i = 1; i < numCells; i++)
        {
            var c = center + GenRadial.RadialPattern[i];
            if (!c.InBounds(map))
            {
                continue;
            }

            var things = thingGrid.ThingsListAtFast(c);
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var j = 0; j < things.Count; j++)
            {
                if (things[j].def.category == ThingCategory.Pawn)
                {
                    pawnList.Add(things[j] as Pawn);
                }
            }
        }

        return pawnList;
    }
}