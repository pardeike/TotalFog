using HarmonyLib;

namespace TotalFog.Notifications;

internal static partial class ThreatAlerts
{
    internal static void Install(Harmony harmony)
    {
        foreach (
            string name in new[]
            {
                "Alert_FireInHomeArea",
                "Alert_CultistPsychicRitual",
                "Alert_EmergingPitGate",
                "Alert_TimedRaidsArriving",
                "Alert_ActivatorCountdown",
                "Alert_InfestationDelay",
            }
        )
        {
            var type = AccessTools.TypeByName("RimWorld." + name);
            if (type != null)
                PatchSafety.Patch(
                    harmony,
                    AccessTools.DeclaredMethod(type, "GetReport"),
                    postfix: new HarmonyMethod(typeof(ThreatAlerts), nameof(ReportPostfix))
                );
        }
    }
}
