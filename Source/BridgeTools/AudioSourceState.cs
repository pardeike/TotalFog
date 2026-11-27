using HarmonyLib;
using UnityEngine;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    // Waylith exposes these properties as write-only. Read their backing state
    // for diagnostics, without changing the native audio source.
    private static bool SourceMuted(AudioSource source) =>
        (bool)AccessTools.Field(typeof(AudioSource), "_mute").GetValue(source);

    private static AudioRolloffMode SourceRolloff(AudioSource source) =>
        (AudioRolloffMode)AccessTools.Field(typeof(AudioSource), "_rolloffMode").GetValue(source);

    private static float SourceMinDistance(AudioSource source) =>
        (float)AccessTools.Field(typeof(AudioSource), "_minDistance").GetValue(source);

    private static float SourceMaxDistance(AudioSource source) =>
        (float)AccessTools.Field(typeof(AudioSource), "_maxDistance").GetValue(source);
}
