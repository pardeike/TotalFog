using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native CE bounds, hit checks and ordinary bow projectile producer.</summary>
public sealed class WatchtowerCEScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Pawn observedShooter;
    private static readonly List<object> launches = new();
    private static readonly List<string> impacts = new();
    private static readonly HashSet<Thing> shots = new();
    private static readonly List<object> draws = new();
    private static readonly HashSet<string> trackedIds = new();
    private static readonly List<object> flightEvents = new();

    private static void FlightEvent(Thing projectile, string phase, string hit = null)
    {
        if (!trackedIds.Contains(projectile.ThingID) || flightEvents.Count >= 160)
            return;
        var type = projectile.GetType();
        flightEvents.Add(
            new
            {
                phase,
                id = projectile.ThingID,
                projectile.Spawned,
                projectile.Destroyed,
                hit,
                exact = AccessTools
                    .Property(type, "ExactPosition")
                    .GetValue(projectile)
                    ?.ToString(),
                destination = AccessTools
                    .Field(type, "Destination")
                    .GetValue(projectile)
                    ?.ToString(),
                tick = AccessTools.Field(type, "FlightTicks").GetValue(projectile),
            }
        );
    }

    private static void TickPrefix(Thing __instance) =>
        FlightEvent(__instance, "before-native-tick");

    private static void TickPostfix(Thing __instance) =>
        FlightEvent(__instance, "after-native-tick");

    private static void DestroyPrefix(Thing __instance)
    {
        if (!trackedIds.Contains(__instance.ThingID))
            return;
        FlightEvent(__instance, "native-destroy");
        flightEvents.Add(
            new { phase = "destroy-stack", stack = new System.Diagnostics.StackTrace().ToString() }
        );
    }

    private static void DrawPrefix(Thing __instance, Vector3 __0)
    {
        if (shots.Contains(__instance) && draws.Count < 64)
            draws.Add(
                new
                {
                    id = __instance.ThingID,
                    native = __instance.DrawPos.ToString(),
                    projected = __0.ToString(),
                    offset = Compatibility
                        .CombatExtendedProjectileDrawing.DrawingOffset(__instance)
                        .ToString(),
                }
            );
    }

    private static void LaunchPostfix(Thing __instance, Thing __0, float __4)
    {
        if (__0 == observedShooter)
        {
            shots.Add(__instance);
            trackedIds.Add(__instance.ThingID);
            launches.Add(
                new
                {
                    id = __instance.ThingID,
                    shotHeight = __4,
                    nativeDrawPos = __instance.DrawPos.ToString(),
                    shooterOffset = TowerPresentation.Offset(__0).ToString(),
                    trajectory = AccessTools
                        .Property(__instance.GetType(), "TrajectoryWorker")
                        .GetValue(__instance)
                        ?.GetType()
                        .FullName,
                }
            );
        }
    }

    private static void ImpactPrefix(Thing __instance, Thing hitThing)
    {
        if (shots.Contains(__instance) || trackedIds.Contains(__instance.ThingID))
        {
            impacts.Add(hitThing?.ThingID ?? "ground");
            FlightEvent(__instance, "native-impact", hitThing?.ThingID ?? "ground");
        }
    }

    [Tool(
        "totalfog/watchtower_ce",
        Description = "Check actual CE collision bounds during native ladder movement, native tall-obstacle hit checks with a separate sight source, and ordinary automatic short-bow fire over a low wall using real inventory arrows. Native CE launch/impact observers preserve results. Restore the unchanged paused named single-player fixture; reject Multiplayer."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (
            string.IsNullOrEmpty(saveName)
            || string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Supply a save and plain evidence label.");
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_ce", evidenceLabel);
        proof.saveName = saveName;
        var states = new List<object>();
        var probe = new Harmony("brrainz.totalfog.watchtower-ce-probe");
        bool staged = false;
        var oldShooting = FogSettings.TowerShootingAllowed;
        var oldAI = FogSettings.AISmart;
        var oldPause = Prefs.PauseOnLoad;
        Building_Watchtower tower = null;
        Pawn lookout = null,
            enemy = null;
        Map map = null;
        Func<Thing, Bounds> bounds = null;
        Verb verb = null;
        Building lowWall = null;
        CellRect area = default;
        float enemyInjury = 0,
            lookoutInjury = 0;
        try
        {
            await On(() =>
            {
                map = Find.CurrentMap;
                if (
                    !ModsConfig.IsActive("ceteam.combatextended")
                    || map?.GetVisibility()?.Initialized != true
                    || !Find.TickManager.Paused
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use a paused initialized CE single-player fixture."
                    );
                var utility = AccessTools.TypeByName("CombatExtended.CE_Utility");
                bounds = AccessTools.MethodDelegate<Func<Thing, Bounds>>(
                    AccessTools.Method(utility, "GetBoundsFor", new[] { typeof(Thing) })
                );
                var projectileType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
                probe.Patch(
                    AccessTools.Method(
                        projectileType,
                        "Launch",
                        new[]
                        {
                            typeof(Thing),
                            typeof(Vector2),
                            typeof(float),
                            typeof(float),
                            typeof(float),
                            typeof(float),
                            typeof(Thing),
                            typeof(float),
                        }
                    ),
                    postfix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(LaunchPostfix))
                );
                probe.Patch(
                    AccessTools.DeclaredMethod(projectileType, "Impact", new[] { typeof(Thing) }),
                    prefix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(ImpactPrefix))
                );
                probe.Patch(
                    AccessTools.DeclaredMethod(
                        projectileType,
                        "DrawAt",
                        new[] { typeof(Vector3), typeof(bool) }
                    ),
                    prefix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(DrawPrefix))
                    {
                        priority = Priority.Last,
                    }
                );
                probe.Patch(
                    AccessTools.DeclaredMethod(projectileType, "Tick"),
                    prefix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(TickPrefix)),
                    postfix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(TickPostfix))
                );
                probe.Patch(
                    AccessTools.Method(typeof(Thing), "Destroy"),
                    prefix: new HarmonyMethod(typeof(WatchtowerCEScenarios), nameof(DestroyPrefix))
                );
                var center = map
                    .AllCells.Where(c =>
                        new CellRect(c.x - 5, c.z - 4, 26, 9).All(p =>
                            p.InBounds(map)
                            && p.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy)
                            && !p.Roofed(map)
                            && p.GetThingList(map).All(t => t is not Pawn and not Building)
                        )
                    )
                    .DefaultIfEmpty(IntVec3.Invalid)
                    .First();
                if (!center.IsValid)
                    throw new InvalidOperationException(
                        "No suitable isolated CE tower area is available on this map."
                    );
                area = new CellRect(center.x - 5, center.z - 4, 26, 9);
                foreach (var p in area)
                {
                    map.fogGrid.Unfog(p);
                    foreach (var thing in p.GetThingList(map).ToArray())
                        thing.Destroy(DestroyMode.Vanish);
                }
                staged = true;
                FogSettings.AISmart = false;
                FogSettings.TowerShootingAllowed = false;
                Prefs.PauseOnLoad = true;
                tower = (Building_Watchtower)
                    ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, ThingDefOf.WoodLog);
                tower.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(tower, center, map, Rot4.South);
                lookout = AddPawn(tower.Approach, Faction.OfPlayer);
                enemy = AddPawn(center + IntVec3.East * 12, Faction.OfAncientsHostile);
                AddPawn(enemy.Position + IntVec3.East * 3, Faction.OfPlayer);
                verb = EquipBow(lookout);
                EquipBow(enemy);
                observedShooter = lookout;
                launches.Clear();
                impacts.Clear();
                shots.Clear();
                draws.Clear();
                trackedIds.Clear();
                flightEvents.Clear();
                lookout.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
                );
            });
            await Until(() => tower.Elevation >= .45f, "partial climb", 160);
            await On(() =>
            {
                Record("partial-climb");
                Check("CE partial climber collider rises with ladder", bounds(lookout).min.y > .7f);
                Check(
                    "CE ordinary ground target retains native collider floor",
                    Mathf.Abs(bounds(enemy).min.y) < .001f
                );
            });
            await Until(() => tower.Watching, "completed climb", 180);
            await On(() =>
            {
                Record("full-platform");
                Check(
                    "CE full platform collider retains native body above deck",
                    bounds(lookout).min.y >= 1.99f
                );
                Check("CE shooting default remains disabled", !verb.TryStartCastOn(enemy));
                FogSettings.TowerShootingAllowed = true;
                Record("open-shot-control");
                Check(
                    "native CE target is within effective bow range",
                    !verb.OutOfRange(lookout.Position, enemy, enemy.OccupiedRect())
                );
                Check("native CE elevated bow can target open lane", verb.CanHitTarget(enemy));
                var blocker = (Building_Watchtower)
                    ThingMaker.MakeThing(
                        WatchtowerDefOf.TotalFog_Watchtower,
                        DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                    );
                blocker.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(blocker, tower.Position + IntVec3.East * 7, map, Rot4.North);
                Check("separate native observer keeps target visible", Visibility.IsVisible(enemy));
                Check("CE respects another tower as a tall obstacle", !verb.CanHitTarget(enemy));
                blocker.Destroy(DestroyMode.Vanish);
                lowWall = (Building)ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                GenSpawn.Spawn(lowWall, tower.Position + IntVec3.East * 3, map);
                tower.RefreshSight();
                Check("CE elevated bow can shoot over a low wall", verb.CanHitTarget(enemy));
                enemyInjury = Injury(enemy);
                Record("automatic-fire-control");
            });
            await Frame();
            await Until(() => launches.Count > 0, "automatic native CE launch", 600);
            await ctx.Game.FramesAsync(10, cancellationToken);
            await Capture("outgoing-arrow");
            Thing savedArrow = null;
            Vector3 savedOffset = default,
                savedExact = default;
            string towerId = tower.ThingID,
                lookoutId = lookout.ThingID,
                enemyId = enemy.ThingID,
                wallId = lowWall.ThingID,
                arrowId = null;
            await On(() =>
            {
                savedArrow = shots.FirstOrDefault(t => t.Spawned);
                if (savedArrow == null)
                    throw new InvalidOperationException(
                        "Native arrow must remain in flight for reload control."
                    );
                arrowId = savedArrow.ThingID;
                savedOffset = Compatibility.CombatExtendedProjectileDrawing.DrawingOffset(
                    savedArrow
                );
                savedExact = (Vector3)
                    AccessTools
                        .Property(savedArrow.GetType(), "ExactPosition")
                        .GetValue(savedArrow);
                Check("actual CE outgoing draw calls observed", draws.Count > 0);
                Check("CE projectile presentation raises the native muzzle", savedOffset.z > .1f);
                states.Add(
                    new
                    {
                        phase = "before-flight-save",
                        tick = Find.TickManager.TicksGame,
                        pauseOnLoad = Prefs.PauseOnLoad,
                        paused = Find.TickManager.Paused,
                        arrowId,
                        towerId,
                        lookoutId,
                        enemyId,
                        wallId,
                    }
                );
                GameDataSaveLoader.SaveGame(evidenceLabel + "-flight");
            });
            var referenceTick = await ctx.Game.StepTicksAsync(
                1,
                cancellationToken: cancellationToken
            );
            if (!referenceTick.Success)
                throw new InvalidOperationException("CE flight reference tick failed.");
            await On(() =>
            {
                savedOffset = Compatibility.CombatExtendedProjectileDrawing.DrawingOffset(
                    savedArrow
                );
                savedExact = (Vector3)
                    AccessTools
                        .Property(savedArrow.GetType(), "ExactPosition")
                        .GetValue(savedArrow);
                Prefs.PauseOnLoad = true;
                states.Add(
                    new
                    {
                        phase = "expected-flight-reference-tick",
                        tick = Find.TickManager.TicksGame,
                        pauseOnLoad = Prefs.PauseOnLoad,
                        paused = Find.TickManager.Paused,
                        expectedOffset = savedOffset.ToString(),
                        expectedExact = savedExact.ToString(),
                    }
                );
            });
            var reloaded = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName = evidenceLabel + "-flight",
                    readiness = "visual",
                    pauseIfNeeded = true,
                },
                cancellationToken: cancellationToken
            );
            proof.assertions.Add(
                RimBridgeEvidence.ToolSucceeded("native CE in-flight save reload", reloaded)
            );
            if (!reloaded.Succeeded())
                throw new InvalidOperationException("Native CE in-flight save reload.");
            await On(() =>
            {
                map = Find.CurrentMap;
                var loaded = map.listerThings.AllThings.ToDictionary(t => t.ThingID);
                // CE projectiles are not guaranteed to be in the native general
                // lister after loading; use their real grid registration too.
                foreach (var cell in area)
                foreach (var thing in cell.GetThingList(map))
                    loaded[thing.ThingID] = thing;
                states.Add(
                    new
                    {
                        phase = "after-flight-load",
                        tick = Find.TickManager.TicksGame,
                        pauseOnLoad = Prefs.PauseOnLoad,
                        paused = Find.TickManager.Paused,
                        present = new[] { towerId, lookoutId, enemyId, wallId, arrowId }
                            .Select(id => new { id, present = loaded.ContainsKey(id) })
                            .ToArray(),
                    }
                );
                Thing Loaded(string id) =>
                    loaded.TryGetValue(id, out var thing)
                        ? thing
                        : throw new InvalidOperationException(
                            "Missing loaded CE fixture object: " + id
                        );
                tower = (Building_Watchtower)Loaded(towerId);
                lookout = (Pawn)Loaded(lookoutId);
                enemy = (Pawn)Loaded(enemyId);
                lowWall = (Building)Loaded(wallId);
                savedArrow = Loaded(arrowId);
                verb = lookout.equipment.Primary.TryGetComp<CompEquippable>().PrimaryVerb;
                observedShooter = lookout;
                shots.Clear();
                shots.Add(savedArrow);
                Check(
                    "CE projectile projection persists across native reload",
                    (
                        Compatibility.CombatExtendedProjectileDrawing.DrawingOffset(savedArrow)
                        - savedOffset
                    ).sqrMagnitude < .000001f
                );
                Check(
                    "CE physics position persists independently",
                    (
                        (Vector3)
                            AccessTools
                                .Property(savedArrow.GetType(), "ExactPosition")
                                .GetValue(savedArrow) - savedExact
                    ).sqrMagnitude < .000001f
                );
            });
            await Until(
                () => enemy.Dead || Injury(enemy) > enemyInjury,
                "native bow impact",
                1200,
                20
            );
            await On(() =>
            {
                Record("native-bow-impact");
                Check("native CE producer launched raised arrows", launches.Count > 0);
                Check(
                    "native CE arrow reaches ground target",
                    impacts.Contains(enemy.ThingID) && (enemy.Dead || Injury(enemy) > enemyInjury)
                );
                Check(
                    "native CE arrow leaves source tower intact",
                    tower.Spawned && tower.HitPoints == tower.MaxHitPoints
                );
            });
            await On(() =>
            {
                FogSettings.TowerShootingAllowed = false;
                verb.Reset();
                lowWall.Destroy(DestroyMode.Vanish);
                var enemyCell = enemy.PositionHeld;
                if (enemy.Corpse?.Spawned == true)
                    enemy.Corpse.Destroy(DestroyMode.Vanish);
                else
                    enemy.Destroy(DestroyMode.Vanish);
                enemy = AddPawn(enemyCell, Faction.OfAncientsHostile);
                var enemyVerb = EquipBow(enemy);
                observedShooter = enemy;
                launches.Clear();
                impacts.Clear();
                shots.Clear();
                Check("native CE enemy can target full lookout", enemyVerb.CanHitTarget(lookout));
                states.Add(
                    new
                    {
                        phase = "incoming-open-shot-control",
                        hit = HitReport(enemyVerb, lookout),
                        range = enemyVerb.EffectiveRange,
                    }
                );
                lookoutInjury = Injury(lookout);
                enemyVerb.OrderForceTarget(lookout);
            });
            await Until(() => launches.Count > 0, "native incoming launch", 600);
            await ctx.Game.FramesAsync(10, cancellationToken);
            await Capture("incoming-arrow");
            await Until(
                () => lookout.Dead || Injury(lookout) > lookoutInjury || impacts.Count >= 6,
                "native incoming arrows",
                1200,
                20
            );
            await On(() =>
            {
                Record("native-incoming-impact");
                Check("native CE incoming producer launches arrows", launches.Count > 0);
                Check(
                    "native CE incoming arrows can injure elevated lookout",
                    impacts.Contains(lookout.ThingID)
                        && (lookout.Dead || Injury(lookout) > lookoutInjury)
                );
                enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                enemy.stances.CancelBusyStanceHard();
                lookout.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerDescend, tower)
                );
            });
            await Until(() => tower.Elevation < .6f && tower.Elevation > 0, "partial descent", 120);
            await On(() =>
            {
                Record("partial-descent");
                Check(
                    "CE collider lowers gradually on native descent",
                    Mathf.Abs(bounds(lookout).min.y - 2f * tower.Elevation) < .02f
                );
                Check(
                    "CE descent remains below native completed platform floor",
                    bounds(lookout).min.y < 1.25f
                );
            });
            await Capture("partial-descent");

            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var request = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist,
                    faction,
                    canGeneratePawnRelations: false,
                    mustBeCapableOfViolence: true
                );
                var pawn = PawnGenerator.GeneratePawn(request);
                pawn.equipment.DestroyAllEquipment();
                // Native pawn generation can equip CE armor that deflects the
                // first arrow and sends the hostile pawn fleeing out of range.
                // This fixture tests projectile geometry, so keep both targets
                // unarmored while preserving native injury and damage handling.
                pawn.apparel.DestroyAll();
                GenSpawn.Spawn(pawn, cell, map);
                if (pawn.drafter != null)
                    pawn.drafter.Drafted = true;
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                return pawn;
            }
            Verb EquipBow(Pawn pawn)
            {
                var bow = (ThingWithComps)
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Bow_Short"));
                pawn.equipment.AddEquipment(bow);
                var arrows = ThingMaker.MakeThing(
                    DefDatabase<ThingDef>.GetNamed("Ammo_Arrow_Stone")
                );
                arrows.stackCount = 20;
                if (!pawn.inventory.innerContainer.TryAdd(arrows))
                    throw new InvalidOperationException("Native arrow inventory failed.");
                var ammo = bow.AllComps.Single(c =>
                    c.GetType().FullName == "CombatExtended.CompAmmoUser"
                );
                AccessTools
                    .Method(ammo.GetType(), "ResetAmmoCount")
                    .Invoke(ammo, new object[] { null });
                pawn.skills.GetSkill(SkillDefOf.Shooting).Level = 20;
                return bow.TryGetComp<CompEquippable>().PrimaryVerb;
            }
            void Record(string phase) =>
                states.Add(
                    new
                    {
                        phase,
                        tower.Elevation,
                        tower.Watching,
                        colliderMin = bounds(lookout).min.ToString(),
                        colliderMax = bounds(lookout).max.ToString(),
                        offset = TowerPresentation.Offset(lookout).ToString(),
                        shotHeight = AccessTools
                            .Property(verb.GetType(), "ShotHeight")
                            .GetValue(verb),
                        launches = launches.ToArray(),
                        draws = draws.ToArray(),
                        impacts = impacts.ToArray(),
                        source = lookout.Position.ToString(),
                        target = enemy.Position.ToString(),
                        nativeVerb = verb.GetType().FullName,
                        effectiveRange = verb.EffectiveRange,
                        available = verb.Available(),
                        drafted = lookout.Drafted,
                        job = lookout.CurJobDef?.defName,
                        food = lookout.needs.food?.CurLevelPercentage,
                        rest = lookout.needs.rest?.CurLevelPercentage,
                        autoTarget = AttackTargetFinder
                            .BestShootTargetFromCurrentPosition(
                                lookout,
                                TargetScanFlags.NeedLOSToAll | TargetScanFlags.NeedThreat
                            )
                            ?.Thing.ThingID,
                        hit = HitReport(verb, enemy),
                        injuries = Injury(enemy),
                        lookoutInjuries = Injury(lookout),
                    }
                );
            void Check(string name, bool value) =>
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            object HitReport(Verb selected, Thing target)
            {
                var args = new object[]
                {
                    selected.caster.Position,
                    new LocalTargetInfo(target),
                    null,
                };
                var allowed = AccessTools
                    .Method(
                        selected.GetType(),
                        "CanHitTargetFrom",
                        new[]
                        {
                            typeof(IntVec3),
                            typeof(LocalTargetInfo),
                            typeof(string).MakeByRefType(),
                        }
                    )
                    .Invoke(selected, args);
                return new { allowed, report = args[2] };
            }
            await On(() => Record("complete-state"));
            async Task Frame()
            {
                var r = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ,
                        width = area.Width,
                        height = area.Height,
                        rootSize = 16f,
                    },
                    cancellationToken: cancellationToken
                );
                if (!r.Succeeded())
                    throw new InvalidOperationException("Frame native CE tower.");
                await ctx.Game.FramesAsync(10, cancellationToken);
            }
            async Task Capture(string phase)
            {
                var r = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ,
                        width = area.Width,
                        height = area.Height,
                        rootSize = 16f,
                        paddingCells = 0,
                        fileName = evidenceLabel + "-" + phase,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                proof.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + phase, r));
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = phase,
                        kind = "cell_rect",
                        path = r.TryReadResult<string>(out var path, "path") ? path : "",
                        success = r.Succeeded(),
                        details = r.Result,
                    }
                );
            }
            async Task Until(Func<bool> done, string label, int limit, int chunk = 5)
            {
                for (int ticks = 0; ticks <= limit; ticks += chunk)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (ticks < limit)
                    {
                        var r = await ctx.Game.StepTicksAsync(
                            chunk,
                            cancellationToken: cancellationToken
                        );
                        if (!r.Success)
                            throw new InvalidOperationException("Native playback: " + r.Status);
                    }
                }
                await On(() => Record("timeout-" + label));
                throw new InvalidOperationException("Timed out: " + label);
            }
        }
        catch (Exception e)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = e.ToString() });
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        proof.environment.details = new
                        {
                            states,
                            flightEvents = flightEvents.ToArray(),
                            gameplayMvid = typeof(Building_Watchtower)
                                .Assembly
                                .ManifestModule
                                .ModuleVersionId,
                        };
                        probe.UnpatchAll(probe.Id);
                        observedShooter = null;
                        launches.Clear();
                        impacts.Clear();
                        shots.Clear();
                        draws.Clear();
                        trackedIds.Clear();
                        flightEvents.Clear();
                        FogSettings.TowerShootingAllowed = oldShooting;
                        FogSettings.AISmart = oldAI;
                        Prefs.PauseOnLoad = oldPause;
                    },
                    CancellationToken.None
                );
                if (staged)
                {
                    var r = await ctx.Tools.CallAsync(
                        "rimworld/load_game_ready",
                        new
                        {
                            saveName,
                            readiness = "visual",
                            pauseIfNeeded = true,
                        },
                        cancellationToken: CancellationToken.None
                    );
                    proof.assertions.Add(
                        RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", r)
                    );
                }
            }
            finally
            {
                gate.Release();
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;
    }

    private static float Injury(Pawn pawn) =>
        pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
}
