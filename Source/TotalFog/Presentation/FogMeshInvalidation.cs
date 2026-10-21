using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static class FogMeshInvalidation
{
    internal static ulong FlagsForSightChange(Map map, int index)
    {
        ulong flags = FogDefOf.RealFogOfWar | MapMeshFlagDefOf.Gas;
        // Plain chunks have no visibility component to dirty their static mesh.
        // Refresh only cells containing such geometry, using the native grid.
        var things = map.thingGrid.ThingsListAtFast(index);
        for (int i = 0; i < things.Count; i++)
            if (
                things[i] is not ThingWithComps
                && things[i].def.drawerType
                    is DrawerType.MapMeshOnly
                        or DrawerType.MapMeshAndRealTime
            )
            {
                flags |= MapMeshFlagDefOf.Things;
                break;
            }
        // The lighting adapter only replaces this cell's artificial glow with
        // zero. If the native contribution is already zero, changing sight
        // cannot change any lighting vertex, including shared section borders.
        // Native light/roof changes still invalidate their own lighting meshes.
        var glow = map.glowGrid.VisualGlowAt(index);
        if ((glow.r | glow.g | glow.b | glow.a) != 0)
            flags |= MapMeshFlagDefOf.GroundGlow;
        return flags;
    }
}
