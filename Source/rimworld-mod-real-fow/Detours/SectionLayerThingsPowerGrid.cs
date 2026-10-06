using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class SectionLayerThingsPowerGrid
{
    public static bool TakePrintFrom_Prefix(Thing t)
    {
        return t.FowIsVisible(true);
    }
}