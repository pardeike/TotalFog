using RimWorldRealFoW.Utils;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class GenMapUI
{
    public static bool DrawThingLabel_Prefix(Thing thing)
    {
        return thing.FowIsVisible();
    }
}