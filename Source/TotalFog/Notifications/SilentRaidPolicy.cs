using RimWorld;
using Verse;

namespace TotalFog.Notifications;

internal static class SilentRaidPolicy
{
    public static void Prefix(IncidentWorker __instance, IncidentParms parms, out bool __state)
    {
        __state = false;
        if (!FogSettings.SilentRaids || parms == null || parms.silent ||
            __instance is not IncidentWorker_RaidEnemy && __instance?.def != IncidentDefOf.ManhunterPack)
            return;
        parms.silent = true;
        __state = true;
    }

    public static void Finalizer(IncidentParms parms, bool __state)
    {
        if (__state) parms.silent = false;
    }

    public static void ManhunterArrivalSlowdown(TimeSlower slower, IncidentWorker worker, IncidentParms parms)
    {
        // 1.6's aggressive-animal worker forces slowdown even for silent
        // incidents. Only its selected manhunter arrival call uses this helper;
        // ordinary combat and other slowdown callers remain native.
        if (!FogSettings.SilentRaids || worker.def != IncidentDefOf.ManhunterPack || !parms.silent)
            slower.SignalForceNormalSpeedShort();
    }
}
