using Verse;

namespace TotalFog.Detours;

public static class EnvironmentStatsDrawer
{
    public static void ShouldShowWindowNow_Postfix(ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        __result = Presentation.CellVisibility.IsCurrent(Find.CurrentMap, UI.MouseCell());
    }
}
