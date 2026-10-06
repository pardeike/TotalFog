using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using Verse;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    [Tool("totalfog/audio_loop", Description = "Measure actual Unity AudioSource volumes while a running hidden loop changes mute/hearing settings. Covers cell and Thing sources and restores options.")]
    public static async Task<object> AudioLoop(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        var results = new List<object>();
        bool mute = false, hearing = false;
        Thing item = null;
        Pawn viewer = null;
        Sustainer loop = null;
        Map map = null;
        IntVec3 cell = IntVec3.Invalid;
        await ctx.MainThread.InvokeAsync(() =>
        {
            mute = FogSettings.MuteHiddenSounds; hearing = FogSettings.DoAudioCheck;
            map = Find.CurrentMap;
            var fog = map.GetComponent<MapVisibility>();
            var listener = map.mapPawns.AllPawnsSpawned.First(p => p.Faction == Faction.OfPlayer && !p.Dead);
            cell = map.AllCells.Where(c => !map.fogGrid.IsFogged(c) && !fog.IsShown(Faction.OfPlayer, c) && c.Standable(map))
                .OrderBy(c => c.DistanceToSquared(listener.Position)).First();
            item = ThingMaker.MakeThing(ThingDefOf.ComponentIndustrial);
            GenSpawn.Spawn(item, cell, map);
        }, cancellationToken);
        try
        {
            await ctx.Tools.CallAsync("rimworld/jump_camera_to_cell", new { x = cell.x, z = cell.z }, cancellationToken: cancellationToken);
            foreach (bool thingSource in new[] { false, true })
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    FogSettings.MuteHiddenSounds = true; FogSettings.DoAudioCheck = false;
                    var maker = thingSource ? new TargetInfo(item) : new TargetInfo(cell, map);
                    var info = SoundInfo.InMap(maker, MaintenanceType.None); info.volumeFactor = .8f;
                    loop = DefDatabase<SoundDef>.GetNamed("HissJet").TrySpawnSustainer(info)
                        ?? throw new InvalidOperationException("The real looping sound did not start.");
                }, cancellationToken);
                foreach (string state in new[] { "muted-at-start", "unfiltered", "hearing-filtered", "muted-existing", "muted-visible", "muted-hidden-again", "restored" })
                {
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        FogSettings.MuteHiddenSounds = state.StartsWith("muted", StringComparison.Ordinal);
                        FogSettings.DoAudioCheck = state == "hearing-filtered";
                        if (state == "muted-visible")
                        {
                            viewer = PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("Colonist"), Faction.OfPlayer);
                            GenSpawn.Spawn(viewer, cell, map);
                            viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                        }
                        else if (state == "muted-hidden-again")
                        {
                            viewer.Destroy(); viewer = null;
                        }
                    }, cancellationToken);
                    await ctx.Game.FramesAsync(15, cancellationToken);
                    results.Add(await ctx.MainThread.InvokeAsync(() => new
                    {
                        thingSource, state, loop.Ended, originalVolume = loop.info.volumeFactor,
                        visible = map.GetComponent<MapVisibility>().IsShown(Faction.OfPlayer, cell),
                        factor = FogSettings.MuteHiddenSounds || FogSettings.DoAudioCheck
                            ? SoundAudibility.GetAudibilityFactor(loop.info.Maker, FogSettings.AudioSourceRange) : 1,
                        samples = Samples(loop).Select(s => new { volume = s.source.volume, muted = s.source.mute, playing = s.source.isPlaying }).ToArray()
                    }, cancellationToken));
                }
                await ctx.MainThread.InvokeAsync(() => loop.End(), cancellationToken);
                await ctx.Game.FramesAsync(30, cancellationToken);
                loop = null;
            }
            return new { cell = new { cell.x, cell.z }, results };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                FogSettings.MuteHiddenSounds = mute; FogSettings.DoAudioCheck = hearing;
                if (loop != null && !loop.Ended) loop.End();
                if (viewer != null && !viewer.Destroyed) viewer.Destroy();
                if (item != null && !item.Destroyed) item.Destroy();
            }, cancellationToken);
        }
    }

    // Test instrumentation only. These two private engine collections were
    // inspected in the installed assembly; public production hooks need neither.
    private static IEnumerable<SampleSustainer> Samples(Sustainer loop)
    {
        var subs = (IEnumerable)typeof(Sustainer).GetField("subSustainers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(loop);
        foreach (SubSustainer sub in subs)
        {
            var samples = (IEnumerable)typeof(SubSustainer).GetField("samples", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sub);
            foreach (SampleSustainer sample in samples) if (sample.source != null) yield return sample;
        }
    }
}
