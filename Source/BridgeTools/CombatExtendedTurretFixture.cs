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
    [Tool("totalfog/ce_turret_fixture", Description = "Stage, inspect or configure an isolated native CE mini-turret, M240B or mortar with a charged battery and hostile waiting target. setup returns owned IDs; pass them for state/configure/supply-ammo/remove-power/cleanup. enemyTurret stages an enemy mini-turret with a drafted player target. configure uses native hold-fire and an optional real security bell of the turret faction. add-alternate spawns one additional waiting target at targetDistance cells east, in the primary target's faction, for native retargeting controls. configure-enemy-fog applies the enemyFog test setting through the normal settings refresh; restore it before cleanup. configure-fire-arc applies valid native angle/span fields and CE's adjustment callback; it does not test editor input. supply-ammo places real ammunition beside the turret; reloading, manning and attacks use ordinary bridge tools. remove-power destroys only fixture power. Magazines are preloaded only during setup. Does not establish performance or all CE weapons.")]
    public static async Task<object> Fixture(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string action = "state", string ids = "", string turretDefName = "Turret_MiniTurret",
        int targetDistance = 16, bool holdFire = true, bool reveal = false,
        bool enemyTurret = false, bool enemyFog = false, float arcCenter = 0f, float arcSpan = 90f)
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
                    if (ownedIds.Count != 0 || turretDefName != "Turret_MiniTurret" &&
                        turretDefName != "Turret_M240B" && turretDefName != "Turret_Mortar")
                        throw new InvalidOperationException("Stage one supported turret in a clean fixture.");
                    if (enemyTurret && turretDefName != "Turret_MiniTurret")
                        throw new InvalidOperationException("Enemy fixture currently supports only the automatic mini-turret.");
                    if (targetDistance < 8 || targetDistance > 60) throw new ArgumentOutOfRangeException(nameof(targetDistance));
                    var source = map.AllCells.First(cell => cell.x >= 6 && cell.z >= 6 &&
                        cell.x + targetDistance + 3 < map.Size.x && cell.z + 4 < map.Size.z &&
                        new CellRect(cell.x - 3, cell.z - 2, targetDistance + 7, 6).All(c => c.Standable(map) &&
                            !c.GetThingList(map).Any(t => t is Pawn || t is Building || t.def.blockLight)) &&
                        !Visibility.IsVisible(map, cell + IntVec3.East * targetDistance) &&
                        !map.mapPawns.FreeColonistsSpawned.Any(p => p.Position.InHorDistOf(cell + IntVec3.East * targetDistance, 75)));
                    Thing Spawn(string defName, IntVec3 cell)
                    {
                        var def = DefDatabase<ThingDef>.GetNamed(defName);
                        var thing = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.Steel : null);
                        made.Add(thing); thing.SetFaction(enemyTurret ? Faction.OfAncientsHostile : Faction.OfPlayer);
                        return GenSpawn.Spawn(thing, cell, map);
                    }
                    var turret = (Building_Turret)Spawn(turretDefName, source);
                    var battery = (ThingWithComps)Spawn("Battery", source + IntVec3.West * 2);
                    battery.TryGetComp<CompPowerBattery>().SetStoredEnergyPct(1);
                    for (int x = -2; x <= 0; x++) Spawn("PowerConduit", source + new IntVec3(x, 0, -1));
                    var ammo = Ammo(turret);
                    AccessTools.Method(ammo.GetType(), "ResetAmmoCount").Invoke(ammo, new object[] { null });
                    SetHoldFire(turret, true);
                    var enemy = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist,
                        enemyTurret ? Faction.OfPlayer : Faction.OfAncientsHostile);
                    made.Add(enemy); enemy.Name = new NameSingle("TF_CE_Target");
                    enemy.equipment.DestroyAllEquipment();
                    GenSpawn.Spawn(enemy, source + IntVec3.East * targetDistance, map);
                    if (enemyTurret) enemy.drafter.Drafted = true;
                    var wait = JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval = 60000;
                    enemy.jobs.TryTakeOrderedJob(wait, JobTag.Misc);
                    owned.AddRange(made); ownedIds.AddRange(made.Select(t => t.ThingID));
                }
                else if (action != "state" && action != "configure" && action != "remove-power" && action != "supply-ammo" &&
                    action != "configure-enemy-fog" && action != "configure-fire-arc" && action != "add-alternate")
                    throw new InvalidOperationException("Use setup, state, configure, configure-enemy-fog, configure-fire-arc, add-alternate, supply-ammo, remove-power or cleanup.");

                if (action == "remove-power")
                {
                    foreach (var thing in owned.Where(t => t.def.defName == "Battery" || t.def.defName == "PowerConduit"))
                    {
                        thing.Destroy(); ownedIds.Remove(thing.ThingID);
                    }
                    owned.RemoveAll(t => t.Destroyed);
                }

                var gunTurret = owned.OfType<Building_Turret>().Single();
                // Preserve the first target's identity when an alternate is added.
                var targetThing = owned.First(t => t is Pawn || t is Corpse);
                var target = targetThing is Pawn pawn ? pawn : ((Corpse)targetThing).InnerPawn;
                if (action == "add-alternate")
                {
                    if (owned.Count(t => t is Pawn || t is Corpse) != 1 || targetDistance < 8 || targetDistance > 60)
                        throw new InvalidOperationException("Add one alternate at 8..60 cells to a single-target fixture.");
                    var cell = gunTurret.Position + IntVec3.East * targetDistance;
                    if (!cell.InBounds(map) || !cell.Standable(map) || cell.GetThingList(map).Any(t => t is Pawn || t is Building))
                        throw new InvalidOperationException("The alternate target cell must be clear and standable.");
                    var alternate = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, target.Faction);
                    made.Add(alternate); alternate.Name = new NameSingle("TF_CE_Alternate");
                    alternate.equipment.DestroyAllEquipment();
                    GenSpawn.Spawn(alternate, cell, map);
                    if (alternate.Faction == Faction.OfPlayer) alternate.drafter.Drafted = true;
                    var wait = JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval = 60000;
                    alternate.jobs.TryTakeOrderedJob(wait, JobTag.Misc);
                    owned.Add(alternate); ownedIds.Add(alternate.ThingID);
                }
                var previousEnemyFog = FogSettings.AISmart;
                if (action == "configure-enemy-fog")
                {
                    FogSettings.AISmart = enemyFog;
                    AccessTools.Method(typeof(FogSettings), "applySettings").Invoke(null, null);
                }
                if (action == "configure")
                {
                    SetHoldFire(gunTurret, holdFire);
                    var bell = owned.FirstOrDefault(t => t.def.defName == "SecurityBellSmall");
                    if (reveal && bell == null)
                    {
                        bell = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SecurityBellSmall"));
                        made.Add(bell); bell.SetFaction(gunTurret.Faction);
                        GenSpawn.Spawn(bell, targetThing.Position + IntVec3.East, map);
                        owned.Add(bell); ownedIds.Add(bell.ThingID);
                    }
                    else if (!reveal && bell != null) { bell.Destroy(); ownedIds.Remove(bell.ThingID); }
                }
                var verb = gunTurret.AttackVerb;
                var compAmmo = Ammo(gunTurret);
                var ammoDef = Property(compAmmo, "CurrentAmmo") as ThingDef;
                if (action == "supply-ammo")
                {
                    if (ammoDef == null) throw new InvalidOperationException("The staged turret has no current ammo definition.");
                    var cell = GenAdj.CellsAdjacent8Way(gunTurret).First(c => c.Standable(map) &&
                        !c.GetThingList(map).Any(t => t is Pawn || t is Building));
                    var ammo = ThingMaker.MakeThing(ammoDef);
                    made.Add(ammo);
                    ammo.stackCount = Math.Min(ammoDef.stackLimit, Math.Max(2, Convert.ToInt32(Property(compAmmo, "MagSize")) * 2));
                    GenSpawn.Spawn(ammo, cell, map);
                    owned.Add(ammo); ownedIds.Add(ammo.ThingID);
                }
                var power = gunTurret.GetComp<CompPowerTrader>();
                var sight = gunTurret.GetComp<CompFog>()?.FieldOfViewWatcher;
                var manningPawn = gunTurret.GetComp<CompMannable>()?.ManningPawn;
                var fireArc = gunTurret.AllComps.FirstOrDefault(c => c.GetType().FullName == "CombatExtended.CompFireArc");
                if (action == "configure-fire-arc")
                {
                    if (fireArc == null) throw new InvalidOperationException("The fixture turret has no native fire arc.");
                    var range = (FloatRange)AccessTools.Field(fireArc.props.GetType(), "spanRange").GetValue(fireArc.props);
                    if (float.IsNaN(arcCenter) || float.IsInfinity(arcCenter) || arcCenter < -180 || arcCenter > 180 ||
                        float.IsNaN(arcSpan) || float.IsInfinity(arcSpan) || !range.Includes(arcSpan))
                        throw new ArgumentOutOfRangeException(nameof(arcSpan), "Use a finite center in -180..180 and span within native bounds.");
                    if ((bool)AccessTools.Field(fireArc.GetType(), "Editing").GetValue(fireArc))
                        throw new InvalidOperationException("Exit CE's native arc editor before configuring the fixture.");
                    AccessTools.Field(fireArc.GetType(), "CurrentCenterAngle").SetValue(fireArc, arcCenter);
                    AccessTools.Field(fireArc.GetType(), "CurrentSpan").SetValue(fireArc, arcSpan);
                    AccessTools.Method(gunTurret.GetType(), "PostAdjustFireArc").Invoke(gunTurret, null);
                }
                return (object)new
                {
                    success = true, ids = string.Join(",", ownedIds), action,
                    enemyFog = FogSettings.AISmart, previousEnemyFog,
                    turret = new { id = gunTurret.ThingID, type = gunTurret.GetType().FullName,
                        faction = gunTurret.Faction?.loadID,
                        cell = gunTurret.Position.ToString(), powerOn = power?.PowerOn,
                        powerNet = power?.PowerNet != null, held = IsHeld(gunTurret),
                        sightRange = sight?.LastSightRange, nativeRange = verb.verbProps.range,
                        expectedUnmannedSightRange = verb.verbProps.range * FogSettings.TurretVisionModifier,
                        manned = gunTurret.GetComp<CompMannable>()?.MannedNow,
                        reloading = AccessTools.Field(gunTurret.GetType(), "isReloading").GetValue(gunTurret),
                        currentTarget = gunTurret.CurrentTarget.Thing?.ThingID,
                        currentTargetValid = gunTurret.CurrentTarget.IsValid,
                        currentTargetHasThing = gunTurret.CurrentTarget.HasThing,
                        currentTargetCell = gunTurret.CurrentTarget.Cell.ToString() },
                    fireArc = fireArc == null ? null : new
                    {
                        center = AccessTools.Field(fireArc.GetType(), "CurrentCenterAngle").GetValue(fireArc),
                        span = AccessTools.Field(fireArc.GetType(), "CurrentSpan").GetValue(fireArc),
                        within = AccessTools.Method(fireArc.GetType(), "WithinFireArc")
                            .Invoke(fireArc, new object[] { new LocalTargetInfo(targetThing) })
                    },
                    weapon = new { type = verb.GetType().FullName, requiresLos = verb.verbProps.requireLineOfSight,
                        fliesOverhead = verb.ProjectileFliesOverhead(), minRange = verb.verbProps.minRange,
                        currentTarget = verb.CurrentTarget.Thing?.ThingID,
                        currentTargetHasThing = verb.CurrentTarget.HasThing,
                        currentTargetCell = verb.CurrentTarget.Cell.ToString(),
                        bursting = verb.state == VerbState.Bursting,
                        midBurst = Property(verb, "MidBurst"),
                        locksRotation = Property(verb, "LockRotationAndAngle"),
                        ammunition = Property(compAmmo, "CurrentAmmo") is ThingDef def ? def.defName : null,
                        magazine = Property(compAmmo, "CurMagCount"), capacity = Property(compAmmo, "MagSize"),
                        lastShotTick = AccessTools.Field(typeof(Verse.Verb), "lastShotTick").GetValue(verb) },
                    crew = manningPawn == null ? null : new { id = manningPawn.ThingID,
                        cell = manningPawn.Position.ToString(), job = manningPawn.CurJob?.def.defName,
                        moving = manningPawn.pather?.Moving,
                        sightRange = manningPawn.GetComp<CompFog>()?.FieldOfViewWatcher.LastSightRange,
                        calculatedSightRange = manningPawn.GetComp<CompFog>()?.FieldOfViewWatcher.CalcPawnSightRange(manningPawn.Position, false, false),
                        groundGlow = map.glowGrid.GroundGlowAt(manningPawn.Position),
                        weatherAccuracy = map.weatherManager.CurWeatherAccuracyMultiplier },
                    ammunitionSupplies = owned.Where(t => t.def == ammoDef && !t.Destroyed)
                        .Select(t => new { id = t.ThingID, cell = t.Position.ToString(), t.stackCount }).ToArray(),
                    alternateTargets = owned.Where(t => t != targetThing && (t is Pawn || t is Corpse)).Select(t =>
                    {
                        var other = t is Pawn p ? p : ((Corpse)t).InnerPawn;
                        return new { id = other.ThingID, cell = t.Position.ToString(), other.Dead, other.Downed,
                            injuries = other.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity),
                            job = other.CurJob?.def.defName,
                            seenByTurretFaction = map.GetVisibility().IsShown(gunTurret.Faction, t.Position),
                            nativeAcquirable = AccessTools.Method(gunTurret.GetType(), "IsValidTarget").Invoke(gunTurret, new object[] { t }) };
                    }).ToArray(),
                    target = new { id = target.ThingID, cell = targetThing.Position.ToString(), target.Dead,
                        faction = target.Faction?.loadID,
                        target.Downed, job = target.CurJob?.def.defName,
                        injuries = target.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity),
                        nativeAcquirable = AccessTools.Method(gunTurret.GetType(), "IsValidTarget").Invoke(gunTurret, new object[] { targetThing }),
                        seenByTurretFaction = map.GetVisibility().IsShown(gunTurret.Faction, targetThing.Position),
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
        .SingleOrDefault(g => g.defaultLabel == "CommandHoldFire".Translate());
    private static bool IsHeld(Building_Turret turret) =>
        (bool)AccessTools.Field(turret.GetType(), "holdFire").GetValue(turret);
    private static void SetHoldFire(Building_Turret turret, bool hold)
    {
        var toggle = HoldFire(turret);
        if (IsHeld(turret) == hold) return;
        if (toggle != null) toggle.toggleAction();
        // Enemy turrets expose no player gizmo. Invoke the same native action
        // used by that gizmo; do not write the flag or bypass burst cleanup.
        else AccessTools.Method(turret.GetType(), "ToggleHoldFire").Invoke(turret, null);
    }
}
