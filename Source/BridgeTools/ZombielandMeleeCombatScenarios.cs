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
using TotalFog.Core;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace TotalFog.BridgeTools;

public sealed partial class AudioScenarios
{
    private static Pawn combatAttacker,
        combatDefender;
    private static readonly List<Mote> combatBubbles = new();
    private static readonly List<object> combatShots = new();
    private static readonly Dictionary<SampleOneShot, float> combatExpectedFactors = new();
    private static int combatBlocks,
        combatOriginalShots,
        combatOriginalDamageShots,
        combatBubbleDraws;
    private static bool combatBlockDamagePreserved;

    [Tool(
        "totalfog/zombieland_melee_combat",
        Description = "Let real Stumble jobs find a staged non-player defender and start native AttackMelee jobs with automatic verb selection. Require repeated attacks and a real bite parry in six normal-playback controls. A seventh hidden, smart-melee-disabled control requires native unblocked attacks and actual injury. Observe native bubble drawing/expiry and positional Smash one-shots across fog mute/hearing/visible/off-camera cases. SafeMeleeLimit, a capable level-twenty fixture defender, audio preferences and zero-threat cleanup are scoped. Restores settings/probes and reloads the unchanged named save."
    )]
    public static async Task<object> ZombielandMeleeCombat(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79,
        int maximumTicks = 900
    )
    {
        if (maximumTicks < 600 || maximumTicks > 1800)
            throw new ArgumentException("Use 600..1800 ticks per combat control.");
        await ambientGate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.melee-combat-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony,
            oldMute = FogSettings.MuteHiddenSounds,
            oldHearing = FogSettings.DoAudioCheck;
        int oldRange = FogSettings.AudioSourceRange;
        float oldMuffling = FogSettings.VolumeMufflingModifier,
            oldAmbient = Prefs.VolumeAmbient;
        Map map = null;
        MapVisibility fog = null;
        var cell = new IntVec3(x, 0, z);
        var area = new CellRect();
        bool addedSight = false,
            passed = true;
        object originalValues = null;
        FieldInfo values = null,
            limit = null;
        var options = new List<(object group, int limit)>();
        var rows = new List<object>();
        var owned = new List<Thing>();
        var ownedSamples = new HashSet<SampleOneShot>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the base fixture first.");
                    fog = map.GetVisibility();
                    FogSettings.OnlyOutsideColony = false;
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    cell = GenRadial
                        .RadialCellsAround(cell, 25, true)
                        .FirstOrDefault(candidate =>
                            new CellRect(candidate.x - 2, candidate.z - 2, 5, 5).All(c =>
                                c.InBounds(map)
                                && !map.fogGrid.IsFogged(c)
                                && !fog.IsShown(Faction.OfPlayer, c)
                            )
                            && new[] { candidate, candidate + IntVec3.North }.All(c =>
                                c.Standable(map) && !c.GetThingList(map).OfType<Pawn>().Any()
                            )
                        );
                    if (!cell.IsValid || cell == IntVec3.Zero)
                        throw new InvalidOperationException(
                            "No remote adjacent combat pair exists."
                        );
                    area = new CellRect(cell.x - 2, cell.z - 2, 5, 5);
                    var settingsType = AccessTools.TypeByName("ZombieLand.ZombieSettings");
                    values = AccessTools.Field(settingsType, "Values");
                    originalValues = values.GetValue(null);
                    limit = AccessTools.Field(originalValues.GetType(), "safeMeleeLimit");
                    var groups = new List<object> { originalValues };
                    if (
                        AccessTools.Field(settingsType, "ValuesOverTime").GetValue(null)
                        is IEnumerable timeline
                    )
                        foreach (var frame in timeline)
                            if (
                                frame != null
                                && AccessTools.Field(frame.GetType(), "values").GetValue(frame)
                                    is object group
                            )
                                groups.Add(group);
                    foreach (var group in groups.Distinct())
                        options.Add((group, (int)limit.GetValue(group)));
                    FogSettings.AudioSourceRange = 500;
                    FogSettings.VolumeMufflingModifier = .5f;
                    Prefs.VolumeAmbient = Math.Max(oldAmbient, .5f);
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.Zombie"),
                            "CustomTick"
                        ),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            "SuppressMobileFixtureCleanup"
                        )
                    );
                    harmony.Patch(
                        AccessTools.Method(typeof(Verb_MeleeAttack), "TryCastShot"),
                        prefix: new HarmonyMethod(typeof(AudioScenarios), nameof(BeforeCombatShot))
                        {
                            priority = Priority.First,
                        },
                        postfix: new HarmonyMethod(typeof(AudioScenarios), nameof(AfterCombatShot))
                        {
                            priority = Priority.Last,
                        }
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            AccessTools.TypeByName("ZombieLand.Tools"),
                            "CastBlockBubble"
                        ),
                        postfix: new HarmonyMethod(
                            typeof(AudioScenarios),
                            nameof(AfterCombatBubble)
                        )
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(Mote), "DrawAt"),
                        postfix: new HarmonyMethod(
                            typeof(AudioScenarios),
                            nameof(ObserveCombatBubbleDraw)
                        )
                    );
                },
                cancellationToken
            );
            foreach (
                string state in new[]
                {
                    "unfiltered-hidden",
                    "hidden-muted",
                    "visible-muted",
                    "hearing-hidden",
                    "hidden-again",
                    "off-camera-muted",
                    "smart-melee-disabled-hidden",
                }
            )
            {
                bool visible = state == "visible-muted",
                    far = state == "off-camera-muted";
                bool smartMelee = state != "smart-melee-disabled-hidden";
                float expectedFactor = 0;
                int startTick = 0;
                HashSet<SampleOneShot> beforeSamples = null;
                var sounds =
                    new Dictionary<SampleOneShot, (float factor, float peak, bool playing)>();
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        StopOwnedOneShots(ownedSamples);
                        DestroyOwned();
                        combatBubbles.Clear();
                        combatShots.Clear();
                        combatExpectedFactors.Clear();
                        combatBlocks = 0;
                        combatOriginalShots = 0;
                        combatOriginalDamageShots = 0;
                        combatBubbleDraws = 0;
                        combatBlockDamagePreserved = true;
                        SetSight(visible);
                        FogSettings.MuteHiddenSounds =
                            state
                                is "hidden-muted"
                                    or "visible-muted"
                                    or "hidden-again"
                                    or "off-camera-muted"
                                    or "smart-melee-disabled-hidden";
                        FogSettings.DoAudioCheck = state == "hearing-hidden";
                        foreach (
                            var group in options
                                .Select(o => o.group)
                                .Append(values.GetValue(null))
                                .Distinct()
                        )
                            limit.SetValue(group, smartMelee ? 2 : 0);
                        combatAttacker = (Pawn)
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
                                            "Normal"
                                        ),
                                        true,
                                    }
                                );
                        if (combatAttacker == null)
                            throw new InvalidOperationException("Combat zombie spawn failed.");
                        owned.Add(combatAttacker);
                        Rand.PushState(8127);
                        try
                        {
                            combatDefender = PawnGenerator.GeneratePawn(
                                DefDatabase<PawnKindDef>.GetNamed("Colonist"),
                                Faction.OfAncients
                            );
                        }
                        finally
                        {
                            Rand.PopState();
                        }
                        GenSpawn.Spawn(combatDefender, cell + IntVec3.North, map);
                        owned.Add(combatDefender);
                        foreach (
                            var injury in combatDefender
                                .health.hediffSet.hediffs.OfType<Hediff_Injury>()
                                .ToArray()
                        )
                            combatDefender.health.RemoveHediff(injury);
                        if (
                            combatDefender.WorkTagIsDisabled(WorkTags.Violent)
                            || !combatDefender.health.capacities.CapableOf(
                                PawnCapacityDefOf.Manipulation
                            )
                        )
                            throw new InvalidOperationException(
                                "The seeded defender cannot perform smart melee."
                            );
                        combatDefender
                            .skills.GetSkill(SkillDefOf.Melee)
                            .Notify_SkillDisablesChanged();
                        combatDefender.skills.GetSkill(SkillDefOf.Melee).Level = 20;
                        if (
                            combatDefender.meleeVerbs.TryGetMeleeVerb(combatAttacker)?.Available()
                            != true
                        )
                            throw new InvalidOperationException(
                                "The defender has no usable melee verb."
                            );
                        var wait = JobMaker.MakeJob(JobDefOf.Wait);
                        wait.expiryInterval = maximumTicks + 200;
                        combatDefender.jobs.StartJob(wait, JobCondition.InterruptForced);
                        combatAttacker.jobs.StartJob(
                            JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Stumble")),
                            JobCondition.InterruptForced
                        );
                        expectedFactor = SoundAudibility.GetAudibilityFactor(
                            new TargetInfo(combatDefender.Position, map)
                        );
                        beforeSamples = Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                        startTick = Find.TickManager.TicksGame;
                    },
                    cancellationToken
                );
                var camera = await ctx.Tools.CallAsync(
                    "rimworld/jump_camera_to_cell",
                    new { x = far ? 20 : cell.x, z = far ? 220 : cell.z },
                    cancellationToken: cancellationToken
                );
                if (!camera.Succeeded())
                    throw new InvalidOperationException("Combat camera setup failed.");
                await ctx.MainThread.InvokeAsync(
                    () => Find.TickManager.CurTimeSpeed = TimeSpeed.Normal,
                    cancellationToken
                );
                var run = await ctx.Game.RunUntilAsync(
                    () =>
                    {
                        ObserveSounds();
                        bool done =
                            combatDefender.Dead
                            || combatDefender.Downed
                            || combatAttacker.Destroyed
                            || combatShots.Count >= 3
                                && (smartMelee ? combatBlocks > 0 : combatOriginalDamageShots > 0)
                            || Find.TickManager.TicksGame - startTick >= maximumTicks;
                        if (done)
                            Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        return done;
                    },
                    new RimBridgeWaitOptions { TimeoutMs = 60000 },
                    cancellationToken
                );
                if (!run.Success)
                    throw new InvalidOperationException(
                        "Normal melee playback did not complete: " + run.Message
                    );
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        // A normal combat termination may remove the attacker.
                        // Preserve the failed control's state instead of issuing a
                        // follow-up job to a pawn whose trackers were cleared.
                        if (
                            combatAttacker?.Spawned == true
                            && combatAttacker.jobs != null
                            && !combatAttacker.Dead
                        )
                        {
                            var wait = JobMaker.MakeJob(JobDefOf.Wait);
                            wait.expiryInterval = 200;
                            combatAttacker.jobs.StartJob(wait, JobCondition.InterruptForced);
                        }
                    },
                    cancellationToken
                );
                await Run(12);
                for (int i = 0; i < 12; i++)
                {
                    await ctx.Game.FramesAsync(1, cancellationToken);
                    await ctx.MainThread.InvokeAsync(ObserveSounds, cancellationToken);
                }
                object row = null;
                bool valid = false;
                Mote[] observedBubbles = null;
                int observedShots = 0;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool soundValid =
                            expectedFactor == 0
                                ? sounds.Count == 0
                                : sounds.Count > 0
                                    && sounds.All(s =>
                                        combatExpectedFactors.TryGetValue(s.Key, out var expected)
                                        && Math.Abs(s.Value.factor - expected) < .001f
                                    )
                                    && sounds.Values.Any(s => s.playing && s.peak > 0);
                        valid =
                            combatShots.Count >= 3
                            && (
                                smartMelee
                                    ? combatBlocks > 0
                                    : combatBlocks == 0
                                        && combatOriginalShots == combatShots.Count
                                        && combatOriginalDamageShots > 0
                            )
                            && combatBlockDamagePreserved
                            && combatAttacker.Spawned
                            && !combatAttacker.Dead
                            && combatDefender.Spawned
                            && !combatDefender.Dead
                            && !combatDefender.Downed
                            && combatAttacker.Position == cell
                            && combatDefender.Position == cell + IntVec3.North
                            && (int)limit.GetValue(values.GetValue(null)) == (smartMelee ? 2 : 0)
                            && soundValid
                            && (visible ? combatBubbleDraws > 0 : combatBubbleDraws == 0);
                        row = new
                        {
                            state,
                            passedBeforeExpiry = valid,
                            automaticJobAndVerbSelection = true,
                            nativeTicks = Find.TickManager.TicksGame - startTick,
                            attackerSpawned = combatAttacker.Spawned,
                            attackerDestroyed = combatAttacker.Destroyed,
                            attackerHasJobs = combatAttacker.jobs != null,
                            attackerJob = combatAttacker.CurJobDef?.defName,
                            defenderSpawned = combatDefender.Spawned,
                            defenderDead = combatDefender.Dead,
                            defenderDowned = combatDefender.Downed,
                            defenderJob = combatDefender.CurJobDef?.defName,
                            shots = combatShots.ToArray(),
                            blocks = combatBlocks,
                            originalShots = combatOriginalShots,
                            originalDamageShots = combatOriginalDamageShots,
                            smartMelee,
                            combatBlockDamagePreserved,
                            bubbleDrawCalls = combatBubbleDraws,
                            expectedFactor,
                            soundValid,
                            sounds = sounds
                                .Select(s => new
                                {
                                    s.Value.factor,
                                    s.Value.peak,
                                    s.Value.playing,
                                    expectedAtShot = combatExpectedFactors.TryGetValue(
                                        s.Key,
                                        out var expected
                                    )
                                        ? (float?)expected
                                        : null,
                                })
                                .ToArray(),
                        };
                        observedBubbles = combatBubbles.ToArray();
                        observedShots = combatShots.Count;
                    },
                    cancellationToken
                );
                await Run(90);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        // Normal NPC job overrides may resume combat during the
                        // follow-up. Accept expiry of the observed bubbles only;
                        // record later births instead of treating them as old motes.
                        ObserveSounds();
                        bool expired = observedBubbles.All(m =>
                            m.Destroyed
                            && !m.Spawned
                            && !map.dynamicDrawManager.DrawThings.Contains(m)
                        );
                        passed &= valid && expired;
                        rows.Add(
                            new
                            {
                                passed = valid && expired,
                                expired,
                                result = row,
                                laterShots = combatShots.Count - observedShots,
                                laterBubbles = combatBubbles.Count - observedBubbles.Length,
                                observedBubbles = observedBubbles
                                    .Select(m => new
                                    {
                                        m.Destroyed,
                                        m.Spawned,
                                        age = m.AgeSecs,
                                        life = m.def.mote.Lifespan,
                                        registered = map.dynamicDrawManager.DrawThings.Contains(m),
                                    })
                                    .ToArray(),
                            }
                        );
                    },
                    cancellationToken
                );
                void ObserveSounds()
                {
                    foreach (
                        var sample in Find.SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                            !beforeSamples.Contains(s)
                            && s.subDef.parentDef.defName == "Smash"
                            && s.info.Maker.Map == map
                            && s.info.Maker.Cell == combatDefender.Position
                        )
                    )
                    {
                        ownedSamples.Add(sample);
                        sounds.TryGetValue(sample, out var old);
                        sounds[sample] = (
                            sample.info.volumeFactor,
                            Math.Max(old.peak, sample.source?.volume ?? 0),
                            old.playing || sample.source?.isPlaying == true
                        );
                    }
                }
            }
            return new
            {
                passed,
                normalPlayback = true,
                directlySelectedBites = false,
                directlyStartedAttackJobs = false,
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
                        harmony.UnpatchAll(harmony.Id);
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                        StopOwnedOneShots(ownedSamples);
                        DestroyOwned();
                        combatAttacker = null;
                        combatDefender = null;
                        combatBubbles.Clear();
                        combatShots.Clear();
                        combatExpectedFactors.Clear();
                        if (addedSight)
                            SetSight(false);
                        foreach (var option in options)
                            limit.SetValue(option.group, option.limit);
                        if (originalValues != null)
                            values.SetValue(null, originalValues);
                        FogSettings.OnlyOutsideColony = oldBypass;
                        FogSettings.MuteHiddenSounds = oldMute;
                        FogSettings.DoAudioCheck = oldHearing;
                        FogSettings.AudioSourceRange = oldRange;
                        FogSettings.VolumeMufflingModifier = oldMuffling;
                        Prefs.VolumeAmbient = oldAmbient;
                    },
                    CancellationToken.None
                );
                var restored = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!restored.Succeeded())
                    throw new InvalidOperationException("Combat fixture restoration failed.");
            }
            finally
            {
                ambientGate.Release();
            }
        }
        void DestroyOwned()
        {
            foreach (var mote in combatBubbles.Where(m => !m.Destroyed))
                mote.Destroy();
            foreach (var thing in owned.Where(t => !t.Destroyed))
                thing.Destroy();
            owned.Clear();
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight)
                return;
            foreach (var c in area)
            {
                int index = map.cellIndices.CellToIndex(c);
                if (visible)
                    fog.IncrementSeen(
                        Faction.OfPlayer,
                        fog.GetFactionShownCells(Faction.OfPlayer),
                        index
                    );
                else
                    fog.DecrementSeen(
                        Faction.OfPlayer,
                        fog.GetFactionShownCells(Faction.OfPlayer),
                        index
                    );
            }
            addedSight = visible;
        }
        async Task Run(int ticks)
        {
            var result = await ctx.Game.RunForTicksAsync(
                ticks,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000 },
                cancellationToken
            );
            if (!result.Success)
                throw new InvalidOperationException(
                    "Normal combat follow-up playback failed: " + result.Message
                );
        }
    }

    private static void BeforeCombatShot(
        Verb_MeleeAttack __instance,
        out (float injury, int bubbles) __state
    )
    {
        __state = (-1, 0);
        if (
            combatAttacker != null
            && combatDefender != null
            && __instance.CasterPawn == combatAttacker
            && (
                (LocalTargetInfo)
                    AccessTools.Field(typeof(Verb), "currentTarget").GetValue(__instance)
            ).Thing == combatDefender
        )
            __state = (
                combatDefender
                    .health.hediffSet.hediffs.OfType<Hediff_Injury>()
                    .Sum(h => h.Severity),
                combatBubbles.Count
            );
    }

    private static void AfterCombatShot(
        Verb_MeleeAttack __instance,
        bool __result,
        bool __runOriginal,
        (float injury, int bubbles) __state
    )
    {
        if (__state.injury < 0)
            return;
        float after = combatDefender
            .health.hediffSet.hediffs.OfType<Hediff_Injury>()
            .Sum(h => h.Severity);
        bool blocked = combatBubbles.Count > __state.bubbles;
        string damage = __instance.GetDamageDef()?.defName;
        if (blocked)
        {
            combatBlocks++;
            combatBlockDamagePreserved &=
                after == __state.injury && damage == "ZombieBite" && !__runOriginal && !__result;
        }
        if (__runOriginal)
        {
            combatOriginalShots++;
            if (after > __state.injury)
                combatOriginalDamageShots++;
        }
        combatShots.Add(
            new
            {
                tick = GenTicks.TicksGame,
                damage,
                blocked,
                ranOriginal = __runOriginal,
                result = __result,
                injuryBefore = __state.injury,
                injuryAfter = after,
                job = combatAttacker.CurJobDef?.defName,
            }
        );
        foreach (
            var sample in Find.SoundRoot.oneShotManager.PlayingOneShots.Where(s =>
                s.subDef.parentDef.defName == "Smash"
                && s.info.Maker.Map == combatDefender.Map
                && s.info.Maker.Cell == combatDefender.Position
                && !combatExpectedFactors.ContainsKey(s)
            )
        )
            combatExpectedFactors[sample] = SoundAudibility.GetAudibilityFactor(sample.info.Maker);
    }

    private static void AfterCombatBubble(Pawn attacker, Pawn defender)
    {
        if (attacker != combatAttacker || defender != combatDefender)
            return;
        var mote = combatDefender
            .Map.dynamicDrawManager.DrawThings.OfType<Mote>()
            .Single(m =>
                m.def.defName == "Mote_Block"
                && m.link1.Target.Thing == combatDefender
                && !combatBubbles.Contains(m)
            );
        combatBubbles.Add(mote);
    }

    private static void ObserveCombatBubbleDraw(Mote __instance, bool __runOriginal)
    {
        if (__runOriginal && combatBubbles.Contains(__instance))
            combatBubbleDraws++;
    }
}
