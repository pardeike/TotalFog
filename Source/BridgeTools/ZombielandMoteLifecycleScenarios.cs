using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed partial class ZombielandEffectsScenarios
{
    [Tool(
        "totalfog/zombieland_mote_lifecycle",
        Description = "Use production block/bump motes, native tick aging and hidden/visible draw controls. Optional meleeBlock starts a real native melee attack with an explicitly selected ZombieBite verb, a capable level-twenty defender and scoped safeMeleeLimit two. Check blocked damage, the attached bubble and hidden Smash samples. Move the defender with a real Goto job; verify native expiry/deregistration. Does not force ages or call TimeInterval. Restores scoped timers/options/probes and reloads the original named fixture. Does not establish automatic melee job/verb selection or wall-threshold initiation."
    )]
    public static async Task<object> MoteLifecycle(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79,
        int frames = 12,
        bool meleeBlock = false
    )
    {
        if (frames < 6 || frames > 30)
            throw new ArgumentException("Use 6..30 frames per phase.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.mote-lifecycle-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony,
            addedSight = false,
            passed = true;
        bool oldMute = FogSettings.MuteHiddenSounds;
        Map map = null;
        MapVisibility fog = null;
        Pawn attacker = null,
            defender = null;
        float[] bumpTimers = null,
            oldTimers = null;
        var cell = new IntVec3(x, 0, z);
        var area = new CellRect(x - 2, z - 2, 11, 5);
        var rows = new List<object>();
        var motion = new List<object>();
        var motes = new List<Mote>();
        var meleeOptions = new List<(object group, int limit)>();
        object meleeInitiation = null,
            originalValues = null;
        System.Reflection.FieldInfo values = null,
            limit = null;
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
                            new CellRect(candidate.x - 2, candidate.z - 2, 11, 5).All(c =>
                                c.InBounds(map)
                                && !map.fogGrid.IsFogged(c)
                                && !fog.IsShown(Faction.OfPlayer, c)
                            )
                            && candidate.Standable(map)
                            && Enumerable
                                .Range(0, 5)
                                .All(i =>
                                    (candidate + IntVec3.North + IntVec3.East * i).Standable(map)
                                )
                        );
                    if (!cell.IsValid || cell == IntVec3.Zero)
                        throw new InvalidOperationException(
                            "No explored remote walking strip exists near the requested cell."
                        );
                    area = new CellRect(cell.x - 2, cell.z - 2, 11, 5);
                    var tools = AccessTools.TypeByName("ZombieLand.Tools");
                    bumpTimers = (float[])AccessTools.Field(tools, "nextBumps").GetValue(null);
                    oldTimers = (float[])bumpTimers.Clone();
                    attacker = (Pawn)
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
                    if (attacker == null)
                        throw new InvalidOperationException("Mote attacker spawn failed.");
                    var wait = JobMaker.MakeJob(JobDefOf.Wait);
                    wait.expiryInterval = 1000;
                    attacker.jobs.StartJob(wait, JobCondition.InterruptForced);
                    Rand.PushState(8127);
                    try
                    {
                        defender = PawnGenerator.GeneratePawn(
                            DefDatabase<PawnKindDef>.GetNamed("Colonist"),
                            Faction.OfAncients
                        );
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    GenSpawn.Spawn(defender, cell + IntVec3.North, map);
                    defender.jobs.StartJob(
                        JobMaker.MakeJob(JobDefOf.Wait),
                        JobCondition.InterruptForced
                    );
                    if (meleeBlock)
                    {
                        if (
                            defender.WorkTagIsDisabled(WorkTags.Violent)
                            || !defender.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
                        )
                            throw new InvalidOperationException(
                                "The seeded defender cannot perform smart melee."
                            );
                        defender.skills.GetSkill(SkillDefOf.Melee).Notify_SkillDisablesChanged();
                        defender.skills.GetSkill(SkillDefOf.Melee).Level = 20;
                        var defenderVerb = defender.meleeVerbs.TryGetMeleeVerb(attacker);
                        if (defenderVerb?.Available() != true)
                            throw new InvalidOperationException(
                                "The defender has no available melee verb."
                            );
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
                        {
                            meleeOptions.Add((group, (int)limit.GetValue(group)));
                            limit.SetValue(group, 2);
                        }
                        FogSettings.MuteHiddenSounds = true;
                    }
                    harmony.Patch(
                        AccessTools.Method(attacker.GetType(), "CustomTick"),
                        prefix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(SuppressMobileFixtureCleanup)
                        )
                    );
                    harmony.Patch(
                        AccessTools.DeclaredMethod(typeof(Mote), "DrawAt"),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveBlockDraw)
                        )
                    );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = cell.x + 2, z = cell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Mote camera setup failed.");
            foreach (string kind in new[] { "Mote_Block", "BumpSmall", "BumpMedium", "BumpLarge" })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        SetSight(false);
                        var before = map.dynamicDrawManager.DrawThings.ToHashSet();
                        var tools = AccessTools.TypeByName("ZombieLand.Tools");
                        if (kind == "Mote_Block")
                        {
                            if (meleeBlock)
                            {
                                var bite = attacker
                                    .meleeVerbs.GetUpdatedAvailableVerbsList(false)
                                    .Select(e => e.verb)
                                    .First(v => v.GetDamageDef()?.defName == "ZombieBite");
                                float injuryBefore = defender
                                    .health.hediffSet.hediffs.OfType<Hediff_Injury>()
                                    .Sum(h => h.Severity);
                                var soundsBefore =
                                    Find.SoundRoot.oneShotManager.PlayingOneShots.ToHashSet();
                                bool attackStarted = attacker.meleeVerbs.TryMeleeAttack(
                                    defender,
                                    bite
                                );
                                float injuryAfter = defender
                                    .health.hediffSet.hediffs.OfType<Hediff_Injury>()
                                    .Sum(h => h.Severity);
                                int smashSounds =
                                    Find.SoundRoot.oneShotManager.PlayingOneShots.Count(s =>
                                        !soundsBefore.Contains(s)
                                        && s.subDef.parentDef.defName == "Smash"
                                    );
                                bool valid =
                                    attackStarted
                                    && injuryAfter == injuryBefore
                                    && !defender.Dead
                                    && !defender.Downed
                                    && (int)limit.GetValue(values.GetValue(null)) == 2
                                    && smashSounds == 0;
                                passed &= valid;
                                meleeInitiation = new
                                {
                                    passed = valid,
                                    nativeMeleeAttack = true,
                                    explicitlySelectedBite = true,
                                    automaticJobSelection = false,
                                    damageDef = bite.GetDamageDef().defName,
                                    attackStarted,
                                    injuryBefore,
                                    injuryAfter,
                                    safeMeleeLimit = 2,
                                    defenderMeleeLevel = defender
                                        .skills.GetSkill(SkillDefOf.Melee)
                                        .Level,
                                    hiddenSmashSamples = smashSounds,
                                };
                            }
                            else
                                AccessTools
                                    .Method(tools, "CastBlockBubble")
                                    .Invoke(null, new object[] { attacker, defender });
                        }
                        else
                        {
                            int index =
                                kind == "BumpSmall" ? 0
                                : kind == "BumpMedium" ? 1
                                : 2;
                            bumpTimers[index] = 0;
                            AccessTools
                                .Method(tools, "CastBumpMote")
                                .Invoke(null, new object[] { map, cell.ToVector3Shifted(), index });
                        }
                        blockMote = (Mote)
                            map.dynamicDrawManager.DrawThings.Single(t =>
                                t.def.defName == kind && !before.Contains(t)
                            );
                        motes.Add(blockMote);
                        if (blockMote.def.mote.realTime || blockMote.def.mote.needsMaintenance)
                            throw new InvalidOperationException(
                                "This probe requires the actual tick-aged, non-maintained Zombieland mote definitions."
                            );
                        if (kind == "Mote_Block" && blockMote.link1.Target.Thing != defender)
                            throw new InvalidOperationException(
                                "Block mote must attach to the native defender."
                            );
                    },
                    cancellationToken
                );
                await Step(7);
                await Observe(kind, "initial-hidden", false, false);
                await Observe(kind, "visible", true, false);
                Vector3 beforePosition = Vector3.zero;
                float beforeRotation = 0;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        SetSight(false);
                        beforePosition = blockMote.exactPosition;
                        beforeRotation = blockMote.exactRotation;
                        if (kind == "Mote_Block")
                        {
                            var job = JobMaker.MakeJob(
                                JobDefOf.Goto,
                                defender.Position + IntVec3.East * 4
                            );
                            job.locomotionUrgency = LocomotionUrgency.Sprint;
                            defender.jobs.StartJob(job, JobCondition.InterruptForced);
                        }
                    },
                    cancellationToken
                );
                if (kind == "Mote_Block")
                {
                    var moved = await ctx.Game.RunForTicksAsync(
                        16,
                        new RimBridgeRunTicksOptions { TimeoutMs = 30000 },
                        cancellationToken
                    );
                    if (!moved.Success)
                        throw new InvalidOperationException(
                            "Normal defender walking did not complete: " + moved.Message
                        );
                }
                else
                    await Step(8);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool changed =
                            kind == "Mote_Block"
                                ? Vector2.Distance(
                                    new Vector2(beforePosition.x, beforePosition.z),
                                    new Vector2(
                                        blockMote.exactPosition.x,
                                        blockMote.exactPosition.z
                                    )
                                ) > .05f
                                    && Vector2.Distance(
                                        new Vector2(
                                            blockMote.exactPosition.x,
                                            blockMote.exactPosition.z
                                        ),
                                        new Vector2(
                                            blockMote.link1.LastDrawPos.x,
                                            blockMote.link1.LastDrawPos.z
                                        )
                                            + new Vector2(
                                                blockMote.def.mote.attachedDrawOffset.x,
                                                blockMote.def.mote.attachedDrawOffset.z
                                            )
                                    ) < .001f
                                : Math.Abs(beforeRotation - blockMote.exactRotation) > .01f;
                        passed &= changed;
                        motion.Add(
                            new
                            {
                                kind,
                                passed = changed,
                                nativeGoto = kind == "Mote_Block",
                                before = beforePosition.ToString(),
                                after = blockMote.exactPosition.ToString(),
                                beforeRotation,
                                afterRotation = blockMote.exactRotation,
                                age = blockMote.AgeSecs,
                            }
                        );
                    },
                    cancellationToken
                );
                await Observe(kind, "moved-hidden", false, false);
                await Observe(kind, "revealed", true, false);
                await Observe(kind, "hidden-again", false, false);
                await Step(60);
                await Observe(kind, "expired", false, true);
            }
            return new
            {
                passed,
                meleeInitiation,
                directMoteTick = false,
                forcedAge = false,
                fixtureCell = cell.ToString(),
                rows,
                motion,
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
                        foreach (var mote in motes.Where(m => !m.Destroyed))
                            mote.Destroy();
                        if (attacker != null && !attacker.Destroyed)
                            attacker.Destroy();
                        if (defender != null && !defender.Destroyed)
                            defender.Destroy();
                        if (addedSight)
                            SetSight(false);
                        if (bumpTimers != null && oldTimers != null)
                            Array.Copy(oldTimers, bumpTimers, oldTimers.Length);
                        foreach (var option in meleeOptions)
                            limit.SetValue(option.group, option.limit);
                        if (originalValues != null)
                            values.SetValue(null, originalValues);
                        FogSettings.MuteHiddenSounds = oldMute;
                        blockMote = null;
                        FogSettings.OnlyOutsideColony = oldBypass;
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
                    throw new InvalidOperationException("Mote fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }
        async Task Step(int ticks)
        {
            var result = await ctx.Game.StepTicksAsync(
                ticks,
                new RimBridgeTickOptions { TimeoutMs = 30000 },
                cancellationToken
            );
            if (!result.Success)
                throw new InvalidOperationException(
                    "Mote native stepping failed: " + result.Message
                );
        }
        async Task Observe(string kind, string stage, bool visible, bool expired)
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    SetSight(visible);
                    blockDraws = 0;
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(frames, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    int registrations = map.dynamicDrawManager.DrawThings.Count(t =>
                        t == blockMote
                    );
                    bool valid = expired
                        ? blockMote.Destroyed
                            && !blockMote.Spawned
                            && registrations == 0
                            && blockDraws == 0
                        : blockMote.Spawned
                            && !blockMote.Destroyed
                            && registrations == 1
                            && blockMote.Alpha > 0
                            && Visibility.IsVisible(map, blockMote.DrawPos.ToIntVec3()) == visible
                            && (visible ? blockDraws > 0 : blockDraws == 0);
                    passed &= valid;
                    rows.Add(
                        new
                        {
                            kind,
                            stage,
                            passed = valid,
                            visible,
                            expired,
                            blockMote.Destroyed,
                            blockMote.Spawned,
                            registrations,
                            drawCalls = blockDraws,
                            age = blockMote.AgeSecs,
                            alpha = blockMote.Alpha,
                            tick = Find.TickManager.TicksGame,
                            drawnCell = blockMote.DrawPos.ToIntVec3().ToString(),
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
            foreach (var c in area)
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
}
