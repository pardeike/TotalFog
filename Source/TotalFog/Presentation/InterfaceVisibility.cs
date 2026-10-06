using System.Collections.Generic;
using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static partial class InterfaceVisibility
{
    // Mouseover dispatch follows one CompSelectProxy before reading the label.
    // A visible proxy must not grant access to its unobserved target's live UI.
    private static bool CanRead(Thing thing, IntVec3? renderedCell = null)
    {
        if (!ThingVisibility.IsVisible(thing, allowMemory: false, renderedCell: renderedCell)) return false;
        var target = thing?.TryGetComp<CompSelectProxy>()?.thingToSelect;
        return target == null || ThingVisibility.IsVisible(target, allowMemory: false);
    }

    public static bool PawnLabelPrefix(Pawn pawn) =>
        ThingVisibility.IsVisible(pawn, allowMemory: false, renderedCell: pawn?.PositionHeld);

    public static void DrawOverlay(Thing thing)
    {
        if (CanRead(thing, thing?.PositionHeld)) thing.DrawGUIOverlay();
    }

    public static IntVec3 TooltipPosition(Thing thing)
    {
        if (thing == null || !CanRead(thing)) return IntVec3.Invalid;
        return CustomInspectionCell.TryGetCell(thing, out var cell) ? cell : thing.Position;
    }

    public static List<Thing> MouseoverThings(IntVec3 cell, Map map)
    {
        var things = cell.GetThingList(map);
        int firstHidden = 0;
        while (firstHidden < things.Count && CanRead(things[firstHidden])) firstHidden++;
        if (firstHidden == things.Count) return things;
        // This local copy preserves the authoritative thing grid. It is needed
        // only when the currently hovered cell contains a hidden entity.
        var visible = new List<Thing>(things.Count);
        for (int i = 0; i < things.Count; i++)
            if (i < firstHidden || CanRead(things[i])) visible.Add(things[i]);
        return visible;
    }

}
