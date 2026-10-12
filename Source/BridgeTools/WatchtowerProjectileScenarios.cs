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

/// <summary>Native ladder-side collisions and completed explosive flight.</summary>
public sealed class WatchtowerProjectileScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Projectile observed;
    private static readonly List<string> impacts = new();
    private static int spinDraws;
    private static bool spinMatches;

    private static void ObserveRotation(Projectile projectile, Quaternion __result)
    {
        if (projectile == observed && projectile.def.defName == "ZombieBall")
        {
            spinDraws++;
            spinMatches &= Quaternion.Angle(__result, projectile.ExactRotation) < .001f;
        }
    }

    private static void ObserveImpact(Projectile __instance, Thing hitThing)
    {
        if (__instance == observed)
            impacts.Add(hitThing?.ThingID ?? "ground");
    }

    [Tool(
        "totalfog/watchtower_projectiles",
        Description = "Check native shots at a partly climbed actor from both ladder sides, tower interception and completed frag-grenade flight. Restore the unchanged paused named fixture; reject Multiplayer."
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
        var proof = RimBridgeEvidence.CreateManifest(
            "totalfog/watchtower_projectiles",
            evidenceLabel
        );
        proof.saveName = saveName;
        var probe = new Harmony("brrainz.totalfog.watchtower-projectile-probe");
        var states = new List<object>();
        var oldAI = FogSettings.AISmart;
        var oldShooting = FogSettings.TowerShootingAllowed;
        var oldPause = Prefs.PauseOnLoad;
        bool staged = false;
        Map map = null;
        Building_Watchtower tower = null;
        Pawn climber = null,
            enemy = null;
        int grenadeSourceHp = 0;
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
                var cell = GenRadial
                    .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                    .First(c =>
                        new CellRect(c.x - 5, c.z - 13, 11, 27).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !p.Roofed(map)
                            && !p.Fogged(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                foreach (var p in new CellRect(cell.x - 5, cell.z - 13, 11, 27))
                foreach (var thing in p.GetThingList(map).ToArray())
                    thing.Destroy(DestroyMode.Vanish);
                staged = true;
                FogSettings.AISmart = false;
                FogSettings.TowerShootingAllowed = false;
                Prefs.PauseOnLoad = true;
                tower = (Building_Watchtower)
                    ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, ThingDefOf.WoodLog);
                tower.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(tower, cell, map, Rot4.North);
                Rand.PushState(831012);
                try
                {
                    climber = AddPawn(tower.Approach, Faction.OfPlayer);
                    enemy = AddPawn(tower.Position + IntVec3.South * 10, Faction.OfAncientsHostile);
                }
                finally
                {
                    Rand.PopState();
                }
                enemy.equipment.AddEquipment(
                    (ThingWithComps)
                        ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"))
                );
                climber.equipment.AddEquipment(
                    (ThingWithComps)
                        ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"))
                );
                enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                climber.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
                );
                probe.Patch(
                    AccessTools.Method(typeof(Projectile), "Impact"),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerProjectileScenarios),
                        nameof(ObserveImpact)
                    )
                );
                probe.Patch(
                    AccessTools.Method(typeof(TowerCombat), nameof(TowerCombat.DrawingRotation)),
                    postfix: new HarmonyMethod(
                        typeof(WatchtowerProjectileScenarios),
                        nameof(ObserveRotation)
                    )
                );
            });
            await Until(() => tower.Elevation >= .4f, "partial native climb", 150);
            await On(() =>
            {
                Check(
                    "native climber is still on exterior ladder",
                    tower.Elevation < .7f && !tower.Watching && climber.Position == tower.Approach
                );
                Record("partial-climb");
                Check(
                    "tower base blocks incoming shot from behind ladder",
                    !enemy.TryGetAttackVerb(climber).CanHitTarget(climber)
                );
                Building_Watchtower.Move(enemy, tower.Position + IntVec3.North * 10);
                Check(
                    "open ladder side allows incoming shot",
                    enemy.TryGetAttackVerb(climber).CanHitTarget(climber)
                );
                Building_Watchtower.Move(enemy, tower.Position + IntVec3.South * 10);
                impacts.Clear();
                observed = Launch("Bullet_AssaultRifle", enemy, climber);
            });
            int baseHp = await ctx.MainThread.InvokeAsync(() => tower.HitPoints, cancellationToken);
            float health = await ctx.MainThread.InvokeAsync(
                () => Injury(climber),
                cancellationToken
            );
            await Until(() => observed.Destroyed, "native rear shot impact", 100);
            await On(() =>
            {
                Record("partial-impact");
                Check(
                    "native rear bullet hits solid tower",
                    impacts.Contains(tower.ThingID) && tower.HitPoints < baseHp
                );
                Check("solid tower protects partial climber", Injury(climber) == health);
            });
            await Until(() => tower.Watching, "completed native climb", 150);
            if (DefDatabase<ThingDef>.GetNamedSilentFail("ZombieBall") != null)
            {
                await On(() =>
                {
                    observed = Launch("ZombieBall", enemy, climber);
                    spinDraws = 0;
                    spinMatches = true;
                    Check(
                        "native spitter ball keeps its own spinning rotation",
                        Quaternion.Angle(
                            TowerCombat.DrawingRotation(observed),
                            observed.ExactRotation
                        ) < .001f
                    );
                    spinDraws = 0;
                });
                var framed = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = tower.Position.x - 5,
                        z = tower.Position.z - 13,
                        width = 11,
                        height = 27,
                        rootSize = 18f,
                    },
                    cancellationToken: cancellationToken
                );
                Check("frame native spitter flight", framed.Succeeded());
                await ctx.Game.FramesAsync(8, cancellationToken);
                await On(() =>
                {
                    Check(
                        "actual native spitter drawing retains rotation override",
                        spinDraws > 0 && spinMatches
                    );
                    observed.Destroy(DestroyMode.Vanish);
                    observed = null;
                });
            }
            await On(() =>
            {
                Check(
                    "completed platform is exposed across its own base",
                    enemy.TryGetAttackVerb(climber).CanHitTarget(climber)
                );
                FogSettings.TowerShootingAllowed = true;
                Check(
                    "completed platform can shoot across its own base",
                    climber.TryGetAttackVerb(enemy).CanHitTarget(enemy)
                );
                impacts.Clear();
                grenadeSourceHp = tower.HitPoints;
                observed = Launch("Proj_GrenadeFrag", climber, enemy);
                Record("native-grenade-launch");
            });
            float enemyHealth = await ctx.MainThread.InvokeAsync(
                () => Injury(enemy),
                cancellationToken
            );
            await Until(() => observed.Destroyed, "native grenade completes flight and fuse", 300);
            await Step(20);
            await On(() =>
            {
                Record("native-grenade-impact");
                Check(
                    "native explosive flight reaches ground target",
                    enemy.Dead || Injury(enemy) > enemyHealth
                );
                Check(
                    "outgoing explosive leaves source tower intact",
                    tower.Spawned && tower.HitPoints == grenadeSourceHp
                );
            });

            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, faction);
                GenSpawn.Spawn(p, cell, map);
                if (p.drafter != null)
                    p.drafter.Drafted = true;
                return p;
            }
            Projectile Launch(string definition, Pawn source, Pawn target)
            {
                var projectile = (Projectile)
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(definition));
                GenSpawn.Spawn(projectile, source.Position, map);
                projectile.Launch(source, source.DrawPos, target, target, ProjectileHitFlags.All);
                return projectile;
            }
            void Record(string phase) =>
                states.Add(
                    new
                    {
                        phase,
                        elevation = tower.Elevation,
                        tower.Watching,
                        climber = climber.Position.ToString(),
                        enemy = enemy.Position.ToString(),
                        tower.HitPoints,
                        impacts = impacts.ToArray(),
                        nativePosition = observed?.ExactPosition.ToString(),
                        projection = observed == null
                            ? null
                            : TowerCombat.DrawingOffset(observed).ToString(),
                    }
                );
            void Check(string name, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            async Task Step(int ticks)
            {
                var r = await ctx.Game.StepTicksAsync(ticks, cancellationToken: cancellationToken);
                if (!r.Success)
                    throw new InvalidOperationException("Tick stepping: " + r.Status);
            }
            async Task Until(Func<bool> done, string label, int limit)
            {
                for (int i = 0; i <= limit; i += 5)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (i < limit)
                        await Step(5);
                }
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
                            gameplayMvid = typeof(Building_Watchtower)
                                .Assembly
                                .ManifestModule
                                .ModuleVersionId,
                        };
                        probe.UnpatchAll(probe.Id);
                        observed = null;
                        impacts.Clear();
                        FogSettings.AISmart = oldAI;
                        FogSettings.TowerShootingAllowed = oldShooting;
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
