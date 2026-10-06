using UnityEngine;
using Verse;

namespace RimWorldRealFoW.Detours;

public static class MouseoverReadout
{
    private static readonly Vector2 botLeft = new(15f, 65f);

    public static bool MouseoverReadoutOnGUI_Prefix(Verse.MouseoverReadout __instance)
    {
        if (Event.current.type != EventType.Repaint || Find.MainTabsRoot.OpenTab != null)
        {
            return true;
        }

        var c = UI.MouseCell();
        if (!c.InBounds(Find.CurrentMap))
        {
            return true;
        }

        var mapComponentSeenFog = Find.CurrentMap.GetMapComponentSeenFog();
        if (c.Fogged(Find.CurrentMap) || mapComponentSeenFog == null ||
            mapComponentSeenFog.knownCells[Find.CurrentMap.cellIndices.CellToIndex(c)])
        {
            return true;
        }

        GenUI.DrawTextWinterShadow(new Rect(256f, UI.screenHeight - 256, -256f, 256f));
        Text.Font = GameFont.Small;
        GUI.color = new Color(1f, 1f, 1f, 0.8f);
        var rect = new Rect(botLeft.x, UI.screenHeight - botLeft.y, 999f, 999f);
        Widgets.Label(rect, "NotVisible".Translate());
        GUI.color = Color.white;
        return false;
    }
}