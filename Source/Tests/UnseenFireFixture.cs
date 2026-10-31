using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Verse
{
    public class SoundDef { }

    public partial class VerbProperties
    {
        public bool IsMeleeAttack;
        public SoundDef soundCast,
            soundCastTail,
            soundCastBeam;
    }

    public partial class Verb
    {
        protected LocalTargetInfo currentTarget;
        public bool SuccessfulShot = true;

        protected virtual bool TryCastShot() => SuccessfulShot;

        protected void TryCastNextBurstShot() { }
    }

    public partial class Pawn
    {
        public bool Downed,
            Drafted,
            InMentalState,
            IsPrisoner,
            Sleeping,
            Pacifist;
        public bool Hostile = true;
        public AI.Pawn_MindState mindState = new();
        public Pawn_HealthTracker health = new();
        public int ReachabilityQueries;

        public bool Awake() => !Sleeping;

        public bool WorkTagIsDisabled(WorkTags tag) => Pacifist;

        public bool HostileTo(Thing other) => Hostile && Faction != other.Faction;

        public bool CanReach(IntVec3 cell, AI.PathEndMode end, Danger danger)
        {
            ReachabilityQueries++;
            return !Map.Unreachable.Contains(cell);
        }
    }

    public class Pawn_HealthTracker
    {
        public PawnCapacitiesHandler capacities = new();
    }

    public class PawnCapacitiesHandler
    {
        public float Hearing = 1;

        public float GetLevel(PawnCapacityDef capacity) => Hearing;
    }

    public class PawnCapacityDef { }

    public enum WorkTags
    {
        Violent,
    }

    public enum Danger
    {
        Deadly,
    }

    public partial class Map
    {
        public HashSet<IntVec3> Blocked = new(),
            Unreachable = new();
        public PawnDestinationReservationManager pawnDestinationReservationManager = new();
    }

    public class PawnDestinationReservationManager
    {
        public HashSet<IntVec3> Reserved = new();

        public bool CanReserve(IntVec3 cell, Pawn pawn, bool drafted) => !Reserved.Contains(cell);
    }

    public static class GenRadial
    {
        public static IEnumerable<IntVec3> RadialCellsAround(
            IntVec3 center,
            float radius,
            bool useCenter
        )
        {
            if (useCenter)
                yield return center;
            for (int r = 1; r <= radius; r++)
            for (int x = -r; x <= r; x++)
            for (int z = -r; z <= r; z++)
                if (Math.Max(Math.Abs(x), Math.Abs(z)) == r && x * x + z * z <= radius * radius)
                    yield return center + new IntVec3(x, z);
        }
    }
}

namespace Verse.AI
{
    public class Pawn_MindState
    {
        public Verse.Thing enemyTarget;
        public PawnDuty duty;
    }

    public class PawnDuty
    {
        public Verse.LocalTargetInfo focus;
        public float radius = -1;
    }

    public enum PathEndMode
    {
        OnCell,
    }

    public class Job
    {
        public RimWorld.JobDef def;
        public Verse.LocalTargetInfo targetA;
        public int expiryInterval;
        public bool checkOverrideOnExpire;
    }

    public static class JobMaker
    {
        public static Job MakeJob(RimWorld.JobDef def, Verse.IntVec3 cell) =>
            new() { def = def, targetA = new(cell) };
    }
}

namespace RimWorld
{
    public abstract class JobGiver_AIFightEnemy
    {
        protected virtual Verse.IntVec3 GetFlagPosition(Verse.Pawn pawn) => Verse.IntVec3.Invalid;

        protected virtual float GetFlagRadius(Verse.Pawn pawn) => 999999;

        protected virtual Verse.AI.Job TryGiveJob(Verse.Pawn pawn) => null;
    }

    public static class PawnCapacityDefOf
    {
        public static readonly Verse.PawnCapacityDef Hearing = new();
    }

    public class JobDef { }

    public static class JobDefOf
    {
        public static readonly JobDef Goto = new();
    }
}

namespace HarmonyLib
{
    public static partial class AccessTools
    {
        public static FieldInfo Field(Type type, string name) => type.GetField(name, AllDeclared);

        public static T MethodDelegate<T>(MethodInfo method)
            where T : Delegate => method.CreateDelegate<T>();
    }

    public class CodeInstruction(OpCode opcode, object operand = null)
    {
        public OpCode opcode = opcode;
        public object operand = operand;
        public List<Label> labels = new();

        public bool Calls(MethodInfo method) =>
            (opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && Equals(operand, method);

        public bool LoadsField(FieldInfo field) =>
            opcode == OpCodes.Ldfld && Equals(operand, field);
    }
}
