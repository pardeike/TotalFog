using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Unity.Collections;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Observes real native producers; probes never replace a combat result.</summary>
public sealed class WatchtowerCombatScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Pawn lookout,
        enemy;
    private static Thing attachment,
        mote;
    private static int drawAttempts,
        raisedLaunches;
    private static readonly List<object> launches = new();
    private static readonly List<object> culls = new();
    private static int outgoing,
        incoming,
        bulletDraws,
        muzzleDraws,
        attachmentDraws,
        moteDraws,
        rotatedDraws,
        groundedShadows;

    private static void ObserveRotation(Projectile projectile, Quaternion __result)
    {
        if (
            (projectile.Launcher == lookout || projectile.Launcher == enemy)
            && Quaternion.Angle(__result, projectile.ExactRotation) > 1f
        )
            Interlocked.Increment(ref rotatedDraws);
    }

    private static void ObserveShadow(Projectile __instance, Vector3 drawLoc)
    {
        if (
            (__instance.Launcher == lookout || __instance.Launcher == enemy)
            && (drawLoc - __instance.DrawPos).MagnitudeHorizontal() < .001f
        )
            Interlocked.Increment(ref groundedShadows);
    }

    private static void ObserveLaunch(Projectile __instance, Thing launcher)
    {
        if (launcher == lookout)
            outgoing++;
        if (launcher == enemy)
            incoming++;
        if (launcher == lookout || launcher == enemy)
        {
            var loc = __instance.DrawPos;
            var raised = loc;
            TowerCombat.DrawPrefix(__instance, ref raised);
            if ((raised - loc).MagnitudeHorizontal() > .01f)
                raisedLaunches++;
            launches.Add(
                new
                {
                    pawn = launcher.ThingID,
                    projectile = __instance.ThingID,
                    drawer = __instance.def.drawerType.ToString(),
                    __instance.Spawned,
                    cell = __instance.Position.ToString(),
                    sourceVisible = Visibility.IsVisible(__instance),
                    native = loc.ToString(),
                    projected = raised.ToString(),
                    elevation = TowerPresentation.TowerFor(lookout)?.Elevation,
                    watching = TowerPresentation.TowerFor(lookout)?.Watching,
                    nativeRegistered = (
                        (List<Thing>)
                            AccessTools
                                .Field(typeof(DynamicDrawManager), "drawThings")
                                .GetValue(__instance.Map.dynamicDrawManager)
                    ).Contains(__instance),
                }
            );
        }
    }

    private static void ObserveProjectileDraw(Projectile __instance, Vector3 drawLoc)
    {
        if (__instance.Launcher != lookout && __instance.Launcher != enemy)
            return;
        Interlocked.Increment(ref drawAttempts);
        if ((drawLoc - __instance.DrawPos).MagnitudeHorizontal() > .05f)
            Interlocked.Increment(ref bulletDraws);
    }

    private static void ObserveCull(
        NativeArray<DynamicDrawManager.ThingCullDetails> details,
        List<Thing> ___drawThings
    )
    {
        if (culls.Count >= 12)
            return;
        for (int i = 0; i < details.Length; i++)
            if (___drawThings[i] is Projectile p && (p.Launcher == lookout || p.Launcher == enemy))
                culls.Add(
                    new
                    {
                        p.ThingID,
                        details[i].shouldDraw,
                        details[i].shouldDrawShadow,
                        cell = details[i].cell.ToString(),
                        visible = Visibility.IsVisible(p),
                        nativePosition = p.DrawPos.ToString(),
                    }
                );
    }

    private static void ObserveFleck(ref FleckStatic __instance, bool __runOriginal)
    {
        if (!__runOriginal || __instance.def != FleckDefOf.ShotFlash || lookout == null)
            return;
        if (
            __instance.spawnPosition.ToIntVec3() == lookout.Position
            && TowerPresentation.FleckPosition(ref __instance).z > __instance.DrawPos.z + 1f
        )
            Interlocked.Increment(ref muzzleDraws);
    }

    private static void ObserveAttachment(Thing __instance, DrawPhase phase, Vector3 drawLoc)
    {
        if (
            __instance == attachment
            && phase == DrawPhase.Draw
            && drawLoc.z > lookout.Position.z + 1f
        )
            Interlocked.Increment(ref attachmentDraws);
    }

    private static void ObserveMote(Mote __instance)
    {
        if (__instance == mote && __instance.DrawPos.z > lookout.Position.z + 1f)
            Interlocked.Increment(ref moteDraws);
    }

    [Tool(
        "totalfog/watchtower_combat",
        Description = "Observe native tower weapon bursts, ground observer targeting, blockers, placement graphics, attachments and lightning. Restore the paused named save; reject Multiplayer."
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
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_combat", evidenceLabel);
        proof.saveName = saveName;
        var probe = new Harmony("brrainz.totalfog.watchtower-combat-probe");
        var oldShooting = FogSettings.TowerShootingAllowed;
        var oldAI = FogSettings.AISmart;
        var oldPause = Prefs.PauseOnLoad;
        Map map = null;
        CellRect area = default;
        Building_Watchtower tower = null,
            second = null;
        Thing wall = null;
        Pawn secondLookout = null;
        bool staged = false;
        int weightedStrikes = 0;
        int woodHp = 0,
            stoneHp = 0,
            stoneControlHp = 0;
        Thing stoneControl = null;
        float woodHealth = 0,
            stoneHealth = 0;
        try
        {
            await On(() =>
            {
                map = Find.CurrentMap;
                if (
                    map?.GetVisibility()?.Initialized != true
                    || !Find.TickManager.Paused
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use a paused initialized single-player fixture."
                    );
                var origin = GenRadial
                    .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                    .First(c =>
                        new CellRect(c.x, c.z, 28, 24).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !p.Roofed(map)
                            && !p.Fogged(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                area = new CellRect(origin.x, origin.z, 28, 24);
                foreach (var c in area)
                foreach (var t in c.GetThingList(map).ToArray())
                    t.Destroy(DestroyMode.Vanish);
                staged = true;
                Prefs.PauseOnLoad = true;
                FogSettings.TowerShootingAllowed = false;
                FogSettings.AISmart = true;
                outgoing = incoming = bulletDraws = muzzleDraws = attachmentDraws = moteDraws = 0;
                drawAttempts = raisedLaunches = 0;
                rotatedDraws = groundedShadows = 0;
                launches.Clear();
                culls.Clear();
                WatchtowerEffectProbe.Install(probe);
                Find.Selector.ClearSelection();
                Rand.PushState(874002);
                try
                {
                    tower = AddTower(
                        new IntVec3(area.minX + 5, 0, area.minZ + 7),
                        ThingDefOf.WoodLog
                    );
                    second = AddTower(
                        new IntVec3(area.minX + 21, 0, area.minZ + 7),
                        DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                    );
                    lookout = AddPawn(tower.Approach + IntVec3.East * 2, Faction.OfPlayer);
                    secondLookout = AddPawn(second.Approach + IntVec3.East * 2, Faction.OfPlayer);
                    enemy = AddPawn(tower.Position + IntVec3.North * 12, Faction.OfAncientsHostile);
                    foreach (var p in new[] { lookout, enemy })
                        p.equipment.AddEquipment(
                            (ThingWithComps)
                                ThingMaker.MakeThing(
                                    DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle")
                                )
                        );
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 10000));
                    for (int i = 0; i < 4; i++)
                    {
                        var bp = GenConstruct.PlaceBlueprintForBuild(
                            WatchtowerDefOf.TotalFog_Watchtower,
                            new IntVec3(area.minX + 3 + i * 6, 0, area.minZ + 21),
                            map,
                            new Rot4(i),
                            Faction.OfPlayer,
                            i % 2 == 0 ? ThingDefOf.WoodLog : second.Stuff
                        );
                        Check(
                            "native rotated blueprint " + i,
                            bp.Graphic is Graphic_WatchtowerPreview
                                && bp.OccupiedRect().Count() == 9
                                && bp.Rotation.AsInt == i
                        );
                        Check(
                            "native ghost retains tower graphic " + i,
                            GhostUtility.GhostGraphicFor(
                                bp.Graphic,
                                WatchtowerDefOf.TotalFog_Watchtower,
                                Color.green
                            ) is Graphic_WatchtowerPreview
                        );
                    }
                }
                finally
                {
                    Rand.PopState();
                }
                Check("stuff affects native health", tower.MaxHitPoints != second.MaxHitPoints);
                Check(
                    "stone has lower native flammability",
                    second.GetStatValue(StatDefOf.Flammability)
                        < tower.GetStatValue(StatDefOf.Flammability)
                );
                map.roofGrid.SetRoof(tower.Position, RoofDefOf.RoofConstructed);
                Check("tower refuses roof", !tower.Position.Roofed(map));
                probe.Patch(
                    AccessTools.Method(
                        typeof(Projectile),
                        nameof(Projectile.Launch),
                        new[]
                        {
                            typeof(Thing),
                            typeof(Vector3),
                            typeof(LocalTargetInfo),
                            typeof(LocalTargetInfo),
                            typeof(ProjectileHitFlags),
                            typeof(bool),
                            typeof(Thing),
                            typeof(ThingDef),
                        }
                    ),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveLaunch)
                    )
                );
                probe.Patch(
                    AccessTools.Method(typeof(Projectile), "DrawAt"),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveProjectileDraw)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                probe.Patch(
                    AccessTools.Method(typeof(TowerCombat), nameof(TowerCombat.DrawingRotation)),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveRotation)
                    )
                );
                probe.Patch(
                    AccessTools.Method(typeof(Projectile), "DrawShadow"),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveShadow)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                probe.Patch(
                    AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveCull)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                probe.Patch(
                    AccessTools.Method(
                        typeof(FleckStatic),
                        nameof(FleckStatic.Draw),
                        new[] { typeof(float), typeof(DrawBatch) }
                    ),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveFleck)
                    )
                );
                probe.Patch(
                    AccessTools.Method(typeof(Thing), nameof(Thing.DynamicDrawPhaseAt)),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveAttachment)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                probe.Patch(
                    AccessTools.Method(typeof(Mote), "DrawAt"),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerCombatScenarios),
                        nameof(ObserveMote)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
                foreach (var pair in new[] { (lookout, tower), (secondLookout, second) })
                    pair.Item1.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, pair.Item2)
                    );
            });
            await Until(() => tower.Watching && second.Watching, "platform access", 600);
            await Frame();
            await On(() =>
            {
                wall = AddWall(tower.Position + IntVec3.North * 5, ThingDefOf.Wall);
                var source = enemy.TryGetComp<CompFog>().FieldOfViewWatcher;
                source.UpdateFoV(true);
                Check(
                    "ground enemy sees exposed lookout over wall",
                    Visibility.AllowsTarget(enemy, lookout.Position)
                );
                Check(
                    "ground enemy retains ordinary hidden ground",
                    !WatchtowerGameplayScenarios.Sees(source, tower.Position + IntVec3.South * 2)
                );
                Check(
                    "native enemy acquisition finds lookout",
                    AttackTargetFinder
                        .BestShootTargetFromCurrentPosition(
                            enemy,
                            TargetScanFlags.NeedLOSToAll,
                            t => t == lookout
                        )
                        ?.Thing == lookout
                );
                Check(
                    "native incoming firing eligibility",
                    enemy.TryGetAttackVerb(lookout).CanHitTarget(lookout)
                );
                Check(
                    "platform shooting disabled",
                    !lookout.TryGetAttackVerb(enemy).TryStartCastOn(enemy)
                );
                FogSettings.TowerShootingAllowed = true;
            });
            await Until(() => outgoing > 0, "automatic native lookout weapon fires", 500, true);
            await On(() => Check("actual native weapon emitted projectile", outgoing > 0));
            await On(() =>
            {
                FogSettings.TowerShootingAllowed = false;
                lookout.TryGetAttackVerb(enemy).Reset();
                lookout.stances.CancelBusyStanceHard();
                if (enemy.Dead || enemy.Downed)
                {
                    enemy = AddPawn(tower.Position + IntVec3.North * 12, Faction.OfAncientsHostile);
                    enemy.equipment.AddEquipment(
                        (ThingWithComps)
                            ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"))
                    );
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 10000));
                }
                enemy.TryGetAttackVerb(lookout).TryStartCastOn(lookout);
            });
            await Until(() => incoming > 0, "native ground weapon fires", 500, true);
            await On(() =>
            {
                Check("actual incoming weapon emitted projectile", incoming > 0);
                proof.environment.details = new
                {
                    outgoing,
                    incoming,
                    drawAttempts,
                    raisedLaunches,
                    bulletDraws,
                    muzzleDraws,
                    launches,
                    culls,
                };
                Check("native bullet drawing is raised", bulletDraws > 0);
                Check("native shot flash drawing is raised", muzzleDraws > 0);
                FogSettings.TowerShootingAllowed = false;
                var melee = enemy.meleeVerbs.TryGetMeleeVerb(lookout);
                Check("ground melee cannot reach upper platform", !melee.CanHitTarget(lookout));
                wall.Destroy(DestroyMode.Vanish);
                wall = AddWall(
                    tower.Position + IntVec3.North * 5,
                    DefDatabase<ThingDef>.GetNamed("Granite")
                );
                Check(
                    "natural rock blocks elevated shooting",
                    !lookout.TryGetAttackVerb(enemy).CanHitTarget(enemy)
                );
                wall.Destroy(DestroyMode.Vanish);
                var blockingTower = AddTower(
                    tower.Position + IntVec3.North * 6,
                    ThingDefOf.WoodLog
                );
                Check(
                    "another tower blocks elevated shooting",
                    !lookout.TryGetAttackVerb(enemy).CanHitTarget(enemy)
                );
                blockingTower.Destroy(DestroyMode.Vanish);
                var roofCell = tower.Position + IntVec3.North * 5;
                map.roofGrid.SetRoof(roofCell, RoofDefOf.RoofConstructed);
                Check(
                    "room roof blocks elevated shooting",
                    !lookout.TryGetAttackVerb(enemy).CanHitTarget(enemy)
                );
                map.roofGrid.SetRoof(roofCell, null);
                // UI mesh refresh must never consume simulation randomness.
                Rand.PushState(49201);
                float expected = Rand.Value;
                Rand.PopState();
                Rand.PushState(49201);
                tower.RebuildWallJoins();
                float actual = Rand.Value;
                Rand.PopState();
                Check("native wall drawing preserves RNG", expected == actual);
                mote = MoteMaker.MakeAttachedOverlay(
                    lookout,
                    DefDatabase<ThingDef>.GetNamed("FlashShieldBreak"),
                    Vector3.zero,
                    solidTimeOverride: 60
                );
            });
            await Frame();
            await Capture("combat-and-blueprints");
            await On(() => WatchtowerEffectProbe.CompareNativeEffects(lookout, enemy, Check));
            await ctx.Game.FramesAsync(4, cancellationToken);
            await Capture("native-impact-effects");
            await Step(3);
            await ctx.Game.FramesAsync(4, cancellationToken);
            await On(() => WatchtowerEffectProbe.VerifyPlayback(Check));
            var visualProjectiles = new List<Projectile>();
            await On(() =>
            {
                // Native launches give a horizontal projected flight and an
                // arcing grenade with a real ground shadow. Leave them paused
                // while native drawing executes, then remove these controls.
                Building_Watchtower.Move(enemy, tower.Position + IntVec3.East * 10);
                foreach (var definition in new[] { "Bullet_AssaultRifle", "Proj_GrenadeFrag" })
                {
                    var p = (Projectile)
                        ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(definition));
                    GenSpawn.Spawn(p, tower.Position, map);
                    p.Launch(lookout, lookout.DrawPos, enemy, enemy, ProjectileHitFlags.All);
                    visualProjectiles.Add(p);
                    var nativePosition = p.ExactPosition;
                    var nativeRotation = p.ExactRotation;
                    var drawn = nativePosition;
                    TowerCombat.DrawPrefix(p, ref drawn);
                    Check(
                        "projection preserves native position and rotation " + definition,
                        p.ExactPosition == nativePosition
                            && p.ExactRotation == nativeRotation
                            && drawn.z > nativePosition.z + 1f
                    );
                    Check(
                        "projected projectile has raised culling bounds " + definition,
                        TowerPresentation.CullBounds(p).minZ > p.OccupiedDrawRect().minZ
                    );
                }
            });
            await ctx.Game.FramesAsync(20, cancellationToken);
            await On(() =>
            {
                Check(
                    "actual horizontal projectile drawing uses raised flight rotation",
                    rotatedDraws > 0
                );
                Check("actual arcing projectile shadow stays on ground", groundedShadows > 0);
                foreach (var p in visualProjectiles)
                    p.Destroy(DestroyMode.Vanish);
            });
            await On(() =>
            {
                Check("actual attached mote drawing is raised", moteDraws > 0);
                Check("ordinary ground pawn retains fire eligibility", enemy.FlammableNow);
                Check("raised lookout is not protected by tower footprint", lookout.FlammableNow);
                var groundCell = enemy.Position;
                enemy.Position = tower.Position;
                Check("ground actor retains solid-building fire protection", !enemy.FlammableNow);
                enemy.Position = groundCell;
                FireUtility.TryAttachFire(lookout, .2f, enemy);
                attachment = lookout.GetAttachment(ThingDefOf.Fire);
                Check("native fire attaches to lookout", attachment != null);
            });
            await ctx.Game.FramesAsync(20, cancellationToken);
            await On(() => Check("actual attached fire drawing is raised", attachmentDraws > 0));
            await Capture("attached-fire");
            await On(() =>
            {
                woodHp = tower.HitPoints;
                stoneHp = second.HitPoints;
                stoneControl = GenSpawn.Spawn(
                    ThingMaker.MakeThing(ThingDefOf.Wall, second.Stuff),
                    new IntVec3(area.minX + 20, 0, area.minZ + 15),
                    map
                );
                stoneControlHp = stoneControl.HitPoints;
                woodHealth = lookout.health.summaryHealth.SummaryHealthPercent;
                stoneHealth = secondLookout.health.summaryHealth.SummaryHealthPercent;
                Mesh bolt = null;
                Rand.PushState(9341);
                try
                {
                    WeatherEvent_LightningStrike.DoStrike(tower.Position, map, ref bolt);
                    WeatherEvent_LightningStrike.DoStrike(second.Position, map, ref bolt);
                    WeatherEvent_LightningStrike.DoStrike(stoneControl.Position, map, ref bolt);
                }
                finally
                {
                    Rand.PopState();
                }
            });
            // Native explosions propagate on ticks. Match the stone damage
            // against an ordinary native wall made from the same material.
            await Step(10);
            await On(() =>
            {
                Check(
                    "native lightning damages wood tower",
                    tower.Destroyed || tower.HitPoints < woodHp
                );
                Check(
                    "stone retains native flame resistance",
                    !second.Destroyed
                        && !stoneControl.Destroyed
                        && Math.Abs(
                            (stoneHp - second.HitPoints) - (stoneControlHp - stoneControl.HitPoints)
                        ) <= 1
                        && stoneHp - second.HitPoints < woodHp - tower.HitPoints
                );
                Check(
                    "native lightning damages wood occupant",
                    lookout.Dead || lookout.health.summaryHealth.SummaryHealthPercent < woodHealth
                );
                Check(
                    "native lightning damages stone occupant",
                    secondLookout.Dead
                        || secondLookout.health.summaryHealth.SummaryHealthPercent < stoneHealth
                );
                var fixedCell = enemy.Position;
                WatchtowerPatches.LightningPrefix(ref fixedCell, map);
                Check("forced strike location retained", fixedCell == enemy.Position);
                int chosen = 0;
                Rand.PushState(97132);
                try
                {
                    for (int i = 0; i < 2000; i++)
                    {
                        var cell = IntVec3.Invalid;
                        WatchtowerPatches.LightningPrefix(ref cell, map);
                        if (cell.IsValid)
                            chosen++;
                    }
                }
                finally
                {
                    Rand.PopState();
                }
                Check("ordinary-event tower weighting is bounded", chosen > 0 && chosen < 100);
                weightedStrikes = chosen;
                proof.environment.details = new
                {
                    area = area.ToString(),
                    outgoing,
                    incoming,
                    bulletDraws,
                    muzzleDraws,
                    moteDraws,
                    attachmentDraws,
                    weightedStrikes = chosen,
                };
            });

            Building_Watchtower AddTower(IntVec3 cell, ThingDef stuff)
            {
                var t = (Building_Watchtower)
                    ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, stuff);
                t.SetFaction(Faction.OfPlayer);
                return (Building_Watchtower)GenSpawn.Spawn(t, cell, map);
            }
            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, faction);
                GenSpawn.Spawn(p, cell, map);
                if (p.drafter != null)
                    p.drafter.Drafted = true;
                return p;
            }
            Thing AddWall(IntVec3 cell, ThingDef def)
            {
                var t = ThingMaker.MakeThing(def, def.MadeFromStuff ? ThingDefOf.WoodLog : null);
                return GenSpawn.Spawn(t, cell, map);
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            void Check(string name, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }
            async Task Step(int ticks)
            {
                var result = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!result.Success)
                    throw new InvalidOperationException("Tick stepping: " + result.Status);
            }
            async Task Until(Func<bool> done, string label, int limit, bool render = false)
            {
                int stride = render ? 1 : 5;
                for (int elapsed = 0; elapsed <= limit; elapsed += stride)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (elapsed < limit)
                        await Step(stride);
                    if (render)
                        await ctx.Game.FramesAsync(4, cancellationToken);
                }
                throw new InvalidOperationException("Timed out: " + label);
            }
            async Task Frame()
            {
                var framed = await ctx.Tools.CallAsync(
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
                if (!framed.Succeeded())
                    throw new InvalidOperationException("Frame combat fixture.");
                await ctx.Game.FramesAsync(10, cancellationToken);
                await Step(14);
                await ctx.Game.FramesAsync(10, cancellationToken);
            }
            async Task Capture(string label)
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
                        fileName = evidenceLabel + "-" + label,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                proof.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + label, r));
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = label,
                        kind = "cell_rect",
                        path = r.TryReadResult<string>(out var path, "path") ? path : "",
                        success = r.Succeeded(),
                        details = r.Result,
                    }
                );
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
                            outgoing,
                            incoming,
                            drawAttempts,
                            raisedLaunches,
                            bulletDraws,
                            muzzleDraws,
                            attachmentDraws,
                            moteDraws,
                            rotatedDraws,
                            groundedShadows,
                            weightedStrikes,
                            launches,
                            culls,
                            effects = WatchtowerEffectProbe.Receipt,
                            camera = Find.CameraDriver.CurrentViewRect.ToString(),
                        };
                        probe.UnpatchAll(probe.Id);
                        WatchtowerEffectProbe.Reset();
                        lookout = enemy = null;
                        attachment = mote = null;
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
}
