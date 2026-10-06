using Verse.Sound;
namespace TotalFog.Audio;

internal static class SustainerVisibility
{
    public static void VolumePostfix(SampleSustainer __instance, ref float __result)
    {
        if (!(FogSettings.DoAudioCheck || FogSettings.MuteHiddenSounds)) return;
        // Apply after the engine's volume, fade and context calculations. Never
        // change the source volume or end a loop merely because sight is lost.
        __result *= SoundAudibility.GetAudibilityFactor(__instance.Info.Maker, FogSettings.AudioSourceRange);
    }
}
