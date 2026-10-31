using System;
using System.Runtime.CompilerServices;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.Combat;

internal static class UnseenFire
{
    internal const int MemoryTicks = 600;
    private static readonly ConditionalWeakTable<Pawn, HeardShot> heardShots = new();

    // The successful firing path supplies the target before the verb can reset it.
    // Keep only a coarse location, never the shooter or a live Thing target.
    internal static void ShotFired(Verb verb, LocalTargetInfo target)
    {
        if (
            !FogSettings.AISmart
            || verb?.verbProps == null
            || verb.verbProps.IsMeleeAttack
            || verb.verbProps.soundCast == null
                && verb.verbProps.soundCastTail == null
                && verb.verbProps.soundCastBeam == null
            || target.Thing is not Pawn pawn
            || !Eligible(pawn)
            || verb.caster is not { Spawned: true } source
            || source.Map != pawn.Map
            || !pawn.HostileTo(source)
        )
            return;

        var fog = pawn.Map.GetVisibility();
        if (!fog.Initialized || fog.IsShown(pawn.Faction, source.Position))
            return;
        var shot = heardShots.GetValue(pawn, static _ => new HeardShot());
        shot.Map = pawn.Map.uniqueID;
        shot.Faction = pawn.Faction.loadID;
        shot.Tick = Find.TickManager.TicksGame;
        shot.Cell = new IntVec3(
            Math.Min(source.Position.x / 5 * 5 + 2, pawn.Map.Size.x - 1),
            0,
            Math.Min(source.Position.z / 5 * 5 + 2, pawn.Map.Size.z - 1)
        );
    }

    internal static Job InvestigationJob(
        Pawn pawn,
        Job existing,
        IntVec3 flagPosition,
        float flagRadius
    )
    {
        if (
            existing != null
            || !HasMemory(pawn)
            || pawn.mindState?.enemyTarget != null
            || !heardShots.TryGetValue(pawn, out var shot)
        )
            return existing;
        if (pawn.Position.InHorDistOf(shot.Cell, 3))
        {
            heardShots.Remove(pawn);
            return null;
        }
        // Search a small area once at a normal combat decision. Native pathing,
        // guard limits and destination reservations remain authoritative.
        var duty = pawn.mindState?.duty;
        foreach (var cell in GenRadial.RadialCellsAround(shot.Cell, 4, true))
        {
            if (
                !cell.InBounds(pawn.Map)
                || !cell.Standable(pawn.Map)
                || flagPosition.IsValid && !cell.InHorDistOf(flagPosition, flagRadius)
                || duty is { radius: >= 0 }
                    && duty.focus.IsValid
                    && !cell.InHorDistOf(duty.focus.Cell, duty.radius)
                || !pawn.Map.pawnDestinationReservationManager.CanReserve(cell, pawn, false)
                || !pawn.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)
            )
                continue;
            var job = JobMaker.MakeJob(JobDefOf.Goto, cell);
            job.expiryInterval = Math.Min(
                120,
                MemoryTicks - (Find.TickManager.TicksGame - shot.Tick)
            );
            job.checkOverrideOnExpire = true;
            return job;
        }
        heardShots.Remove(pawn);
        return null;
    }

    internal static bool HasMemory(Pawn pawn)
    {
        if (pawn == null || !heardShots.TryGetValue(pawn, out var shot))
            return false;
        int age = Find.TickManager.TicksGame - shot.Tick;
        if (
            Eligible(pawn)
            && shot.Map == pawn.Map.uniqueID
            && shot.Faction == pawn.Faction.loadID
            && age >= 0
            && age < MemoryTicks
        )
            return true;
        heardShots.Remove(pawn);
        return false;
    }

    private static bool Eligible(Pawn pawn) =>
        FogSettings.AISmart
        && pawn.Spawned
        && pawn.Map != null
        && pawn.Faction is { IsPlayer: false }
        && !pawn.Dead
        && !pawn.Downed
        && !pawn.Drafted
        && !pawn.IsPrisoner
        && !pawn.InMentalState
        && !pawn.WorkTagIsDisabled(WorkTags.Violent)
        && pawn.Awake()
        && (pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Hearing) ?? 0) > 0;

    private sealed class HeardShot
    {
        internal int Map,
            Faction,
            Tick;
        internal IntVec3 Cell;
    }
}
