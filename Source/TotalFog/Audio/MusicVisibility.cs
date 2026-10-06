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
}
