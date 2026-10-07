using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Opt-in synchronized enemy-caster control; never shipped to players.</summary>
public sealed class MultiplayerEnemyCombatScenarios
{
    private static object handler;
    private static Func<object, object[], bool> submit;
    private static Pawn shooter;
    private static Thing target;
    private static Hediff missingEye;
    private static string acquiredTarget;

    [Tool(
        "totalfog/multiplayer_enemy_combat",
        Description = "Register FOURTH after notifications, caravan, register-world on BOTH main menus. Setup stages a hostile non-player bolt-action shooter with one missing eye and a player steel wall on an empty 20-cell firing lane; setup-ai uses a drafted unarmed player pawn instead. Actions setup/setup-ai/acquire/reveal/attack/cleanup use native synchronized world commands; status is readonly. Acquire runs the native enemy target finder with its ordinary threat/LOS/reachability flags and the owned target as the only candidate. Tests target acquisition and the enemy attack pipeline, not autonomous raid jobs. No single-client mutation or tick stepping. Fixture does not survive save/load; cleanup before saving. Excluded from player ZIPs."
    )]
    public static Task<object> Fixture(
        IRimBridgeContext context,
        string action = "status",
        int mapId = 1
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            if (action == "register")
            {
                if (Current.Game != null)
                    throw new InvalidOperationException("Register on both main menus.");
                if (handler == null)
                    handler = MultiplayerProbeRegistration.Register(
                        typeof(MultiplayerEnemyCombatScenarios).GetMethod(
                            nameof(Command),
                            BindingFlags.NonPublic | BindingFlags.Static
                        ),
                        out submit
                    );
                return new
                {
                    success = true,
                    existingCommandIdsPreserved = true,
                    syncId = MultiplayerProbeRegistration.Id(handler),
                };
            }
            if (action == "status")
                return Status();
            if (
                action is "setup" or "setup-ai"
                && Faction.OfAncientsHostile?.HostileTo(Faction.OfPlayer) != true
            )
                throw new InvalidOperationException(
                    "The fixture needs hostile ancients before submitting setup."
                );
            var api = AccessTools.TypeByName("Multiplayer.Client.Multiplayer");
            var session = api == null ? null : AccessTools.Field(api, "session").GetValue(null);
            if (
                submit == null
                || session == null
                || AccessTools.Field(session.GetType(), "desynced").GetValue(session) is not false
                || AccessTools.Property(api, "IsReplay").GetValue(null) is not false
                || action
                    is not ("setup" or "setup-ai" or "acquire" or "reveal" or "attack" or "cleanup")
            )
                throw new InvalidOperationException("Use a registered live non-desynced session.");
            return new
            {
                success = submit(null, new object[] { action, mapId }),
                phase = "native-enemy-command-submitted",
                action,
            };
        });

    private static void Command(string action, int mapId)
    {
        if (action == "cleanup")
        {
            shooter?.Destroy();
            target?.Destroy();
            shooter = null;
            target = null;
            missingEye = null;
            acquiredTarget = null;
            return;
        }
        if (action is "setup" or "setup-ai")
        {
            if (shooter != null || target != null)
                throw new InvalidOperationException("Clean up the previous fixture first.");
            var map = Find.Maps.Single(map => map.uniqueID == mapId);
            var enemy = Faction.OfAncientsHostile;
            if (enemy == null || !enemy.HostileTo(Faction.OfPlayer))
                throw new InvalidOperationException("Use a fixture with hostile ancients.");
            IntVec3 origin = IntVec3.Invalid;
            for (int z = 35; z < map.Size.z - 35 && !origin.IsValid; z += 10)
            for (int x = 35; x < map.Size.x - 55 && !origin.IsValid; x += 10)
            {
                var lane = new CellRect(x, z - 1, 21, 3);
                if (
                    lane.Cells.All(cell =>
                        cell.Standable(map)
                        && cell.GetEdifice(map) == null
                        && cell.GetFirstPawn(map) == null
                        && !cell.GetThingList(map).Any(thing => thing.def.blockLight)
                    ) && !map.GetVisibility().IsShown(enemy, new IntVec3(x + 20, 0, z))
                )
                    origin = new IntVec3(x, 0, z);
            }
            if (!origin.IsValid)
                throw new InvalidOperationException("No empty hidden firing lane was found.");
            shooter = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, enemy);
            shooter.equipment.DestroyAllEquipment();
            shooter.equipment.AddEquipment(
                (ThingWithComps)
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"))
            );
            var eye = shooter.RaceProps.body.AllParts.First(part => part.def == BodyPartDefOf.Eye);
            missingEye = shooter.health.AddHediff(HediffDefOf.MissingBodyPart, eye);
            GenSpawn.Spawn(shooter, origin, map);
            if (action == "setup-ai")
            {
                var victim = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                victim.equipment.DestroyAllEquipment();
                target = victim;
            }
            else
            {
                target = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                target.SetFaction(Faction.OfPlayer);
            }
            GenSpawn.Spawn(target, origin + new IntVec3(20, 0, 0), map);
            if (target is Pawn heldVictim)
            {
                heldVictim.drafter.Drafted = true;
                heldVictim.jobs.StartJob(
                    JobMaker.MakeJob(JobDefOf.Wait_Combat),
                    JobCondition.InterruptForced
                );
            }
            shooter.jobs.StartJob(
                JobMaker.MakeJob(JobDefOf.Wait_Combat),
                JobCondition.InterruptForced
            );
        }
        else if (action == "reveal")
        {
            if (missingEye != null)
                shooter.health.RemoveHediff(missingEye);
            missingEye = null;
        }
        else if (action == "attack")
        {
            var job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            job.maxNumStaticAttacks = int.MaxValue;
            shooter.jobs.StartJob(job, JobCondition.InterruptForced);
        }
        else if (action == "acquire")
        {
            // Same flags as JobGiver_AIFightEnemy.FindAttackTarget. Restrict the
            // candidate pool without weakening native eligibility checks.
            var flags =
                TargetScanFlags.NeedLOSToPawns
                | TargetScanFlags.NeedReachableIfCantHitFromMyPos
                | TargetScanFlags.NeedThreat
                | TargetScanFlags.NeedAutoTargetable;
            acquiredTarget = AttackTargetFinder
                .BestAttackTarget(
                    shooter,
                    flags,
                    thing => thing == target,
                    0f,
                    56f,
                    canTakeTargetsCloserThanEffectiveMinRange: true
                )
                ?.Thing.ThingID;
        }
        shooter.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
    }

    private static object Status()
    {
        if (shooter == null || target == null)
            return new { success = true, active = false };
        var verb = shooter.equipment.Primary.GetComp<CompEquippable>().PrimaryVerb;
        return new
        {
            success = true,
            active = true,
            FogSettings.AISmart,
            acquiredTarget,
            map = shooter.Map?.uniqueID,
            shooter = new
            {
                id = shooter.ThingID,
                faction = shooter.Faction.loadID,
                playerFaction = shooter.Faction.IsPlayer,
                shooter.Dead,
                shooter.Downed,
                position = shooter.Position.ToString(),
                sightCapacity = shooter.health.capacities.GetLevel(PawnCapacityDefOf.Sight),
                range = shooter.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange,
                job = shooter.CurJob?.def.defName,
                stance = shooter.stances.curStance.GetType().FullName,
            },
            target = new
            {
                id = target.ThingID,
                target.Destroyed,
                target.HitPoints,
                position = target.Position.ToString(),
                health = (target as Pawn)?.health.summaryHealth.SummaryHealthPercent,
                seenByEnemy = shooter.Map.GetVisibility().IsShown(shooter.Faction, target.Position),
            },
            nativeLineOfSight = GenSight.LineOfSight(
                shooter.Position,
                target.Position,
                shooter.Map
            ),
            nativeWeaponRange = verb.verbProps.range,
            verbAvailable = verb.Available(),
            canHitTarget = verb.CanHitTarget(target),
            verbState = verb.state.ToString(),
        };
    }
}
