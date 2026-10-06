using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native CE turret/mortar fixtures. IDs survive save/load; no gameplay assembly dependency on CE.</summary>
public sealed class CombatExtendedTurretFixture
{
    [Tool("totalfog/ce_turret_fixture", Description = "Stage, inspect or configure an isolated native CE mini-turret/mortar with a charged battery and hostile waiting target. setup returns owned IDs; pass them for state/configure/remove-power/cleanup. configure uses the actual hold-fire gizmo and an optional real security bell. remove-power destroys only the fixture battery and conduits. Playback, manning and attack orders use ordinary bridge tools. Magazines are preloaded only during fixture setup. Does not establish performance or all CE weapons.")]
    public static async Task<object> Fixture(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string action = "state", string ids = "", string turretDefName = "Turret_MiniTurret",
        int targetDistance = 16, bool holdFire = true, bool reveal = false)
    {
        return await ctx.MainThread.InvokeAsync(() =>
        {
            if (!ModsConfig.IsActive("ceteam.combatextended") || Find.CurrentMap == null || !Find.TickManager.Paused)
                throw new InvalidOperationException("Use the paused isolated CE profile.");
            var map = Find.CurrentMap;
            var ownedIds = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(id => id.Trim()).ToList();
            var made = new List<Thing>();
            Thing Owned(string id) => map.listerThings.AllThings.FirstOrDefault(t => t.ThingID == id ||
                t is Corpse corpse && corpse.InnerPawn.ThingID == id);
            var owned = ownedIds.Select(Owned).Where(t => t != null).ToList();
            if (action == "cleanup")
            {
                foreach (var thing in owned) if (!thing.Destroyed) thing.Destroy();
                return (object)new { success = ownedIds.All(id => Owned(id) == null), ownedIds };
            }
            try
            {
                if (action == "setup")
                {
                    if (ownedIds.Count != 0 || turretDefName != "Turret_MiniTurret" && turretDefName != "Turret_Mortar")
                        throw new InvalidOperationException("Stage one supported turret in a clean fixture.");
                    if (targetDistance < 8 || targetDistance > 60) throw new ArgumentOutOfRangeException(nameof(targetDistance));
                    var source = map.AllCells.First(cell => cell.x >= 6 && cell.z >= 6 &&
                        cell.x + targetDistance + 3 < map.Size.x && cell.z + 4 < map.Size.z &&
                        new CellRect(cell.x - 3, cell.z - 2, targetDistance + 7, 6).All(c => c.Standable(map) &&
                            !c.GetThingList(map).Any(t => t is Pawn || t is Building || t.def.blockLight)) &&
                        !Visibility.IsVisible(map, cell + IntVec3.East * targetDistance));
                    Thing Spawn(string defName, IntVec3 cell)
                    {
                        var def = DefDatabase<ThingDef>.GetNamed(defName);
                        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null);
                        made.Add(thing); thing.SetFaction(Faction.OfPlayer);
                        return GenSpawn.Spawn(thing, cell, map);
                    }
                    var turret = (Building_Turret)Spawn(turretDefName, source);
                    var battery = (ThingWithComps)Spawn("Battery", source + IntVec3.West * 2);
                    battery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
                    for (int x = -2; x <= 0; x++) Spawn("PowerConduit", source + new IntVec3(x, 0, -1));
                    var ammo = Ammo(turret);
                    AccessTools.Method(ammo.GetType(), "ResetAmmoCount").Invoke(ammo, new object[] { null });
                    SetHoldFire(turret, true);
                    var enemy = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile);
                    made.Add(enemy); enemy.Name = new NameSingle("TF_CE_Target");
                    enemy.equipment.DestroyAllEquipment();
                    GenSpawn.Spawn(enemy, source + IntVec3.East * targetDistance, map);
                    var wait = JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval = 60000;
                    enemy.jobs.TryTakeOrderedJob(wait, JobTag.Misc);
                    owned.AddRange(made); ownedIds.AddRange(made.Select(t => t.ThingID));
                }
                else if (action != "state" && action != "configure" && action != "remove-power")
                    throw new InvalidOperationException("Use setup, state, configure, remove-power or cleanup.");

                if (action == "remove-power")
                {
                    foreach (var thing in owned.Where(t => t.def.defName == "Battery" || t.def.defName == "PowerConduit"))
                    {
                        thing.Destroy(); ownedIds.Remove(thing.ThingID);
                    }
                    owned.RemoveAll(t => t.Destroyed);
                }

                var gunTurret = owned.OfType<Building_Turret>().Single();
                var targetThing = owned.Single(t => t is Pawn || t is Corpse);
                var target = targetThing is Pawn pawn ? pawn : ((Corpse)targetThing).InnerPawn;
                if (action == "configure")
                {
                    SetHoldFire(gunTurret, holdFire);
                    var bell = owned.FirstOrDefault(t => t.def.defName == "SecurityBellSmall");
                    if (reveal && bell == null)
                    {
                        bell = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SecurityBellSmall"));
                        made.Add(bell); bell.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(bell, targetThing.Position + IntVec3.East, map);
                        owned.Add(bell); ownedIds.Add(bell.ThingID);
                    }
                    else if (!reveal && bell != null) { bell.Destroy(); ownedIds.Remove(bell.ThingID); }
                }
                var verb = gunTurret.AttackVerb;
                var compAmmo = Ammo(gunTurret);
                var power = gunTurret.GetComp<CompPowerTrader>();
                var sight = gunTurret.GetComp<CompFog>()?.FieldOfViewWatcher;
                return (object)new
                {
                    success = true, ids = string.Join(",", ownedIds), action,
                    turret = new { id = gunTurret.ThingID, type = gunTurret.GetType().FullName,
                        cell = gunTurret.Position.ToString(), powerOn = power?.PowerOn,
                        powerNet = power?.PowerNet != null, held = HoldFire(gunTurret).isActive(),
                        sightRange = sight?.LastSightRange, nativeRange = verb.verbProps.range,
                        expectedUnmannedSightRange = verb.verbProps.range * FogSettings.TurretVisionModifier,
                        manned = gunTurret.GetComp<CompMannable>()?.MannedNow,
                        currentTarget = gunTurret.CurrentTarget.Thing?.ThingID },
                    weapon = new { type = verb.GetType().FullName, requiresLos = verb.verbProps.requireLineOfSight,
                        ammunition = Property(compAmmo, "CurrentAmmo") is ThingDef def ? def.defName : null,
                        magazine = Property(compAmmo, "CurMagCount"), capacity = Property(compAmmo, "MagSize"),
                        lastShotTick = AccessTools.Field(typeof(Verse.Verb), "lastShotTick").GetValue(verb) },
                    target = new { id = target.ThingID, cell = targetThing.Position.ToString(), target.Dead,
                        target.Downed, job = target.CurJob?.def.defName,
                        injuries = target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity),
                        shown = Visibility.IsVisible(map, targetThing.Position) }
                };
            }
            catch
            {
                foreach (var thing in made) if (!thing.Destroyed) thing.Destroy();
                throw;
            }
        }, cancellationToken);
    }

    private static object Property(object instance, string name) =>
        (AccessTools.Property(instance.GetType(), name) ?? throw new InvalidOperationException("Missing CE property " + name)).GetValue(instance);
    private static object Ammo(Building_Turret turret) => Property(turret, "CompAmmo") ??
        throw new InvalidOperationException("The staged CE turret has no ammunition component.");
    private static Command_Toggle HoldFire(Building_Turret turret) => turret.GetGizmos().OfType<Command_Toggle>()
        .Single(g => g.defaultLabel == "CommandHoldFire".Translate());
    private static void SetHoldFire(Building_Turret turret, bool hold)
    {
        var toggle = HoldFire(turret);
        if (toggle.isActive() != hold) toggle.toggleAction();
    }
}
