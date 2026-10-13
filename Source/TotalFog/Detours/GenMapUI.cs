using HarmonyLib;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class GenMapUI
{
    [HarmonyPriority(Priority.Last)]
    public static bool DrawThingLabel_Prefix(Thing thing)
    {
        return Presentation.ThingVisibility.CanInspect(thing);
    }
}
