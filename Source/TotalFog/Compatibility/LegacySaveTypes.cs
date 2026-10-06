using System;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>Read old saves at the engine boundary without exporting the original mod's types.</summary>
internal static partial class LegacySaveTypes
{
    public static void ResolvePostfix(Type baseType, string providedClassName, ref Type __result)
    {
        if (__result != null || Scribe.mode != LoadSaveMode.LoadingVars) return;
        var replacement = providedClassName switch
        {
            "RimWorldRealFoW.MapComponentSeenFog" => typeof(MapVisibility),
            "RimWorldRealFoW.PendingAlertManager" => typeof(DeferredNotifications),
            "RimWorldRealFoW.DeferredNotification" => typeof(DeferredNotification),
            "RimWorldRealFoW.RfowSettings" => typeof(FogSettings),
            "RimWorldRealFoW.Building_CameraConsole" => typeof(Building_VisionConsole),
            "RimWorldRealFoW.Building_SurveillanceCamera" => typeof(Building_VisionCamera),
            "RimWorldRealFoW.MoteSoundWave" => typeof(Mote_HearingCue),
            "RimWorldRealFoW.JobDriver_SurveilCameraConsole" => typeof(JobDriver_MonitorVision),
            _ => null
        };
        if (replacement != null && baseType != null && baseType.IsAssignableFrom(replacement)) __result = replacement;
    }
}
