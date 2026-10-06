using TotalFog;
using TotalFog.Audio;
using Verse.Sound;
using Xunit;

namespace TotalFog.Tests;

public sealed class AudioLifecycleTests
{
    [Fact]
    public void LoopVolumeUsesEachCurrentPolicyWithoutChangingTheEngineInput()
    {
        FogSettings.DoAudioCheck = true; FogSettings.MuteHiddenSounds = false;
        var sample = new SampleSustainer { Info = new SoundInfo { volumeFactor = .8f } };
        foreach (float factor in new[] { .5f, .5f, 0f, 1f })
        {
            SoundAudibility.Factor = factor;
            float result = .8f;
            SustainerVisibility.VolumePostfix(sample, ref result);
            Assert.Equal(.8f * factor, result);
            Assert.Equal(.8f, sample.Info.volumeFactor);
        }
    }

    [Theory]
    [InlineData(false, false, .8f)]
    [InlineData(true, false, 0f)]
    [InlineData(false, true, 0f)]
    public void SettingsTakeEffectForAnAlreadyExistingLoop(bool hearing, bool mute, float expected)
    {
        FogSettings.DoAudioCheck = hearing;
        FogSettings.MuteHiddenSounds = mute;
        SoundAudibility.Factor = 0;
        var sample = new SampleSustainer { Info = new SoundInfo { volumeFactor = .8f } };
        float result = .8f;
        SustainerVisibility.VolumePostfix(sample, ref result);
        Assert.Equal(expected, result);
    }
}
