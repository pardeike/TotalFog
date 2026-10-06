using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class SectionLayerThingsGeneral
{
    public static bool TakePrintFrom_Prefix(Thing t)
    {
        return t.FowIsVisible(true);
    }
}