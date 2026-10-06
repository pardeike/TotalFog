using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class ZombielandEffectsScenarios
{
    private static Pawn screamingAlbino;
    private static readonly HashSet<Material> screamMaterials = new();
    private static int screamMeshes,
        screamBubbleDraws;
    private static FieldInfo observedScreamPhase;
    private static readonly List<object> screamResets = new();
    private static readonly HashSet<Pawn> screamVictims = new();

    [Tool(
        "totalfog/zombieland_albino_scream",
        Description = "Let a newly spawned Albino acquire and complete a scream through its native AI and ordinary Normal playback in a staged remote room with two hostile humans held in Wait with per-pawn melee disabled and their job-override checks suppressed. Never starts the Albino's job, sets scream/queue/cooldown fields, calls its action helper or steps ticks. Observe native scream meshes, attached bubble, actual sound samples, sight transitions, victim stun/vomit and natural expiry. Test native damage-job notification without changing health. Audio controls: hidden-muted, visible-muted, hidden-unfiltered. Restores scoped fog options/probes and reloads the unchanged named base. This isolates scream behavior, not victims' combat AI."
    )]
    public static async Task<object> AlbinoScream(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string audioMode = "hidden-muted",
        int frames = 12
    )
    {
        if (
            audioMode is not ("hidden-muted" or "visible-muted" or "hidden-unfiltered")
            || frames < 12
            || frames > 30
        )
            throw new ArgumentException("Use a supported audio control and 12..30 frames.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.albino-scream-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony,
            oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck,
            addedSight = false,
            passed = true;
        Map map = null;
        MapVisibility fog = null;
        CellRect room = default;
        IntVec3 cell = IntVec3.Invalid;
        FieldInfo phase = null,
            affected = null,
            cooldown = null;
        var owned = new List<Thing>();
        var victims = new List<Pawn>();
        var bubbles = new HashSet<Mote>();
        var samples = new Dictionary<SampleOneShot, (float factor, float peak, bool played)>();
        var rows = new List<object>();
        int startTick = -1,
            acquiredTick = -1,
            firstActiveTick = -1,
            initialCooldown = -1,
            maximumAffected = 0,
            maximumPhase = 0,
            nativeVictimVomitFrames = 0,
            nativeVictimStunFrames = 0;
        string acquiredJob = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the base fixture first.");
                    fog = map.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    var settings = AccessTools
                        .Field(AccessTools.TypeByName("ZombieLand.ZombieSettings"), "Values")
                        .GetValue(null);
                    if (
                        AccessTools
                            .Field(settings.GetType(), "attackMode")
                            .GetValue(settings)
                            .ToString() == "OnlyColonists"
                    )
                        throw new InvalidOperationException(
                            "This hostile-human fixture requires an attack mode that includes other humans."
                        );
                    if (
                        !(bool)
                            AccessTools
                                .Field(settings.GetType(), "playWallAndSabotageSounds")
                                .GetValue(settings)
                        || !(bool)
                            AccessTools
                                .Field(settings.GetType(), "showZombieThoughtBubbles")
                                .GetValue(settings)
                    )
                        throw new InvalidOperationException(
                            "Enable native sabotage sound and thought bubbles for their positive controls."
                        );
                    FogSettings.OnlyOutsideColony = false;
                    FogSettings.DoAudioCheck = false;
                    FogSettings.MuteHiddenSounds = audioMode != "hidden-unfiltered";
                    cell = GenRadial
                        .RadialCellsAround(new IntVec3(212, 0, 79), 32, true)
                        .First(candidate =>
                            new CellRect(candidate.x - 4, candidate.z - 4, 9, 9).All(c =>
                                c.InBounds(map)
                                && !map.fogGrid.IsFogged(c)
                                && !fog.IsShown(Faction.OfPlayer, c)
                                && c.GetEdifice(map) == null
                                && !c.GetThingList(map).OfType<Pawn>().Any()
                            )
                            && new[]
                            {
                                candidate,
                                candidate + IntVec3.North * 2,
                                candidate + IntVec3.South * 2,
                            }.All(c => c.Standable(map))
                        );
                    room = new CellRect(cell.x - 4, cell.z - 4, 9, 9);
                    foreach (var c in room.EdgeCells)
                        owned.Add(
                            GenSpawn.Spawn(
                                ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                                c,
                                map
                            )
                        );
                    var faction = Find.FactionManager.AllFactionsVisible.First(f =>
                        f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction
                    );
                    Rand.PushState(8127);
                    try
                    {
                        foreach (var offset in new[] { IntVec3.North * 2, IntVec3.South * 2 })
                        {
                            var victim = PawnGenerator.GeneratePawn(
                                new PawnGenerationRequest(
                                    PawnKindDefOf.Colonist,
                                    faction,
                                    forceGenerateNewPawn: true,
                                    canGeneratePawnRelations: false,
                                    allowAddictions: false
                                )
                            );
                            owned.Add(victim);
                            victims.Add(victim);
                            screamVictims.Add(victim);
                            // Wait can strike adjacent enemies without a new job.
                            // Change only this disposable pawn's copied definition.
                            victim.kindDef = (PawnKindDef)
                                AccessTools
                                    .Method(typeof(object), "MemberwiseClone")
                                    .Invoke(victim.kindDef, null);
                            victim.kindDef.canMeleeAttack = false;
                            GenSpawn.Spawn(victim, cell + offset, map);
                            var wait = JobMaker.MakeJob(JobDefOf.Wait);
                            wait.expiryInterval = 6000;
                            victim.jobs.StartJob(wait, JobCondition.InterruptForced);
                            map.attackTargetsCache.UpdateTarget(victim);
                        }
                        screamingAlbino = (Pawn)
                            AccessTools
                                .Method(
                                    AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions"),
                                    "SpawnZombie"
                                )
                                .Invoke(
                                    null,
                                    new object[]
                                    {
                                        cell,
                                        map,
                                        Enum.Parse(
                                            AccessTools.TypeByName("ZombieLand.ZombieType"),
                                            "Albino"
                                        ),
                                        true,
                                    }
                                );
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    if (screamingAlbino == null)
                        throw new InvalidOperationException("Albino spawn failed.");
                    owned.Add(screamingAlbino);
                    phase = AccessTools.Field(screamingAlbino.GetType(), "scream");
                    observedScreamPhase = phase;
                    screamResets.Clear();
                    affected = AccessTools.Field(
                        screamingAlbino.GetType(),
                        "albinoScreamAffectedCount"
                    );
                    cooldown = AccessTools.Field(screamingAlbino.GetType(), "albinoNextScreamTick");
                    initialCooldown = (int)cooldown.GetValue(screamingAlbino);
                    screamMaterials.Clear();
                    foreach (
                        var pair in (IEnumerable)
                            AccessTools
                                .Field(
                                    AccessTools.TypeByName("ZombieLand.Constants"),
                                    "screamPairs"
                                )
                                .GetValue(null)
                    )
                    {
                        screamMaterials.Add(
                            (Material)pair.GetType().GetField("Item1").GetValue(pair)
                        );
                        screamMaterials.Add(
                            (Material)pair.GetType().GetField("Item2").GetValue(pair)
                        );
                    }
                    harmony.Patch(
                        AccessTools.Method(screamingAlbino.GetType(), "CustomTick"),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(KeepScreamFixtureAlive)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.JobDriver_Sabotage"),
                            "ResetActionState"
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveScreamReset)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(typeof(Pawn_JobTracker), "CheckForJobOverride"),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(HoldScreamVictimsWaiting)
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.GraphicToolbox"),
                            "DrawScaledMesh"
                        ),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveScreamMesh)
                        )
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(RimWorld.MoteBubble), "DrawAt"),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveScreamBubble)
                        )
                    );
                    SetSight(audioMode == "visible-muted");
                    startTick = Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = cell.x, z = cell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Scream camera setup failed.");
            await RunUntil(
                () => (int)phase.GetValue(screamingAlbino) >= 12,
                2600,
                "AI acquisition"
            );
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    bool valid =
                        firstActiveTick >= startTick
                        && acquiredJob == "Sabotage"
                        && screamingAlbino.Spawned
                        && !screamingAlbino.Dead
                        && initialCooldown < 0;
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            stage = "native-acquisition",
                            passed = valid,
                            acquiredTick,
                            firstActiveTick,
                            acquiredJob,
                            initialCooldown,
                            ticksUntilActive = firstActiveTick - startTick,
                            cooldownTick = (int)cooldown.GetValue(screamingAlbino),
                            phase = (int)phase.GetValue(screamingAlbino),
                            albinoPosition = screamingAlbino.Position.ToString(),
                        }
                    );
                },
                cancellationToken
            );
            await Observe("initial", audioMode == "visible-muted");
            await Observe("revealed", true);
            await Observe("hidden-after-reveal", false);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    bool shouldPlay = audioMode != "hidden-muted";
                    bool valid = shouldPlay
                        ? samples.Count > 0
                            && samples.Values.All(s => Math.Abs(s.factor - 1f) < .001f)
                            && samples.Values.Any(s => s.played && s.peak > 0)
                        : samples.Count == 0;
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            stage = "native-scream-audio",
                            passed = valid,
                            shouldPlay,
                            audioMode,
                            samples = samples
                                .Values.Select(s => new
                                {
                                    s.factor,
                                    s.peak,
                                    s.played,
                                })
                                .ToArray(),
                        }
                    );
                },
                cancellationToken
            );
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var job = screamingAlbino.CurJob;
                    var driver = screamingAlbino.jobs.curDriver;
                    int before = (int)phase.GetValue(screamingAlbino);
                    // Exercise the exact damage-notification branch that reset the
                    // native scream. Do not alter health or force the scream itself.
                    screamingAlbino.jobs.Notify_DamageTaken(
                        new DamageInfo(DamageDefOf.Blunt, 0f, instigator: victims[0])
                    );
                    bool valid =
                        before > 0
                        && before == (int)phase.GetValue(screamingAlbino)
                        && screamingAlbino.CurJob == job
                        && screamingAlbino.jobs.curDriver == driver;
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            stage = "damage-job-continuation",
                            passed = valid,
                            phaseBefore = before,
                            phaseAfter = (int)phase.GetValue(screamingAlbino),
                            sameJob = screamingAlbino.CurJob == job,
                            sameDriver = screamingAlbino.jobs.curDriver == driver,
                        }
                    );
                },
                cancellationToken
            );
            await RunUntil(
                () => (int)phase.GetValue(screamingAlbino) == -1 && maximumPhase >= 390,
                1200,
                "scream lifetime"
            );
            await ctx.Game.FramesAsync(frames, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    bool valid =
                        screamingAlbino.Spawned
                        && !screamingAlbino.Dead
                        && (int)phase.GetValue(screamingAlbino) == -1
                        && maximumAffected > 0
                        && nativeVictimVomitFrames > 0
                        && nativeVictimStunFrames > 0
                        && (int)cooldown.GetValue(screamingAlbino) > Find.TickManager.TicksGame
                        && bubbles.Count > 0
                        && bubbles.All(b =>
                            b.Destroyed
                            && !b.Spawned
                            && !map.dynamicDrawManager.DrawThings.Contains(b)
                        )
                        && map.dynamicDrawManager.DrawThings.Count(t => t == screamingAlbino) == 1
                        && !Visibility.IsVisible(screamingAlbino);
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            stage = "native-expiry",
                            passed = valid,
                            maximumPhase,
                            maximumAffected,
                            nativeVictimVomitFrames,
                            nativeVictimStunFrames,
                            tick = Find.TickManager.TicksGame,
                            cooldownTick = (int)cooldown.GetValue(screamingAlbino),
                            bubbleCount = bubbles.Count,
                            bubblesExpired = bubbles.All(b => b.Destroyed),
                            albinoRegistration = map.dynamicDrawManager.DrawThings.Count(t =>
                                t == screamingAlbino
                            ),
                            victims = victims
                                .Select(p => new
                                {
                                    p.ThingID,
                                    p.Dead,
                                    p.Downed,
                                    job = p.CurJobDef?.defName,
                                })
                                .ToArray(),
                        }
                    );
                },
                cancellationToken
            );
            return new
            {
                passed,
                audioMode,
                normalPlayback = true,
                forcedScreamState = false,
                shortenedCooldown = false,
                startedAlbinoJob = false,
                stagedRoomAndHeldVictims = true,
                naturalVictimCombat = false,
                damageNotificationControl = true,
                startTick,
                rows,
                resets = screamResets.ToArray(),
            };
        }
        catch (Exception error)
        {
            return new
            {
                passed = false,
                success = false,
                failure = error.Message,
                audioMode,
                rows,
                maximumPhase,
                maximumAffected,
                resets = screamResets.ToArray(),
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        if (addedSight)
                            SetSight(false);
                        // Retired samples can retain a pooled source now owned by
                        // another sound. Stop only our samples still playing.
                        foreach (var sample in Find.SoundRoot.oneShotManager.PlayingOneShots)
                            if (samples.ContainsKey(sample))
                                sample.source?.Stop();
                        foreach (
                            var thing in owned.Concat(bubbles).Where(t => !t.Destroyed).Distinct()
                        )
                            thing.Destroy();
                        screamingAlbino = null;
                        screamMaterials.Clear();
                        observedScreamPhase = null;
                        screamResets.Clear();
                        screamVictims.Clear();
                        FogSettings.OnlyOutsideColony = oldBypass;
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                    },
                    CancellationToken.None
                );
                var reload = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!reload.Succeeded())
                    throw new InvalidOperationException("Scream fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }
        async Task RunUntil(Func<bool> done, int maximumTicks, string stage)
        {
            int began = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    return Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            var wait = await ctx.Game.RunUntilAsync(
                () =>
                {
                    Collect();
                    bool complete =
                        done()
                        || screamingAlbino.Destroyed
                        || screamingAlbino.Dead
                        || Find.TickManager.TicksGame - began >= maximumTicks;
                    if (complete)
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    return complete;
                },
                new RimBridgeWaitOptions { TimeoutMs = 65000 },
                cancellationToken
            );
            bool completed = await ctx.MainThread.InvokeAsync(done, cancellationToken);
            if (!wait.Success || !completed)
                throw new InvalidOperationException(
                    stage
                        + " did not complete: "
                        + wait.Message
                        + "; phase="
                        + phase.GetValue(screamingAlbino)
                        + "; maxPhase="
                        + maximumPhase
                        + "; affected="
                        + maximumAffected
                        + "; dead="
                        + screamingAlbino.Dead
                        + "; destroyed="
                        + screamingAlbino.Destroyed
                        + "; job="
                        + screamingAlbino.CurJobDef?.defName
                        + "; ticks="
                        + (Find.TickManager.TicksGame - began)
                );
        }
        void Collect()
        {
            int value = (int)phase.GetValue(screamingAlbino);
            if (screamingAlbino.CurJobDef?.defName == "Sabotage" && acquiredTick < 0)
            {
                acquiredTick = Find.TickManager.TicksGame;
                acquiredJob = screamingAlbino.CurJobDef.defName;
            }
            if (value > 0 && firstActiveTick < 0)
                firstActiveTick = Find.TickManager.TicksGame;
            maximumPhase = Math.Max(maximumPhase, value);
            maximumAffected = Math.Max(maximumAffected, (int)affected.GetValue(screamingAlbino));
            if (victims.Any(p => p.CurJobDef == JobDefOf.Vomit))
                nativeVictimVomitFrames++;
            if (victims.Any(p => p.stances.stunner.Stunned))
                nativeVictimStunFrames++;
            foreach (
                var mote in map
                    .dynamicDrawManager.DrawThings.OfType<Mote>()
                    .Where(m =>
                        m.def.defName == "ZombieThought" && m.link1.Target.Thing == screamingAlbino
                    )
            )
                bubbles.Add(mote);
            foreach (
                var sample in Find.SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                    s.subDef.parentDef.defName == "Scream"
                )
            )
            {
                samples.TryGetValue(sample, out var previous);
                samples[sample] = (
                    sample.info.volumeFactor,
                    Math.Max(previous.peak, sample.source?.volume ?? 0),
                    previous.played || sample.source?.isPlaying == true
                );
            }
        }
        async Task Observe(string stage, bool visible)
        {
            int tick = await ctx.MainThread.InvokeAsync(
                () =>
                {
                    SetSight(visible);
                    screamMeshes = screamBubbleDraws = 0;
                    return Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(frames, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Collect();
                    var bubble = bubbles.Single(b => !b.Destroyed && b.Spawned);
                    bool valid =
                        Visibility.IsVisible(screamingAlbino) == visible
                        && (
                            visible
                                ? screamMeshes > 0 && screamBubbleDraws > 0
                                : screamMeshes == 0 && screamBubbleDraws == 0
                        )
                        && bubble.Alpha > 0
                        && map.dynamicDrawManager.DrawThings.Count(t => t == bubble) == 1
                        && map.dynamicDrawManager.DrawThings.Count(t => t == screamingAlbino) == 1
                        && Find.TickManager.TicksGame == tick;
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            stage,
                            passed = valid,
                            visible,
                            screamMeshes,
                            screamBubbleDraws,
                            bubbleAge = bubble.AgeSecs,
                            bubbleAlpha = bubble.Alpha,
                            phase = (int)phase.GetValue(screamingAlbino),
                            tick,
                        }
                    );
                },
                cancellationToken
            );
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight)
                return;
            foreach (var c in room)
            {
                int index = map.cellIndices.CellToIndex(c);
                if (visible)
                    fog.IncrementSeen(Faction.OfPlayer, index);
                else
                    fog.DecrementSeen(Faction.OfPlayer, index);
            }
            addedSight = visible;
        }
    }

    private static void KeepScreamFixtureAlive(Pawn __instance, ref float threatLevel)
    {
        if (__instance == screamingAlbino && threatLevel <= 0)
            threatLevel = 1f;
    }

    private static void ObserveScreamMesh(Material mat)
    {
        if (screamingAlbino != null && screamMaterials.Contains(mat))
            screamMeshes++;
    }

    private static void ObserveScreamBubble(RimWorld.MoteBubble __instance, bool __runOriginal)
    {
        if (__runOriginal && __instance.link1.Target.Thing == screamingAlbino)
            screamBubbleDraws++;
    }

    private static bool HoldScreamVictimsWaiting(Pawn ___pawn) => !screamVictims.Contains(___pawn);

    private static void ObserveScreamReset(JobDriver __instance, bool resetScream)
    {
        if (
            __instance.pawn != screamingAlbino
            || observedScreamPhase == null
            || screamResets.Count >= 8
        )
            return;
        int value = (int)observedScreamPhase.GetValue(screamingAlbino);
        if (value < 0)
            return;
        screamResets.Add(
            new
            {
                phase = value,
                resetScream,
                tick = Find.TickManager.TicksGame,
                stack = Environment.StackTrace.Split('\n').Take(18).ToArray(),
            }
        );
    }
}
