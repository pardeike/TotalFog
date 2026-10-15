using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TotalFog.Utils;

public static class FogThingUtility
{
    private static readonly Dictionary<IntVec3, IntVec3[]> peekArrayCache = new(15);

    public static bool PlantBlocksView(Plant plant)
    {
        if (plant?.def.plant == null)
        {
            return false;
        }

        if (!plant.def.plant.IsTree)
        {
            return false;
        }

        return !plant.def.plant.isStump;
    }

    public static IntVec3[] GetPeekArray(IntVec3 intVec3)
    {
        IntVec3[] result;
        if (peekArrayCache.TryGetValue(intVec3, out var value))
        {
            result = value;
        }
        else
        {
            var array = new[] { intVec3 };
            peekArrayCache[intVec3] = array;
            result = array;
        }

        return result;
    }

    public static DeferredNotifications GetDeferredNotifications(this Map map)
    {
        return map.GetComponent<DeferredNotifications>();
    }

    extension(Thing thing)
    {
        public bool IsFogVisible(bool forRender = false) =>
            Presentation.ThingVisibility.IsVisible(thing);
    }
}
