using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class SectionLayerThingsGeneral
{
    public static bool TakePrintFrom_Prefix(Thing t)
    {
        return t.IsFogVisible(true);
    }
}