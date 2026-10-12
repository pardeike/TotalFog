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

/// <summary>Native emergencies while carrying a patient down a ladder.</summary>
public sealed class WatchtowerSafetyScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Pawn watchedCarrier,
        watchedPatient;
    private static int carriedDraws;

    private static void ObserveCarried(Pawn __instance, DrawPhase phase, Vector3 drawLoc)
    {
        if (__instance != watchedPatient || phase != DrawPhase.Draw || watchedCarrier == null)
            return;
        var bonus = TowerPresentation.Offset(watchedCarrier);
        var ground = watchedCarrier.Drawer.DrawPos;
        if (TowerPresentation.DrawingDepth > 0)
            ground -= bonus;
        if (drawLoc.z > ground.z + bonus.z * .8f)
            carriedDraws++;
    }

    [Tool(
        "totalfog/watchtower_safety",
        Description = "Check native collapse, downing and death during ladder retrieval, carried-pawn drawing and single-body cleanup. Restore the unchanged paused named save; reject Multiplayer."
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
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_safety", evidenceLabel);
        proof.saveName = saveName;
        var probe = new Harmony("brrainz.totalfog.watchtower-safety-probe");
        var states = new List<object>();
        Map map = null;
        CellRect area = default;
        bool staged = false;
        bool oldPause = Prefs.PauseOnLoad;
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
                foreach (var cell in area)
                foreach (var thing in cell.GetThingList(map).ToArray())
                    thing.Destroy(DestroyMode.Vanish);
                staged = true;
                Prefs.PauseOnLoad = true;
                probe.Patch(
                    AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                    prefix: new HarmonyMethod(
                        typeof(WatchtowerSafetyScenarios),
                        nameof(ObserveCarried)
                    )
                    {
                        priority = Priority.Last,
                    }
                );
            });
            for (int i = 0; i < 3; i++)
            {
                var name = new[] { "collapse", "downed", "death" }[i];
                Building_Watchtower tower = null;
                Pawn patient = null,
                    carrier = null;
                await On(() =>
                {
                    tower = (Building_Watchtower)
                        ThingMaker.MakeThing(
                            WatchtowerDefOf.TotalFog_Watchtower,
                            ThingDefOf.WoodLog
                        );
                    tower.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(
                        tower,
                        new IntVec3(area.minX + 4 + i * 9, 0, area.minZ + 15),
                        map
                    );
                    patient = AddPawn(tower.Approach + IntVec3.East * 2);
                    carrier = AddPawn(tower.Approach + IntVec3.West * 2);
                    patient.jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
                    );
                });
                await Until(() => tower.ProvidesSight, "patient reaches deck");
                await On(() => HealthUtility.DamageUntilDowned(patient));
                await Step(5);
                await On(() =>
                {
                    Check(
                        name + " patient downed on deck",
                        patient.Downed && !patient.Dead && tower.Occupant == patient
                    );
                    var job = JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerRetrieve, tower);
                    job.targetC = patient;
                    carrier.jobs.TryTakeOrderedJob(job);
                });
                await Until(
                    () =>
                        carrier.carryTracker.CarriedThing == patient
                        && (
                            name == "death"
                                ? tower.Elevation > .99f
                                : tower.Elevation > .5f && tower.Elevation < .8f
                        ),
                    "carrying descent"
                );
                float injury = await ctx.MainThread.InvokeAsync(
                    () => Injury(patient),
                    cancellationToken
                );
                if (i == 0)
                {
                    await On(() =>
                    {
                        watchedCarrier = carrier;
                        watchedPatient = patient;
                        carriedDraws = 0;
                    });
                    var framed = await ctx.Tools.CallAsync(
                        "rimworld/frame_cell_rect",
                        new
                        {
                            x = tower.Position.x - 4,
                            z = tower.Position.z - 4,
                            width = 9,
                            height = 9,
                            rootSize = 12f,
                        },
                        cancellationToken: cancellationToken
                    );
                    Check("frame carrying control", framed.Succeeded());
                    await ctx.Game.FramesAsync(6, cancellationToken);
                    await On(() =>
                        Check(
                            "actual carried patient draws with carrier elevation",
                            carriedDraws > 0
                        )
                    );
                }
                await On(() =>
                {
                    var height = tower.Elevation;
                    if (name == "collapse")
                        tower.Destroy(DestroyMode.KillFinalize);
                    else if (name == "downed")
                        HealthUtility.DamageUntilDowned(carrier);
                    else
                        carrier.Kill(null);
                    var body = patient.Dead ? (Thing)patient.Corpse : patient;
                    states.Add(
                        new
                        {
                            name,
                            height,
                            patient.Dead,
                            patient.Spawned,
                            bodyCell = body?.Position.ToString(),
                            bodySpawned = body?.Spawned,
                            patientInjuryBefore = injury,
                            patientInjuryAfter = Injury(patient),
                            carrierDead = carrier.Dead,
                            carrierDowned = carrier.Downed,
                            carrierCell = carrier.Position.ToString(),
                            carrying = carrier.carryTracker?.CarriedThing?.ThingID,
                            towerSpawned = tower.Spawned,
                            occupant = tower.Occupant?.ThingID,
                            rescuer = tower.Rescuer?.ThingID,
                        }
                    );
                    Check(
                        name + " drops exactly one native patient onto walkable ground",
                        body?.Spawned == true
                            && body.Position.Walkable(map)
                            && carrier.carryTracker?.CarriedThing == null
                            && area.SelectMany(c => c.GetThingList(map)).Count(t => t == body) == 1
                            && TowerPresentation.TowerFor(body) == null
                    );
                    Check(
                        name + " dropped patient receives height-scaled fall injury",
                        patient.Dead || Injury(patient) > injury + .01f
                    );
                    if (name == "collapse")
                        Check(
                            "collapse releases carrier ownership",
                            TowerPresentation.TowerFor(carrier.Dead ? carrier.Corpse : carrier)
                                == null
                        );
                    else if (name == "death")
                        Check(
                            "dead carrier retains one rescuable corpse on deck",
                            tower.Occupant == carrier
                                && carrier.Corpse?.Spawned == true
                                && carrier.Corpse.Position == tower.Position
                        );
                    else
                        Check(
                            "partly descended downed carrier reaches ground",
                            carrier.Downed
                                && !carrier.Dead
                                && carrier.Position.Walkable(map)
                                && !tower.HasActor(carrier)
                        );
                });
            }
            Pawn AddPawn(IntVec3 cell)
            {
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(pawn, cell, map);
                pawn.drafter.Drafted = true;
                return pawn;
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            void Check(string name, bool passed)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, passed));
                if (!passed)
                    throw new InvalidOperationException(name);
            }
            async Task Step(int ticks)
            {
                var receipt = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!receipt.Success)
                    throw new InvalidOperationException("Tick stepping: " + receipt.Status);
            }
            async Task Until(Func<bool> done, string label)
            {
                for (int ticks = 0; ticks < 2000; ticks++)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    await Step(1);
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
                        proof.environment.details = new { states, carriedDraws };
                        probe.UnpatchAll(probe.Id);
                        watchedCarrier = watchedPatient = null;
                        Prefs.PauseOnLoad = oldPause;
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

    private static float Injury(Pawn pawn) =>
        pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(h => h.Severity);
}
