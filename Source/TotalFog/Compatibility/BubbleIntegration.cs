using System;
using HarmonyLib;
using Verse;

namespace TotalFog.Compatibility;

internal static class BubbleIntegration
{
    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.IsActive("jaxe.bubbles"))
            return;
        var method = AccessTools.Method("Bubbles.Core.Bubbler:DrawBubble");
        if (method == null)
        {
            Log.Warning("[Total Fog] Interaction Bubbles integration: DrawBubble is unavailable.");
            return;
        }
        try
        {
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(BubbleIntegration), nameof(DrawPrefix))
            );
        }
        catch (Exception exception)
        {
            Log.Warning("[Total Fog] Interaction Bubbles integration: " + exception.Message);
        }
    }

    public static bool DrawPrefix(Pawn pawn) =>
        !FogSettings.HideSpeakBubble || Presentation.ThingVisibility.IsVisible(pawn);
}
