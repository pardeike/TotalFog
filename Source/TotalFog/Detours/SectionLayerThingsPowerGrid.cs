using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class SectionLayerThingsPowerGrid
{
    public static bool TakePrintFrom_Prefix(Thing t)
    {
        return t.IsFogVisible(true);
    }
}