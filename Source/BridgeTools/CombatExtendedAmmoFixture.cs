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

/// <summary>Observes ordinary short-bow attack jobs without changing CE shot or ammunition results.</summary>
public sealed class CombatExtendedAmmoFixture
{
    private const string ProbeId = "brrainz.totalfog.ce-ammo-probe";
    private static readonly List<Thing> Owned = new();
    private static readonly List<object> Events = new();
    private static Pawn shooter;
    private static Thing target;
    private static ThingWithComps weapon;
    private static Thing bell;
    private static ThingDef ammoDef;
    private static ThingComp ammo;
    private static Verb verb;
    private static int? originalRange;

    [Tool("totalfog/ce_ammo_fixture", Description = "Stage/read/attack/stop/configure/cleanup an isolated CE short bow with real inventory arrows and a steel wall target. Uses ordinary native AttackStatic jobs and normal playback through other tools; records unmodified ammunition-preparation and shot results. setup temporarily uses a five-cell base sight range; configure adds/removes a real player security bell using reveal. cleanup removes only owned objects and probe patches and restores the original range without saving settings. Does not survive save/load and does not establish broader combat acceptance.")]
    public static async Task<object> Fixture(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string action = "state", bool reveal = true)
    {
        return await ctx.MainThread.InvokeAsync(() =>
        {
            if (!ModsConfig.IsActive("ceteam.combatextended") || Find.CurrentMap == null || !Find.TickManager.Paused)
                throw new InvalidOperationException("Use the paused isolated CE profile.");
            if (action == "cleanup")
            {
                Cleanup();
                return (object)new { success = true, restoredRange = FogSettings.BaseViewRange, remainingObjects = Owned.Count };
            }
            var map = Find.CurrentMap;
            try
            {
                if (action == "setup")
                {
                    if (originalRange.HasValue) throw new InvalidOperationException("Clean up the existing ammo fixture first.");
                    var source = map.AllCells.First(c => c.x >= 6 && c.z >= 6 && c.x + 16 < map.Size.x &&
                        c.z + 3 < map.Size.z && new CellRect(c.x - 2, c.z - 2, 18, 5).All(cell =>
                            cell.Standable(map) && !cell.GetThingList(map).Any(t => t is Pawn || t is Building || t.def.blockLight)) &&
                        !map.mapPawns.FreeColonistsSpawned.Any(p => p.Position.InHorDistOf(c, 75)));
                    originalRange = FogSettings.BaseViewRange;
                    FogSettings.BaseViewRange = 5;
                    RefreshSight();
                    shooter = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    Owned.Add(shooter);
                    shooter.Name = new NameSingle("TF_CE_Archer");
                    shooter.equipment.DestroyAllEquipment();
                    weapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bow_Short"));
                    Owned.Add(weapon);
                    shooter.equipment.AddEquipment(weapon);
                    GenSpawn.Spawn(shooter, source, map);
                    shooter.drafter.Drafted = true;
                    Wait();
                    ammo = weapon.AllComps.Single(c => c.GetType().FullName == "CombatExtended.CompAmmoUser");
                    ammoDef = DefDatabase<ThingDef>.GetNamed("Ammo_Arrow_Stone");
                    var arrows = ThingMaker.MakeThing(ammoDef);
                    Owned.Add(arrows);
                    arrows.stackCount = 20;
                    if (!shooter.inventory.innerContainer.TryAdd(arrows)) throw new InvalidOperationException("Cannot add the actual arrow supply.");
                    AccessTools.Method(ammo.GetType(), "ResetAmmoCount").Invoke(ammo, new object[] { null });
                    if ((bool)Property(ammo, "HasMagazine") || !(bool)Property(ammo, "UseAmmo") || Property(ammo, "CurrentAmmo") != ammoDef)
                        throw new InvalidOperationException("This native short bow does not use the expected no-magazine stone arrows.");
                    verb = weapon.TryGetComp<CompEquippable>().PrimaryVerb;
                    target = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                    Owned.Add(target);
                    GenSpawn.Spawn(target, source + IntVec3.East * 13, map);
                    var harmony = new Harmony(ProbeId);
                    harmony.Patch(AccessTools.Method(ammo.GetType(), "TryPrepareShot"),
                        prefix: new HarmonyMethod(typeof(CombatExtendedAmmoFixture), nameof(PreparePrefix)),
                        postfix: new HarmonyMethod(typeof(CombatExtendedAmmoFixture), nameof(PreparePostfix)));
                    harmony.Patch(AccessTools.DeclaredMethod(AccessTools.TypeByName("CombatExtended.Verb_ShootCE"), "TryCastShot"),
                        postfix: new HarmonyMethod(typeof(CombatExtendedAmmoFixture), nameof(ShotPostfix)));
                    SetReveal(reveal);
                }
                else
                {
                    if (shooter?.Map != map || target?.Map != map || verb == null)
                        throw new InvalidOperationException("Stage the ammo fixture on this map first.");
                    if (action == "configure") SetReveal(reveal);
                    else if (action == "attack") verb.OrderForceTarget(new LocalTargetInfo(target));
                    else if (action == "stop") Wait();
                    else if (action != "state") throw new InvalidOperationException("Use setup, state, attack, stop, configure or cleanup.");
                }
                var pending = AccessTools.Field(ammo.GetType(), "ammoToBeDeleted").GetValue(ammo) as Thing;
                return (object)new
                {
                    success = true, action, tick = Find.TickManager.TicksGame,
                    ids = Owned.Select(t => t.ThingID).ToArray(), originalRange, currentRange = FogSettings.BaseViewRange,
                    shooter = new { id = shooter.ThingID, cell = shooter.Position.ToString(), job = shooter.CurJob?.def.defName,
                        stance = shooter.stances.curStance.GetType().FullName, injuries = shooter.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity),
                        sightRange = shooter.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange },
                    target = new { id = target.ThingID, cell = target.Position.ToString(), hp = target.HitPoints,
                        visible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position) },
                    weapon = new { id = weapon.ThingID, def = weapon.def.defName, verb = verb.GetType().FullName,
                        state = verb.state.ToString(), currentTarget = verb.CurrentTarget.Thing?.ThingID,
                        available = verb.Available(), canHitTarget = verb.CanHitTarget(new LocalTargetInfo(target)),
                        jobVerb = shooter.CurJob?.verbToUse?.GetType().FullName,
                        hasMagazine = Property(ammo, "HasMagazine"), useAmmo = Property(ammo, "UseAmmo"),
                        currentAmmo = (Property(ammo, "CurrentAmmo") as ThingDef)?.defName, inventoryRounds = InventoryRounds(),
                        pendingRound = pending?.ThingID, pendingDestroyed = pending?.Destroyed,
                        pendingHolder = pending?.ParentHolder?.GetType().FullName },
                    events = Events.ToArray(), ceMvid = ammo.GetType().Assembly.ManifestModule.ModuleVersionId.ToString()
                };
            }
            catch
            {
                Cleanup();
                throw;
            }
        }, cancellationToken);
    }

    private static object Property(object instance, string name) => AccessTools.Property(instance.GetType(), name).GetValue(instance, null);
    private static int InventoryRounds() => shooter?.inventory?.innerContainer.Where(t => t.def == ammoDef).Sum(t => t.stackCount) ?? 0;
    private static void Wait()
    {
        var job = JobMaker.MakeJob(JobDefOf.Wait);
        job.expiryInterval = 60000;
        shooter.jobs.StartJob(job, JobCondition.InterruptForced);
    }
    private static void RefreshSight()
    {
        foreach (var map in Find.Maps)
            foreach (var watcher in map.GetVisibility().fowWatchers.ToArray()) watcher.UpdateFoV(true);
    }
    private static void SetReveal(bool reveal)
    {
        if (reveal && bell == null)
        {
            bell = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SecurityBellSmall"));
            Owned.Add(bell);
            bell.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(bell, target.Position + IntVec3.East, target.Map);
        }
        else if (!reveal && bell != null)
        {
            bell.Destroy();
            bell = null;
        }
    }
    private static void Cleanup()
    {
        new Harmony(ProbeId).UnpatchAll(ProbeId);
        foreach (var thing in Owned) if (!thing.Destroyed) thing.Destroy();
        Owned.Clear();
        Events.Clear();
        shooter = null; target = null; weapon = null; bell = null; ammoDef = null; ammo = null; verb = null;
        if (originalRange.HasValue)
        {
            FogSettings.BaseViewRange = originalRange.Value;
            originalRange = null;
            RefreshSight();
        }
    }
    public static void PreparePrefix(ThingComp __instance, out int __state) => __state = __instance == ammo ? InventoryRounds() : -1;
    public static void PreparePostfix(ThingComp __instance, int __state, bool __result)
    {
        if (__instance != ammo) return;
        Events.Add(new { kind = "prepare", tick = Find.TickManager.TicksGame, result = __result,
            before = __state, after = InventoryRounds(), visible = target.Map.GetVisibility().IsShown(Faction.OfPlayer, target.Position) });
    }
    public static void ShotPostfix(Verb __instance, bool __result)
    {
        if (__instance != verb) return;
        Events.Add(new { kind = "shot", tick = Find.TickManager.TicksGame, result = __result,
            rounds = InventoryRounds(), target = __instance.CurrentTarget.Thing?.ThingID,
            visible = target.Map.GetVisibility().IsShown(Faction.OfPlayer, target.Position) });
    }
}
