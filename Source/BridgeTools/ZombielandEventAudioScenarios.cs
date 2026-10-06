using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    [Tool("totalfog/zombieland_event_audio", Description = "Run real spitter and substantial rising-wave spawn paths under hidden mute, disabled filtering, hearing, visible mute, hidden-again and disabled-siren controls. Observes newly created native one-shot samples and Unity volume, checks spawn counts and unchanged paused ticks. Stops only its own samples, restores settings/sight and reloads the unchanged named fixture.")]
    public static async Task<object> ZombielandEventAudio(IRimBridgeContext ctx,
        CancellationToken cancellationToken, string saveName, int frames = 15)
    {
        if (frames < 12 || frames > 60) throw new ArgumentException("Use 12..60 frames per phase.");
        await ambientGate.WaitAsync(cancellationToken);
        bool oldMute = FogSettings.MuteHiddenSounds, oldHearing = FogSettings.DoAudioCheck,
            oldBypass = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.AudioSourceRange;
        float oldMuffling = FogSettings.VolumeMufflingModifier, oldAmbient = Prefs.VolumeAmbient;
        var ownedSamples = new HashSet<SampleOneShot>();
        var rows = new List<object>();
        Map map = null;
        MapVisibility fog = null;
        object settings = null;
        System.Reflection.FieldInfo sirenField = null, letterField = null;
        bool oldSiren = false, oldLetter = false, addedSight = false;
        IntVec3 cell = IntVec3.Invalid;
        int startTick = -1, index = -1;
        bool passed = true;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load the named fixture first.");
                fog = map.GetVisibility();
                if (!Find.TickManager.Paused || !fog.Initialized)
                    throw new InvalidOperationException("Use a paused initialized map.");
                FogSettings.OnlyOutsideColony = false;
                FogSettings.AudioSourceRange = 500; FogSettings.VolumeMufflingModifier = .5f;
                Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                cell = map.AllCells.First(c => c.Standable(map) && !map.fogGrid.IsFogged(c) &&
                    !fog.IsShown(Faction.OfPlayer, c) && c.GetThingList(map).Count == 0);
                index = map.cellIndices.CellToIndex(cell);
                var settingsType = AccessTools.TypeByName("ZombieLand.ZombieSettings");
                settings = AccessTools.Field(settingsType, "Values").GetValue(null);
                sirenField = AccessTools.Field(settings.GetType(), "playZombieEventSiren");
                letterField = AccessTools.Field(settings.GetType(), "showZombieEventLetters");
                oldSiren = (bool)sirenField.GetValue(settings); oldLetter = (bool)letterField.GetValue(settings);
                sirenField.SetValue(settings, true); letterField.SetValue(settings, false);
                startTick = Find.TickManager.TicksGame;
            }, cancellationToken);
            var camera = await ctx.Tools.CallAsync("rimworld/jump_camera_to_cell", new { x = cell.x, z = cell.z }, cancellationToken: cancellationToken);
            if (!camera.Succeeded()) throw new InvalidOperationException("Event audio camera setup failed.");
            foreach (string kind in new[] { "spitter", "wave" })
                foreach (string state in new[] { "hidden-muted", "unfiltered", "hearing", "visible-muted", "hidden-again", "cue-disabled" })
                {
                    int expectedCount = 0, spawnedCount = 0;
                    float expectedVolume = 1;
                    SampleOneShot[] newSamples = null;
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        StopOwnedOneShots(ownedSamples);
                        if (addedSight)
                        {
                            fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                            addedSight = false;
                        }
                        sirenField.SetValue(settings, state != "cue-disabled");
                        FogSettings.MuteHiddenSounds = state == "hidden-muted" || state == "visible-muted" || state == "hidden-again";
                        FogSettings.DoAudioCheck = state == "hearing";
                        if (state == "visible-muted")
                        {
                            fog.IncrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                            addedSight = true;
                        }
                        expectedVolume = state == "cue-disabled" ? 0 :
                            FogSettings.MuteHiddenSounds || FogSettings.DoAudioCheck
                                ? SoundAudibility.GetAudibilityFactor(new TargetInfo(cell, map), FogSettings.AudioSourceRange) : 1;
                        var existingSamples = Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                        int before = map.mapPawns.AllPawnsSpawned.Count;
                        if (kind == "spitter")
                        {
                            expectedCount = 1;
                            AccessTools.Method(AccessTools.TypeByName("ZombieLand.ZombieSpitter"), "Spawn")
                                .Invoke(null, new object[] { map, cell });
                        }
                        else
                        {
                            var tools = AccessTools.TypeByName("ZombieLand.Tools");
                            var colonists = ((int, int))AccessTools.Method(tools, "ColonistsInfo").Invoke(null, new object[] { map });
                            expectedCount = Math.Max(4, colonists.Item1 * 4 + 1);
                            Predicate<IntVec3> validCell = p => p.InBounds(map) && p.Standable(map) && !map.fogGrid.IsFogged(p);
                            var iterator = (IEnumerator)AccessTools.Method(AccessTools.TypeByName("ZombieLand.ZombiesRising"), "SpawnEventProcess")
                                .Invoke(null, new object[] { map, expectedCount, cell, validCell, false, true,
                                    Enum.Parse(AccessTools.TypeByName("ZombieLand.ZombieType"), "Normal") });
                            int steps = 0;
                            while (iterator.MoveNext())
                                if (++steps > 8192) throw new InvalidOperationException("Wave spawn did not finish.");
                        }
                        spawnedCount = map.mapPawns.AllPawnsSpawned.Count - before;
                        newSamples = Find.SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                            !existingSamples.Contains(s) && s.subDef.parentDef.defName == "ZombiesRising").ToArray();
                        foreach (var sample in newSamples) ownedSamples.Add(sample);
                    }, cancellationToken);
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        var samples = newSamples.Select(s => new { infoVolume = s.info.volumeFactor,
                            camera = s.info.IsOnCamera, hasMap = s.info.Maker.Map != null,
                            volume = s.source == null ? 0 : s.source.volume,
                            playing = s.source != null && s.source.isPlaying }).ToArray();
                        bool valid = spawnedCount == expectedCount && Find.TickManager.TicksGame == startTick &&
                            (expectedVolume <= 0 ? samples.Length == 0 : samples.Length > 0 &&
                                samples.All(s => Math.Abs(s.infoVolume - expectedVolume) < .001f) &&
                                samples.Any(s => s.playing && s.volume > 0));
                        passed &= valid;
                        rows.Add(new { kind, state, passed = valid, spawnedCount, expectedCount, expectedVolume,
                            current = Visibility.IsVisible(map, cell), samples, tick = Find.TickManager.TicksGame });
                    }, cancellationToken);
                }
            return new { passed, startTick, rows };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    StopOwnedOneShots(ownedSamples);
                    if (addedSight) fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                    if (settings != null)
                    {
                        sirenField.SetValue(settings, oldSiren); letterField.SetValue(settings, oldLetter);
                    }
                    FogSettings.MuteHiddenSounds = oldMute; FogSettings.DoAudioCheck = oldHearing;
                    FogSettings.OnlyOutsideColony = oldBypass; FogSettings.AudioSourceRange = oldRange;
                    FogSettings.VolumeMufflingModifier = oldMuffling; Prefs.VolumeAmbient = oldAmbient;
                }, CancellationToken.None);
                var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new
                    { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!restored.Succeeded()) throw new InvalidOperationException("Event audio fixture restoration failed.");
            }
            finally { ambientGate.Release(); }
        }
    }

    private static void StopOwnedOneShots(HashSet<SampleOneShot> owned)
    {
        // Released samples can hold stale references to pooled sources.
        foreach (var sample in Find.SoundRoot.oneShotManager.PlayingOneShots)
            if (owned.Contains(sample)) sample.source?.Stop();
    }
}
