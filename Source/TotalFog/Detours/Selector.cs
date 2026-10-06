using RimWorld.Planet;
using TotalFog.Utils;
using Verse;

namespace TotalFog.Detours;

public static class Selector
{
    public static bool SelectInternal_Prefix(object obj)
    {
        var thing = obj as Thing;
        var pawn = obj as Verse.Pawn;
        return !(
            thing is { Destroyed: false }
            && (pawn == null || !pawn.IsWorldPawn())
            && !Presentation.ThingVisibility.IsVisible(thing, allowMemory: false)
        );
    }
}
