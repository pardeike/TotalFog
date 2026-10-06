using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using Verse;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    [Tool(
        "totalfog/zombieland_global_ambient",
        Description = "Spawn the real creepy ambience definition with the same native camera sound info used by Zombieland; verify actual Unity loop volume is unchanged under disabled filtering, hidden mute and hearing modes. Does not modify the production night/wandering volume dictionary or manager. Restores options and ends only its own loop. Paused audio-policy proof, not a night-cycle/scheduler test."
    )]
    public static async Task<object> ZombielandGlobalAmbient(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        int frames = 15
    )
    {
        if (frames < 12 || frames > 60)
            throw new ArgumentException("Use 12..60 frames per phase.");
        await ambientGate.WaitAsync(cancellationToken);
        bool oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck;
        float oldAmbient = Prefs.VolumeAmbient;
        Sustainer loop = null;
        IDictionary wanderingVolumes = null;
        DictionaryEntry[] originalVolumes = null;
        int startTick = -1;
        float baseline = 0;
        bool passed = true;
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (Find.CurrentMap == null || !Find.TickManager.Paused)
                        throw new InvalidOperationException("Load a paused paired map first.");
                    var type =
                        AccessTools.TypeByName("ZombieLand.ZombieStateHandler")
                        ?? throw new InvalidOperationException("Load Zombieland first.");
                    wanderingVolumes = (IDictionary)
                        AccessTools.Field(type, "creepyAmbientSoundVolumes").GetValue(null);
                    originalVolumes = wanderingVolumes
                        .Keys.Cast<object>()
                        .Select(key => new DictionaryEntry(key, wanderingVolumes[key]))
                        .ToArray();
                    startTick = Find.TickManager.TicksGame;
                    Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                    FogSettings.MuteHiddenSounds = false;
                    FogSettings.DoAudioCheck = false;
                    loop =
                        DefDatabase<SoundDef>
                            .GetNamed("ZombiesClosingIn")
                            .TrySpawnSustainer(SoundInfo.OnCamera(MaintenanceType.None))
                        ?? throw new InvalidOperationException(
                            "The native creepy ambience did not start."
                        );
                    loop.info.volumeFactor = .5f;
                },
                cancellationToken
            );
            foreach (
                string state in new[]
                {
                    "unfiltered",
                    "hidden-mute",
                    "hearing",
                    "mute-and-hearing",
                    "restored",
                }
            )
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        FogSettings.MuteHiddenSounds =
                            state == "hidden-mute" || state == "mute-and-hearing";
                        FogSettings.DoAudioCheck =
                            state == "hearing" || state == "mute-and-hearing";
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        var samples = Samples(loop)
                            .Select(s => new
                            {
                                volume = s.source.volume,
                                playing = s.source.isPlaying,
                                muted = s.source.mute,
                            })
                            .ToArray();
                        float peak = samples.Length == 0 ? 0 : samples.Max(s => s.volume);
                        if (state == "unfiltered")
                            baseline = peak;
                        float factor = SoundAudibility.GetAudibilityFactor(
                            loop.info.Maker,
                            FogSettings.AudioSourceRange
                        );
                        bool dictionaryUnchanged =
                            wanderingVolumes.Count == originalVolumes.Length
                            && originalVolumes.All(e =>
                                wanderingVolumes.Contains(e.Key)
                                && Equals(wanderingVolumes[e.Key], e.Value)
                            );
                        bool valid =
                            !loop.Ended
                            && loop.info.Maker.Map == null
                            && factor == 1
                            && loop.info.volumeFactor == .5f
                            && baseline > 0
                            && Math.Abs(peak - baseline) < .001f
                            && samples.Any(s => s.playing && !s.muted && s.volume > 0)
                            && dictionaryUnchanged
                            && Find.TickManager.TicksGame == startTick;
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                state,
                                passed = valid,
                                factor,
                                peak,
                                baseline,
                                dictionaryUnchanged,
                                cameraSource = loop.info.IsOnCamera,
                                makerHasMap = loop.info.Maker.Map != null,
                                samples,
                            }
                        );
                    },
                    cancellationToken
                );
            }
            return new
            {
                passed,
                startTick,
                rows,
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (loop != null && !loop.Ended)
                            loop.End();
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                        Prefs.VolumeAmbient = oldAmbient;
                    },
                    CancellationToken.None
                );
            }
            finally
            {
                ambientGate.Release();
            }
        }
    }
}
