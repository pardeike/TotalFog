using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using TotalFog.Combat;
using Verse;
using Verse.AI;
using Xunit;

namespace TotalFog.Tests;

public sealed class UnseenFireTests : IDisposable
{
    private readonly bool previousSmart = FogSettings.AISmart;
    private readonly int previousTick = Find.TickManager.TicksGame;
    private readonly Map map = new() { Size = new(80, 80) };
    private readonly Pawn victim;
    private readonly Pawn shooter;
    private readonly Verb weapon;

    public UnseenFireTests()
    {
        FogSettings.AISmart = true;
        Find.TickManager.TicksGame = 100;
        map.Fog.Initialized = true;
        map.Fog.InSight = new bool[80 * 80];
        victim = new ModdedEnemy
        {
            Map = map,
            Faction = new() { loadID = 2 },
            PositionHeld = new(10, 10),
        };
        shooter = new Pawn
        {
            Map = map,
            Faction = Faction.OfPlayer,
            PositionHeld = new(41, 12),
        };
        weapon = new Verb { caster = shooter, CurrentTarget = new(victim) };
        weapon.verbProps.soundCast = new();
    }

    public void Dispose()
    {
        FogSettings.AISmart = previousSmart;
        Find.TickManager.TicksGame = previousTick;
    }

    [Fact]
    public void Modded_enemy_investigates_a_noisy_shot_without_revealing_or_tracking_shooter()
    {
        Fire();
        shooter.PositionHeld = new(65, 60);
        var job = Investigate();
        Assert.NotNull(job);
        Assert.Same(JobDefOf.Goto, job.def);
        Assert.False(job.targetA.HasThing);
        Assert.True(job.targetA.Cell.InHorDistOf(new(41, 12), 4));
        Assert.NotEqual(new IntVec3(41, 12), job.targetA.Cell);
        Assert.InRange(job.expiryInterval, 1, UnseenFire.MemoryTicks);
        Assert.True(job.checkOverrideOnExpire);
        Assert.False(Visibility.AllowsTarget(victim, shooter.Position));
        Assert.All(map.Fog.InSight, shown => Assert.False(shown));
        Assert.All(map.Fog.knownCells, known => Assert.False(known));
        Assert.Null(victim.mindState.enemyTarget);
    }

    [Fact]
    public void Silent_and_melee_weapons_do_not_create_a_reaction()
    {
        weapon.verbProps.soundCast = null;
        Fire();
        Assert.Null(Investigate());
        weapon.verbProps.soundCast = new();
        weapon.verbProps.IsMeleeAttack = true;
        Fire();
        Assert.Null(Investigate());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tail_and_beam_sounds_also_identify_noisy_modded_weapons(bool beam)
    {
        weapon.verbProps.soundCast = null;
        if (beam)
            weapon.verbProps.soundCastBeam = new();
        else
            weapon.verbProps.soundCastTail = new();
        Fire();
        Assert.NotNull(Investigate());
    }

    [Fact]
    public void Existing_combat_jobs_and_acquired_targets_keep_priority()
    {
        Fire();
        var existing = new Job();
        Assert.Same(existing, Investigate(existing));
        Assert.Equal(0, victim.ReachabilityQueries);
        victim.mindState.enemyTarget = shooter;
        Assert.Null(Investigate());
        victim.mindState.enemyTarget = null;
        Assert.NotNull(Investigate());
    }

    [Fact]
    public void Memory_expires_and_does_not_survive_map_or_faction_changes()
    {
        Fire();
        Assert.NotNull(Investigate());
        Find.TickManager.TicksGame += UnseenFire.MemoryTicks;
        Assert.Null(Investigate());
        Fire();
        victim.Map = new() { Size = new(80, 80) };
        Assert.Null(Investigate());
        victim.Map = map;
        Fire();
        victim.Faction = new() { loadID = 3 };
        Assert.Null(Investigate());
    }

    [Fact]
    public void Reaching_the_heard_area_ends_investigation_instead_of_following_the_shooter()
    {
        Fire();
        var job = Investigate();
        Assert.NotNull(job);
        victim.PositionHeld = job.targetA.Cell;
        shooter.PositionHeld = new(65, 65);
        Assert.Null(Investigate());
        victim.PositionHeld = new(10, 10);
        Assert.Null(Investigate());
    }

    [Fact]
    public void Nonhuman_modded_pawns_are_not_excluded_by_type_or_race_name()
    {
        victim.RaceProps.Humanlike = false;
        Fire();
        Assert.NotNull(Investigate());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Firing_hook_runs_only_after_a_successful_shot(bool success)
    {
        var method = new DynamicMethod(
            "Fire",
            typeof(void),
            new[] { typeof(Verb) },
            typeof(Verb),
            true
        );
        var il = method.GetILGenerator();
        il.DeclareLocal(typeof(LocalTargetInfo));
        var end = il.DefineLabel();
        var codes = NativeShotBody(end);
        codes = UnseenFirePatches.ShotTranspiler(codes).ToList();
        foreach (var code in codes)
        {
            foreach (var label in code.labels)
                il.MarkLabel(label);
            switch (code.operand)
            {
                case System.Reflection.FieldInfo field:
                    il.Emit(code.opcode, field);
                    break;
                case System.Reflection.MethodInfo call:
                    il.Emit(code.opcode, call);
                    break;
                case Label label:
                    il.Emit(code.opcode, label);
                    break;
                default:
                    il.Emit(code.opcode);
                    break;
            }
        }
        weapon.SuccessfulShot = success;
        method.CreateDelegate<Action<Verb>>()(weapon);
        if (success)
            Assert.NotNull(Investigate());
        else
            Assert.Null(Investigate());
    }

    [Fact]
    public void Unsupported_firing_instructions_remain_unchanged()
    {
        var codes = NativeShotBody(default);
        codes[2] = new(OpCodes.Stloc_1);
        Assert.Equal(codes, UnseenFirePatches.ShotTranspiler(codes));
        codes = NativeShotBody(default);
        codes[5] = new(OpCodes.Brtrue, default(Label));
        Assert.Equal(codes, UnseenFirePatches.ShotTranspiler(codes));
    }

    [Fact]
    public void Modded_guard_overrides_limit_the_fallback_combat_job()
    {
        Fire();
        UnseenFirePatches.Install(new Harmony());
        Job job = null;
        UnseenFirePatches.CombatJobPostfix(new ModdedGuard(), victim, ref job);
        Assert.Null(job);
    }

    private static List<CodeInstruction> NativeShotBody(Label end) =>
        new()
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, AccessTools.Field(typeof(Verb), "currentTarget")),
            new(OpCodes.Stloc_0),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Callvirt, AccessTools.DeclaredMethod(typeof(Verb), "TryCastShot")),
            new(OpCodes.Brfalse, end),
            new(OpCodes.Ret),
            new(OpCodes.Ret) { labels = new() { end } },
        };

    private sealed class ModdedGuard : JobGiver_AIFightEnemy
    {
        protected override IntVec3 GetFlagPosition(Pawn pawn) => pawn.Position;

        protected override float GetFlagRadius(Pawn pawn) => 3;
    }

    [Fact]
    public void Investigation_respects_guard_radius_and_reachable_standing_cells()
    {
        Fire();
        Assert.Null(UnseenFire.InvestigationJob(victim, null, victim.Position, 3));
        Fire();
        var first = Investigate();
        Assert.NotNull(first);
        map.Blocked.Add(first.targetA.Cell);
        var next = Investigate();
        Assert.NotNull(next);
        Assert.NotEqual(first.targetA.Cell, next.targetA.Cell);
        Assert.DoesNotContain(next.targetA.Cell, map.Blocked);
    }

    [Fact]
    public void Explicit_duty_radius_also_limits_nodes_without_flag_overrides()
    {
        victim.mindState.duty = new() { focus = new(victim.Position), radius = 3 };
        Fire();
        Assert.Null(Investigate());
        victim.mindState.duty.radius = -1;
        Fire();
        Assert.NotNull(Investigate());
    }

    [Fact]
    public void No_job_is_created_for_unreachable_or_reserved_destinations()
    {
        Fire();
        for (int x = 35; x <= 48; x++)
        for (int z = 5; z <= 20; z++)
            map.Unreachable.Add(new(x, z));
        Assert.Null(Investigate());
        map.Unreachable.Clear();
        Fire();
        for (int x = 35; x <= 48; x++)
        for (int z = 5; z <= 20; z++)
            map.pawnDestinationReservationManager.Reserved.Add(new(x, z));
        Assert.Null(Investigate());
    }

    [Theory]
    [InlineData("disabled")]
    [InlineData("player")]
    [InlineData("friendly")]
    [InlineData("deaf")]
    [InlineData("pacifist")]
    [InlineData("downed")]
    [InlineData("mental")]
    [InlineData("prisoner")]
    [InlineData("drafted")]
    [InlineData("sleeping")]
    [InlineData("visible")]
    public void Ordinary_ai_and_ineligible_pawns_remain_unchanged(string reason)
    {
        switch (reason)
        {
            case "disabled":
                FogSettings.AISmart = false;
                break;
            case "player":
                victim.Faction = Faction.OfPlayer;
                break;
            case "friendly":
                victim.Hostile = false;
                break;
            case "deaf":
                victim.health.capacities.Hearing = 0;
                break;
            case "pacifist":
                victim.Pacifist = true;
                break;
            case "downed":
                victim.Downed = true;
                break;
            case "mental":
                victim.InMentalState = true;
                break;
            case "prisoner":
                victim.IsPrisoner = true;
                break;
            case "drafted":
                victim.Drafted = true;
                break;
            case "sleeping":
                victim.Sleeping = true;
                break;
            case "visible":
                map.Fog.InSight[shooter.Position.z * 80 + shooter.Position.x] = true;
                break;
        }
        Fire();
        Assert.Null(Investigate());
        Assert.Equal(0, victim.ReachabilityQueries);
    }

    private void Fire() => UnseenFire.ShotFired(weapon, new(victim));

    private Job Investigate(Job existing = null) =>
        UnseenFire.InvestigationJob(victim, existing, IntVec3.Invalid, float.MaxValue);

    private sealed class ModdedEnemy : Pawn { }
}
