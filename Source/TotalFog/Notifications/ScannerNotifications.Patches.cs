using HarmonyLib;
using RimWorld;

namespace TotalFog.Notifications;

internal static partial class ScannerNotifications
{
    internal static void Install(Harmony harmony) =>
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(typeof(CompDeepScanner), "DoFind"),
            prefix: new HarmonyMethod(typeof(ScannerNotifications), nameof(Prefix)),
            finalizer: new HarmonyMethod(typeof(ScannerNotifications), nameof(Finalizer))
        );
}
