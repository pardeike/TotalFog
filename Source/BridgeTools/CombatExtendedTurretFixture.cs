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
    private const string TraceOwner = "brrainz.totalfog.ce-burst-probe";
    private static readonly List<object> BurstEvents = new();
    private static Verse.Verb tracedVerb;
    private static bool nativeFallback, traceOverflow;
    [Tool("totalfog/ce_turret_fixture", Description = "Stage, inspect or configure an isolated native CE mini-turret, M240B or mortar with a charged battery and hostile waiting target. setup returns owned IDs; pass them for later actions. enemyTurret stages an enemy mini-turret with a drafted player target. configure uses native hold-fire and an optional real security bell of the turret faction. add-alternate spawns one additional waiting target at targetDistance cells east, in the primary target's faction. configure-enemy-fog applies the enemyFog test setting through normal refresh; restore it before cleanup. configure-fire-arc applies native angle/span fields and CE's adjustment callback; it does not test editor input. configure-aim-mode uses the weapon's native toggle and rejects unavailable modes. trace-start/trace-stop observe at most 128 actual burst-fallback results before/after Total Fog without changing them. fallback-policy supplies false/true inputs to the loaded fog guard; it is a readonly contract check, not a native fallback reproduction. supply-ammo places real ammunition beside the turret; reloading, manning and attacks use ordinary bridge tools. remove-power destroys only fixture power. cleanup removes owned things and their active trace. Magazines are preloaded only during setup. Does not establish performance or all CE weapons.")]
    public static async Task<object> Fixture(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string action = "state", string ids = "", string turretDefName = "Turret_MiniTurret",
        int targetDistance = 16, bool holdFire = true, bool reveal = false,
        bool enemyTurret = false, bool enemyFog = false, float arcCenter = 0f, float arcSpan = 90f,
        string aimMode = "SuppressFire")
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
                if (owned.Contains(tracedVerb?.caster)) StopTrace();
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
                    action != "configure-enemy-fog" && action != "configure-fire-arc" && action != "add-alternate" &&
                    action != "configure-aim-mode" && action != "trace-start" && action != "trace-stop" && action != "fallback-policy")
                    throw new InvalidOperationException("Use setup, state, configure, configure-enemy-fog, configure-fire-arc, configure-aim-mode, trace-start, trace-stop, fallback-policy, add-alternate, supply-ammo, remove-power or cleanup.");

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
                var fireModes = verb.EquipmentSource?.AllComps.FirstOrDefault(c => c.GetType().FullName == "CombatExtended.CompFireModes");
                string[] AimModes() => fireModes == null ? new string[0] :
                    ((System.Collections.IEnumerable)Property(fireModes, "AvailableAimModes")).Cast<object>().Select(mode => mode.ToString()).ToArray();
                if (action == "configure-aim-mode")
                {
                    var available = AimModes();
                    if (!available.Contains(aimMode)) throw new InvalidOperationException("Select one of this weapon's native aim modes.");
                    for (int i = 0; i < available.Length && Property(fireModes, "CurrentAimMode").ToString() != aimMode; i++)
                        AccessTools.Method(fireModes.GetType(), "ToggleAimMode").Invoke(fireModes, null);
                    if (Property(fireModes, "CurrentAimMode").ToString() != aimMode)
                        throw new InvalidOperationException("The native aim-mode toggle did not select the requested mode.");
                }
                if (action == "trace-start") StartTrace(verb);
                else if (action == "trace-stop") StopTrace();
                // This readonly contract check supplies the incoming result;
                // it does not claim that CE naturally took this fallback branch.
                object[] fallbackPolicy = null;
                if (action == "fallback-policy")
                {
                    var guard = AccessTools.Method(typeof(TotalFogMod).Assembly.GetType("TotalFog.Compatibility.CombatExtendedIntegration"),
                        "BurstFallbackPostfix");
                    fallbackPolicy = new[] { false, true }.Select(original =>
                    {
                        var arguments = new object[] { verb, original, new ShootLine(verb.caster.Position, verb.CurrentTarget.Cell) };
                        guard.Invoke(null, arguments);
                        return (object)new { originalResult = original, filteredResult = (bool)arguments[1],
                            hasThing = verb.CurrentTarget.HasThing, target = verb.CurrentTarget.Thing?.ThingID };
                    }).ToArray();
                }
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
                        warmupTicksLeft = AccessTools.Field(gunTurret.GetType(), "burstWarmupTicksLeft").GetValue(gunTurret),
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
                    fireModes = fireModes == null ? null : new { current = Property(fireModes, "CurrentAimMode").ToString(), available = AimModes() },
                    burstTrace = new { active = tracedVerb == verb, overflow = traceOverflow, rows = BurstEvents.ToArray() },
                    fallbackPolicy,
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

    private static void StartTrace(Verse.Verb verb)
    {
        if (tracedVerb != null) throw new InvalidOperationException("Stop the active burst trace first.");
        var method = AccessTools.Method(verb.GetType(), "KeepBurstOnNoShootLine",
            new[] { typeof(bool), typeof(ShootLine).MakeByRefType() });
        // Harmony's patch registry uses the declared MethodInfo. Resolving an
        // inherited member from Verb_ShootCE can retain a different ReflectedType.
        if (method != null) method = AccessTools.DeclaredMethod(method.DeclaringType, method.Name,
            new[] { typeof(bool), typeof(ShootLine).MakeByRefType() });
        if (method == null || Harmony.GetPatchInfo(method)?.Owners.Contains("brrainz.totalfog") != true)
            throw new InvalidOperationException("The actual native fallback must have Total Fog's integration hook.");
        BurstEvents.Clear(); traceOverflow = false;
        var harmony = new Harmony(TraceOwner);
        try
        {
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(CombatExtendedTurretFixture), nameof(NativeFallbackPostfix))
                { priority = Priority.First, before = new[] { "brrainz.totalfog" } });
            harmony.Patch(method, postfix: new HarmonyMethod(typeof(CombatExtendedTurretFixture), nameof(FinalFallbackPostfix))
                { priority = Priority.Last, after = new[] { "brrainz.totalfog" } });
            tracedVerb = verb;
        }
        catch { StopTrace(); throw; }
    }
    private static void StopTrace()
    {
        new Harmony(TraceOwner).UnpatchAll(TraceOwner);
        tracedVerb = null;
    }
    public static void NativeFallbackPostfix(Verse.Verb __instance, bool __result)
    {
        if (__instance == tracedVerb) nativeFallback = __result;
    }
    public static void FinalFallbackPostfix(Verse.Verb __instance, bool __result, bool __0)
    {
        if (__instance != tracedVerb) return;
        if (BurstEvents.Count >= 128) { traceOverflow = true; return; }
        // The native method and Total Fog's readonly guard execute synchronously;
        // this snapshot retains the native result before the final fog decision.
        var target = __instance.CurrentTarget;
        var observer = __instance.caster.TryGetComp<CompMannable>()?.ManningPawn ?? __instance.caster;
        BurstEvents.Add(new { tick = Find.TickManager.TicksGame, suppressing = __0, nativeResult = nativeFallback,
            finalResult = __result, hasThing = target.HasThing, target = target.Thing?.ThingID, cell = target.Cell.ToString(),
            seenByFaction = observer.Map.GetVisibility().IsShown(observer.Faction, target.Cell) });
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
