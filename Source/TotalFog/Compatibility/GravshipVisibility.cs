using HarmonyLib;
using RimWorld;
using Verse;

namespace TotalFog.Compatibility;

internal static class GravshipVisibility
{
    internal static bool Revealed =>
        ModsConfig.OdysseyActive
        && FogSettings.ClearFogDuringTargeting
        && (
            WorldComponent_GravshipController.CutsceneInProgress
            || Find.GravshipController?.LandingAreaConfirmationInProgress == true
        );

    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.OdysseyActive)
            return;
        var method = AccessTools.Method(typeof(WorldComponent_GravshipController), "LandingEnded");
        if (method == null)
        {
            Log.Warning("[Total Fog] Gravship integration: LandingEnded is unavailable.");
            return;
        }
        harmony.Patch(
            method,
            postfix: new HarmonyMethod(typeof(GravshipVisibility), nameof(LandingEnded))
        );
    }

    public static void LandingEnded()
    {
        if (FogSettings.ClearFogDuringTargeting)
            Find.CurrentMap?.mapDrawer?.RegenerateEverythingNow();
    }
}
