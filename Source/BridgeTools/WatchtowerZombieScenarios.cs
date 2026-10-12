using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native zombie structure attacks and spitter landings around an occupied tower.</summary>
public sealed class WatchtowerZombieScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Pawn spitter;
    private static IntVec3 spitTarget;

    private static bool TargetPrefix(JobDriver __instance, ref IntVec3 __result)
    {
        if (__instance.pawn != spitter)
            return true;
        // Only target selection is supplied; Shoot, flight, impact, explosion
        // and zombie placement use their actual native implementations.
        __result = spitTarget;
        return false;
    }

    [Tool(
        "totalfog/watchtower_zombies",
        Description = "Check a real occupied tower against native zombie melee eligibility, wall pushing with a real wall control, native structure damage and real spitter Shoot/flight/impact at tower-center and ladder targets. Target selection is supplied, not natural spitter AI. Restore scoped settings/probes and the unchanged paused paired fixture."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_zombies", evidenceLabel);
        proof.saveName = saveName;
        var probe = new Harmony("brrainz.totalfog.watchtower-zombie-probe");
        var settings = new List<(object group, FieldInfo field, object value)>();
        var rows = new List<object>();
        var oldAI = FogSettings.AISmart;
        var oldShooting = FogSettings.TowerShootingAllowed;
        bool staged = false;
        Map map = null;
        Building_Watchtower tower = null;
        Pawn lookout = null;
        try
        {
            await On(() =>
            {
                map = Find.CurrentMap;
                if (
                    !ModsConfig.IsActive("brrainz.zombieland")
                    || map?.GetVisibility()?.Initialized != true
                    || !Find.TickManager.Paused
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use a paused paired single-player fixture."
                    );
                var center = map.AllCells.First(c =>
                    new CellRect(c.x - 5, c.z - 5, 22, 11).All(p =>
                        p.InBounds(map)
                        && p.Standable(map)
                        && !p.Roofed(map)
                        && p.GetThingList(map).All(t => t is Plant or Filth)
                    )
                );
                foreach (var p in new CellRect(center.x - 5, center.z - 5, 22, 11))
                {
                    map.fogGrid.Unfog(p);
                    foreach (var thing in p.GetThingList(map).ToArray())
                        thing.Destroy(DestroyMode.Vanish);
                }
                staged = true;
                FogSettings.AISmart = false;
                FogSettings.TowerShootingAllowed = false;
                var settingsType = AccessTools.TypeByName("ZombieLand.ZombieSettings");
                var groups = new List<object>
                {
                    AccessTools.Field(settingsType, "Values").GetValue(null),
                };
                foreach (
                    var frame in (IEnumerable)
                        AccessTools.Field(settingsType, "ValuesOverTime").GetValue(null)
                )
                    groups.Add(AccessTools.Field(frame.GetType(), "values").GetValue(frame));
                foreach (var group in groups.Distinct())
                {
                    Change(group, "minimumZombiesForWallPushing", 1);
                    Change(group, "smashOnlyWhenAgitated", false);
                    Change(
                        group,
                        "smashMode",
                        Enum.Parse(
                            AccessTools.Field(group.GetType(), "smashMode").FieldType,
                            "AnyBuilding"
                        )
                    );
                }
                probe.Patch(
                    AccessTools.Method(AccessTools.TypeByName("ZombieLand.Zombie"), "CustomTick"),
                    prefix: new HarmonyMethod(
                        typeof(ZombielandEffectsScenarios),
                        "SuppressMobileFixtureCleanup"
                    )
                );
                probe.Patch(
                    AccessTools.Method(
                        AccessTools.TypeByName("ZombieLand.JobDriver_Spitter"),
                        "TryFindNewTarget"
                    ),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerZombieScenarios),
                        nameof(TargetPrefix)
                    )
                );
                tower = (Building_Watchtower)
                    ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, ThingDefOf.WoodLog);
                tower.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(tower, center, map, Rot4.South);
                lookout = PawnGenerator.GeneratePawn(
                    new PawnGenerationRequest(
                        PawnKindDefOf.Colonist,
                        Faction.OfPlayer,
                        canGeneratePawnRelations: false,
                        mustBeCapableOfViolence: true
                    )
                );
                GenSpawn.Spawn(lookout, tower.Approach, map);
                lookout.drafter.Drafted = true;
                lookout.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
                );
            });
            await Until(() => tower.Watching, "native climb", 240);
            Pawn zombie = null;
            var handler = AccessTools.TypeByName("ZombieLand.ZombieStateHandler");
            var gridType = AccessTools.TypeByName("ZombieLand.PheromoneGrid");
            await On(() =>
            {
                zombie = SpawnZombie(tower.Approach, "Normal");
                var grid = map.components.Single(c => c.GetType() == gridType);
                var checkPush = AccessTools.Method(handler, "CheckWallPushing");
                Check(
                    "zombie ground melee cannot target elevated lookout",
                    !zombie.TryGetAttackVerb(lookout).CanHitTarget(lookout)
                );
                Check(
                    "zombie wall push rejects solid tower destination",
                    !(bool)checkPush.Invoke(null, new[] { (object)zombie, grid })
                );
                Check(
                    "rejected tower push leaves zombie on ground",
                    (float)AccessTools.Field(zombie.GetType(), "wallPushProgress").GetValue(zombie)
                        < 0
                );
                var wallCell = tower.Position + IntVec3.East * 10;
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                GenSpawn.Spawn(wall, wallCell, map);
                Building_Watchtower.Move(zombie, wallCell + IntVec3.South);
                Check(
                    "same native wall push accepts ordinary wall",
                    (bool)checkPush.Invoke(null, new[] { (object)zombie, grid })
                );
                Check(
                    "ordinary wall push starts its native progress",
                    (float)AccessTools.Field(zombie.GetType(), "wallPushProgress").GetValue(zombie)
                        == 0
                );
                AccessTools.Field(zombie.GetType(), "wallPushProgress").SetValue(zombie, -1f);
                wall.Destroy(DestroyMode.Vanish);
                Building_Watchtower.Move(zombie, tower.Approach);
                foreach (string kind in new[] { "Normal", "TankyOperator", "SuicideBomber" })
                {
                    var actor =
                        kind == "Normal"
                            ? zombie
                            : SpawnZombie(tower.Approach + IntVec3.East * 3, kind);
                    Check(
                        kind + " can damage the native tower structure",
                        (bool)
                            AccessTools
                                .Method(handler, "CanSmashBuilding")
                                .Invoke(
                                    null,
                                    new object[] { actor, tower, false, Faction.OfPlayer }
                                )
                    );
                    if (actor != zombie)
                        actor.Destroy(DestroyMode.Vanish);
                }
                // Supply the structure target to Zombieland's own job handoff;
                // native AttackStatic, verb, damage and the building remain live.
                AccessTools
                    .Method(handler, "AttackThing")
                    .Invoke(null, new object[] { zombie, tower, JobDefOf.AttackStatic });
            });
            int hp = await ctx.MainThread.InvokeAsync(() => tower.HitPoints, cancellationToken);
            await Until(() => tower.HitPoints < hp, "native zombie structure impact", 600);
            await On(() =>
            {
                Check(
                    "native zombie attack damages structure",
                    tower.HitPoints < hp && tower.Spawned
                );
                Check(
                    "structure attack keeps one raised native lookout",
                    tower.Occupant == lookout && lookout.Spawned && tower.Watching
                );
                rows.Add(
                    new
                    {
                        phase = "native-structure-damage",
                        before = hp,
                        after = tower.HitPoints,
                        zombieJob = zombie.CurJob?.def.defName,
                    }
                );
                zombie.Destroy(DestroyMode.Vanish);
            });
            var spawn = await ctx.Tools.CallAsync(
                "zombieland/spawn_spitter_visual_fixture",
                new
                {
                    x = tower.Position.x + 12,
                    z = tower.Position.z,
                    aggressive = false,
                },
                cancellationToken: cancellationToken
            );
            Check("native spitter fixture", spawn.Succeeded());
            await On(() =>
            {
                spitter = map.mapPawns.AllPawnsSpawned.Single(p =>
                    p.GetType().FullName == "ZombieLand.ZombieSpitter"
                );
                AccessTools.Field(spitter.GetType(), "tickCounter").SetValue(spitter, 10000);
            });
            foreach (bool center in new[] { true, false })
            {
                Projectile ball = null;
                HashSet<Pawn> before = null;
                await On(() =>
                {
                    spitTarget = center ? tower.Position : tower.Approach;
                    before = new HashSet<Pawn>(map.mapPawns.AllPawnsSpawned);
                    var projectiles = map.listerThings.AllThings.OfType<Projectile>().ToArray();
                    Check(
                        "native spitter Shoot creates ball " + center,
                        (bool)
                            AccessTools
                                .Method(spitter.jobs.curDriver.GetType(), "Shoot")
                                .Invoke(spitter.jobs.curDriver, null)
                    );
                    ball = map
                        .listerThings.AllThings.OfType<Projectile>()
                        .Single(p => p.def.defName == "ZombieBall" && !projectiles.Contains(p));
                    Check(
                        "native spitter cell target retains ground trajectory " + center,
                        TowerCombat.DrawingOffset(ball) == UnityEngine.Vector3.zero
                    );
                });
                await Until(() => ball.Destroyed, "native spitter impact", 300);
                await Step(30);
                await On(() =>
                {
                    var born = map
                        .mapPawns.AllPawnsSpawned.Where(p =>
                            !before.Contains(p) && p.GetType().FullName == "ZombieLand.Zombie"
                        )
                        .ToArray();
                    rows.Add(
                        new
                        {
                            phase = center ? "spit-center" : "spit-ladder",
                            target = spitTarget.ToString(),
                            tower.Spawned,
                            tower.HitPoints,
                            born = born.Select(p => new
                                {
                                    p.ThingID,
                                    cell = p.Position.ToString(),
                                    walkable = p.Position.Walkable(map),
                                })
                                .ToArray(),
                        }
                    );
                    Check("spitter impact preserves tower " + center, tower.Spawned);
                    Check(
                        "spitter impact preserves native elevated occupant " + center,
                        tower.Occupant == lookout && lookout.Spawned && tower.Watching
                    );
                    Check(
                        "spitter lands one zombie on accessible ground " + center,
                        born.Length == 1
                            && born[0].Position.Walkable(map)
                            && !tower.OccupiedRect().Contains(born[0].Position)
                    );
                    foreach (var pawn in born)
                        pawn.Destroy(DestroyMode.Vanish);
                });
            }

            Pawn bomber = null;
            int bomberTowerHp = 0;
            await On(() =>
            {
                bomber = SpawnZombie(tower.Approach, "SuicideBomber");
                bomberTowerHp = tower.HitPoints;
                // Native adjacent-building evaluation arms the bomber. Only
                // shorten its fuse; death, queued explosion and damage stay native.
                AccessTools.Method(handler, "CanSmash").Invoke(null, new object[] { bomber });
                Check(
                    "native tower evaluation arms suicide bomber",
                    (bool)AccessTools.Field(bomber.GetType(), "bombWillGoOff").GetValue(bomber)
                );
                AccessTools.Field(bomber.GetType(), "bombTickingInterval").SetValue(bomber, .1f);
            });
            // Zombieland's scheduler and queued explosions run in ordinary
            // playback, outside the single-tick stepping path.
            var bomberPlayback = await ctx.Game.RunForTicksAsync(
                180,
                new RimBridgeRunTicksOptions { TimeoutMs = 30000 },
                cancellationToken
            );
            if (!bomberPlayback.Success)
                throw new InvalidOperationException(
                    "Native bomber playback: " + bomberPlayback.Message
                );
            await On(() =>
            {
                Check(
                    "native bomber fuse completes during ordinary playback",
                    bomber.Dead || bomber.Destroyed
                );
                Check(
                    "native bomber explosion damages tower",
                    tower.Destroyed || tower.HitPoints < bomberTowerHp
                );
                var body = lookout.Dead ? (Thing)lookout.Corpse : lookout;
                Check(
                    "bomber leaves exactly one native lookout or corpse",
                    body?.Spawned == true && map.listerThings.AllThings.Count(t => t == body) == 1
                );
                Check(
                    "destroyed tower releases raised actor",
                    !tower.Destroyed
                        || TowerPresentation.TowerFor(body) == null && body.Position.Walkable(map)
                );
                rows.Add(
                    new
                    {
                        phase = "native-bomber",
                        tower.Destroyed,
                        tower.HitPoints,
                        before = bomberTowerHp,
                        lookout.Dead,
                        body = body?.ThingID,
                        cell = body?.Position.ToString(),
                        offset = TowerPresentation.Offset(body).ToString(),
                    }
                );
            });

            Pawn SpawnZombie(IntVec3 cell, string kind) =>
                (Pawn)
                    AccessTools
                        .Method(
                            AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions"),
                            "SpawnZombie"
                        )
                        .Invoke(
                            null,
                            new object[]
                            {
                                cell,
                                map,
                                Enum.Parse(AccessTools.TypeByName("ZombieLand.ZombieType"), kind),
                                true,
                            }
                        );
            void Change(object group, string name, object value)
            {
                var field = AccessTools.Field(group.GetType(), name);
                settings.Add((group, field, field.GetValue(group)));
                field.SetValue(group, value);
            }
            void Check(string name, bool ok)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, ok));
                if (!ok)
                    throw new InvalidOperationException(name);
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            async Task Step(int ticks)
            {
                var result = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!result.Success)
                    throw new InvalidOperationException("Native tick step: " + result.Status);
            }
            async Task Until(Func<bool> done, string label, int limit)
            {
                for (int tick = 0; tick <= limit; tick += 5)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (tick < limit)
                        await Step(5);
                }
                throw new InvalidOperationException("Timed out: " + label);
            }
        }
        catch (Exception exception)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = exception.ToString() });
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
                            rows,
                            gameplayMvid = typeof(Building_Watchtower)
                                .Assembly
                                .ManifestModule
                                .ModuleVersionId,
                        };
                        probe.UnpatchAll(probe.Id);
                        spitter = null;
                        foreach (var item in settings)
                            item.field.SetValue(item.group, item.value);
                        FogSettings.AISmart = oldAI;
                        FogSettings.TowerShootingAllowed = oldShooting;
                    },
                    CancellationToken.None
                );
                if (staged)
                {
                    var reload = await ctx.Tools.CallAsync(
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
                        RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
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
