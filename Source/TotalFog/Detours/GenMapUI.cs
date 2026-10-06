using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class GenMapUI
{
    public static bool DrawThingLabel_Prefix(Thing thing)
    {
        return Presentation.ThingVisibility.IsVisible(thing, allowMemory: false);
    }
}
