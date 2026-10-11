using RimWorld;

namespace TotalFog.Audio;

internal static class MusicVisibility
{
    public static bool DangerModePrefix(ref bool __result)
    {
        if (!FogSettings.SuppressCombatMusic)
            return true;
        __result = false;
        return false;
    }

    public static bool TransitionPrefix(MusicTransition __instance, ref bool __result)
    {
        if (!FogSettings.SuppressCombatMusic || !IsAnomaly(__instance))
            return true;
        __result = false;
        return false;
    }

    public static void CheckTransitionsPrefix(MusicManagerPlay __instance)
    {
        if (FogSettings.SuppressCombatMusic && IsAnomaly(__instance.TriggeredTransition))
            __instance.ForceFadeoutAndSilenceFor(0f, 1f);
    }

    public static bool ForceTransitionPrefix(MusicTransitionDef transition) =>
        !FogSettings.SuppressCombatMusic
        || transition == null
        || !IsAnomalyWorker(transition.workerType);

    private static bool IsAnomaly(MusicTransition transition) =>
        transition
            is HorrorRelaxTransition
                or HorrorTensionTransition
                or HorrorMonolithAdvancedTransition
                or HorrorCombatTransition
                or CorpseAttackTransition
                or MetalHellTransition;

    private static bool IsAnomalyWorker(System.Type type) =>
        type != null
        && (
            typeof(HorrorRelaxTransition).IsAssignableFrom(type)
            || typeof(HorrorTensionTransition).IsAssignableFrom(type)
            || typeof(HorrorMonolithAdvancedTransition).IsAssignableFrom(type)
            || typeof(HorrorCombatTransition).IsAssignableFrom(type)
            || typeof(CorpseAttackTransition).IsAssignableFrom(type)
            || typeof(MetalHellTransition).IsAssignableFrom(type)
        );
}
