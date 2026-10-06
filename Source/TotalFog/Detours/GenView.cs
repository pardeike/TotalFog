using RimWorld;
using Verse;

namespace TotalFog.Detours;

public static class GenView
{
    private static MapVisibility lastUsedMapComponent;

    private static Map lastUsedMap;

    public static void ShouldSpawnMotesAt_Postfix(IntVec3 loc, Map map, bool drawOffscreen, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        var mapComponentSeenFog = lastUsedMapComponent;
        if (map != lastUsedMap)
        {
            lastUsedMap = map;
            mapComponentSeenFog = lastUsedMapComponent = map.GetComponent<MapVisibility>();
        }

        __result = mapComponentSeenFog == null || mapComponentSeenFog.IsShown(Faction.OfPlayer, loc.x, loc.z);
    }
}