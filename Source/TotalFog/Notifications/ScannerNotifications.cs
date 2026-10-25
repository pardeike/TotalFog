using RimWorld;

namespace TotalFog.Notifications;

internal static partial class ScannerNotifications
{
    // A scanner's report is colony knowledge even when its deposit is out of sight.
    internal static bool IsPlayerReport { get; private set; }

    public static void Prefix(CompScanner __instance, out bool? __state)
    {
        __state = IsPlayerReport;
        IsPlayerReport = __state.Value || __instance?.parent?.Faction?.IsPlayer == true;
    }

    public static void Finalizer(bool? __state)
    {
        // Another prefix may skip this one. Its empty state must not clear an outer report.
        if (__state.HasValue)
            IsPlayerReport = __state.Value;
    }
}
