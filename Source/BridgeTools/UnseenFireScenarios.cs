using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Combat;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class UnseenFireScenarios
{
    [Tool(
        "totalfog/unseen_fire",
        Description = "Compare native silent and audible shots against an unseen enemy. Verify a cell investigation job, hidden shooter and real movement. Reload the unchanged paused fixture."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/unseen_fire");
        proof.saveName = saveName;
        var owned = new List<Thing>();
        var rows = new List<object>();
        bool oldSmart = FogSettings.AISmart,
            oldOutside = FogSettings.OnlyOutsideColony;
        int oldRange = FogSettings.BaseViewRange;
        Pawn target = null;
        Map map = null;
        IntVec3 start = IntVec3.Invalid,
            destination = IntVec3.Invalid;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    if (
                        map?.GetVisibility().Initialized != true
                        || !Find.TickManager.Paused
                        || Compatibility.MultiplayerIntegration.Active
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized single-player fixture."
                        );
                    var origin = map.AllCells.First(c =>
                        c.x > 5
                        && c.z > 5
                        && c.x < map.Size.x - 35
                        && c.z < map.Size.z - 12
                        && new CellRect(c.x, c.z, 32, 10).All(p =>
                            p.Standable(map) && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    FogSettings.AISmart = true;
                    FogSettings.BaseViewRange = 8;
                    FogSettings.OnlyOutsideColony = false;
                    var fog = map.GetVisibility();
                    foreach (var cell in new CellRect(origin.x, origin.z, 32, 10))
                        map.fogGrid.Unfog(cell);
                    var shooter = PawnGenerator.GeneratePawn(
                        new PawnGenerationRequest(
                            PawnKindDefOf.Colonist,
                            Faction.OfPlayer,
                            canGeneratePawnRelations: false,
                            mustBeCapableOfViolence: true
                        )
                    );
                    owned.Add(GenSpawn.Spawn(shooter, origin + new IntVec3(28, 0, 5), map));
                    shooter.equipment.DestroyAllEquipment();
                    shooter.equipment.AddEquipment(
                        (ThingWithComps)
                            ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_AssaultRifle"))
                    );
                    shooter.jobs.StartJob(
                        JobMaker.MakeJob(JobDefOf.Wait, 2000),
                        JobCondition.InterruptForced
                    );
                    var verb = shooter.equipment.Primary.GetComp<CompEquippable>().PrimaryVerb;
                    var props = verb.verbProps;
                    var cast = props.soundCast;
                    var tail = props.soundCastTail;
                    var beam = props.soundCastBeam;
                    foreach (string phase in new[] { "silent", "audible" })
                    {
                        if (target?.Spawned == true)
                            target.Destroy(DestroyMode.Vanish);
                        target = PawnGenerator.GeneratePawn(
                            new PawnGenerationRequest(
                                PawnKindDefOf.Colonist,
                                Faction.OfAncientsHostile,
                                canGeneratePawnRelations: false,
                                mustBeCapableOfViolence: true
                            )
                        );
                        owned.Add(GenSpawn.Spawn(target, origin + new IntVec3(3, 0, 5), map));
                        target.jobs.StartJob(
                            JobMaker.MakeJob(JobDefOf.Wait, 2000),
                            JobCondition.InterruptForced
                        );
                        target.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                        var shotCell = shooter.Position;
                        int targetIndex = map.cellIndices.CellToIndex(target.Position);
                        fog.IncrementSeen(Faction.OfPlayer, targetIndex);
                        bool started;
                        try
                        {
                            props.soundCast = phase == "silent" ? null : cast;
                            props.soundCastTail = phase == "silent" ? null : tail;
                            props.soundCastBeam = phase == "silent" ? null : beam;
                            verb.Reset();
                            started = verb.TryStartCastOn(target);
                            if (started)
                                verb.WarmupComplete();
                            verb.Reset();
                        }
                        finally
                        {
                            props.soundCast = cast;
                            props.soundCastTail = tail;
                            props.soundCastBeam = beam;
                            fog.DecrementSeen(Faction.OfPlayer, targetIndex);
                        }
                        bool heard = UnseenFire.HasMemory(target);
                        var combat = new ProbeCombat();
                        var job = combat.Decide(target);
                        Check(phase + " firing started", started);
                        Check(phase + " memory", heard == (phase == "audible"));
                        Check(phase + " investigation", (job != null) == (phase == "audible"));
                        Check(
                            phase + " shooter remains hidden",
                            !Visibility.AllowsTarget(target, shooter.Position)
                        );
                        if (job != null)
                        {
                            Check(
                                "native cell job uses the rough firing area",
                                !job.targetA.HasThing && job.targetA.Cell.InHorDistOf(shotCell, 4)
                            );
                            destination = job.targetA.Cell;
                            start = target.Position;
                            shooter.Position += new IntVec3(0, 0, 4);
                            shooter.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                            var remembered = UnseenFire.InvestigationJob(
                                target,
                                null,
                                IntVec3.Invalid,
                                999999
                            );
                            Check(
                                "native memory does not follow the shooter",
                                remembered?.targetA.Cell == destination
                            );
                            target.jobs.StartJob(job, JobCondition.InterruptForced);
                        }
                        rows.Add(
                            new
                            {
                                phase,
                                started,
                                heard,
                                job = job?.def.defName,
                                cell = job?.targetA.Cell.ToString(),
                            }
                        );
                    }
                    foreach (
                        var projectile in map
                            .listerThings.AllThings.OfType<Projectile>()
                            .Where(p => p.Launcher == shooter)
                            .ToArray()
                    )
                        projectile.Destroy(DestroyMode.Vanish);
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    proof.environment.details = new
                    {
                        rows,
                        gameplayMvid = typeof(MapVisibility)
                            .Assembly
                            .ManifestModule
                            .ModuleVersionId,
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => m.PackageId)
                            .ToArray(),
                    };
                },
                cancellationToken
            );
            var ticks = await ctx.Game.RunForTicksAsync(
                45,
                new RimBridgeRunTicksOptions
                {
                    ForceNormalSpeed = true,
                    PauseWhenDone = true,
                    TimeoutMs = 15000,
                },
                cancellationToken
            );
            Check("native movement ticks completed", ticks.Success);
            await ctx.MainThread.InvokeAsync(
                () =>
                    Check(
                        "enemy moves toward the heard area",
                        target?.Spawned == true
                            && target.Position.DistanceToSquared(destination)
                                < start.DistanceToSquared(destination)
                    ),
                cancellationToken
            );
        }
        catch (Exception error)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy(DestroyMode.Vanish);
                    FogSettings.AISmart = oldSmart;
                    FogSettings.OnlyOutsideColony = oldOutside;
                    FogSettings.BaseViewRange = oldRange;
                },
                CancellationToken.None
            );
            if (map != null)
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
        RimBridgeEvidence.Complete(proof);
        return proof;

        void Check(string name, bool passed) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, passed));
    }

    private sealed class ProbeCombat : JobGiver_AIFightEnemies
    {
        public Job Decide(Pawn pawn) => base.TryGiveJob(pawn);
    }
}
