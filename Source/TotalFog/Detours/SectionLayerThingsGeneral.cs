using HarmonyLib;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class SectionLayerThingsGeneral
{
    [HarmonyPriority(Priority.Last)]
    public static bool TakePrintFrom_Prefix(Thing t)
    {
        return t.IsFogVisible(true);
    }
}
