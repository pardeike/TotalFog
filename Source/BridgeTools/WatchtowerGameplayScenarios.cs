using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Core;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class WatchtowerGameplayScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/watchtower_gameplay",
        Description = "Run production tower access, interruptions, sight and native projectile controls in the paused named fixture. Capture all rotations and restore the unchanged save. Reject a connected Multiplayer session."
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
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_gameplay", evidenceLabel);
        proof.saveName = saveName;
        var owned = new List<Thing>();
        var towers = new List<Building_Watchtower>();
        var pawns = new List<Pawn>();
        var states = new List<object>();
        Map map = null;
        CellRect area = default;
        Pawn helper = null,
            enemy = null;
        Building_Bed bed = null;
        var oldShooting = FogSettings.TowerShootingAllowed;
        var oldHighlight = Prefs.DotHighlightDisplayMode;
        bool staged = false;
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
                FogSettings.TowerShootingAllowed = false;
                Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.None;
                Find.Selector.ClearSelection();
                Rand.PushState(731009);
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var tower = (Building_Watchtower)
                            ThingMaker.MakeThing(
                                WatchtowerDefOf.TotalFog_Watchtower,
                                i % 2 == 0
                                    ? ThingDefOf.WoodLog
                                    : DefDatabase<ThingDef>.GetNamed("BlocksGranite")
                            );
                        owned.Add(tower);
                        tower.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(
                            tower,
                            new IntVec3(area.minX + 3 + i * 6, 0, area.minZ + 8),
                            map,
                            new Rot4(i)
                        );
                        towers.Add(tower);
                        pawns.Add(
                            AddPawn(
                                tower.Approach + tower.Rotation.FacingCell * 2,
                                Faction.OfPlayer
                            )
                        );
                    }
                    helper = AddPawn(
                        new IntVec3(area.minX + 12, 0, area.minZ + 2),
                        Faction.OfPlayer
                    );
                    enemy = AddPawn(
                        towers[0].Position + new IntVec3(0, 0, 12),
                        Faction.OfAncientsHostile
                    );
                    enemy.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 10000));
                    bed = (Building_Bed)ThingMaker.MakeThing(ThingDefOf.Bed, ThingDefOf.WoodLog);
                    owned.Add(bed);
                    bed.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(bed, new IntVec3(area.minX + 17, 0, area.minZ + 2), map);
                    bed.Medical = true;
                }
                finally
                {
                    Rand.PopState();
                }
                Check(
                    "four native solid footprints",
                    towers.All(t =>
                        t.OccupiedRect().Count() == 9
                        && t.OccupiedRect().All(c => c.GetEdifice(map) == t && !c.Walkable(map))
                    )
                );
                Check(
                    "native breach predicates",
                    towers.All(t =>
                        BreachingUtility.ShouldBreachBuilding(t)
                        && BreachingUtility.BlocksBreaching(map, t.Position)
                    )
                );
                var wall = AddWall(towers[0].Approach);
                Check("blocked ladder refuses entry", !towers[0].CanEnter(pawns[0]));
                wall.Destroy(DestroyMode.Vanish);
                for (int i = 0; i < 4; i++)
                    StartWatch(pawns[i], towers[i]);
            });
            await Until(
                "all four begin ascent",
                () => towers.All(t => t.Occupant != null && t.Elevation > 0 && t.Elevation < 1)
            );
            await Step(30);
            await On(() =>
            {
                Check(
                    "ascent keeps logical ladder approach",
                    towers
                        .Select(
                            (t, i) =>
                                pawns[i].Position == t.Approach
                                && t.Elevation > .1f
                                && t.Elevation < .7f
                                && pawns[i].Rotation == t.Rotation.Opposite
                        )
                        .All(v => v)
                );
                Record("ascent");
            });
            await Capture("climbing", 12);
            await Until("all four occupy platform", () => towers.All(t => t.Watching));
            await On(() =>
            {
                Check(
                    "one spawned registered pawn per platform",
                    towers
                        .Select(
                            (t, i) =>
                                pawns[i].Spawned
                                && pawns[i].Position == t.Position
                                && map.thingGrid.ThingsListAt(t.Position).Contains(pawns[i])
                                && t.Occupant == pawns[i]
                                && t.Elevation == 1
                        )
                        .All(v => v)
                );
                Check(
                    "native DrawPos stays in ground coordinates outside rendering",
                    pawns.All(p =>
                        (p.DrawPos - p.Position.ToVector3Shifted()).MagnitudeHorizontal() < .01f
                    )
                );
                Check(
                    "all four projection offsets",
                    pawns.All(p =>
                        Math.Abs(TowerPresentation.Offset(p).z - 1.65f) < .001f
                        && Math.Abs(TowerPresentation.Offset(p).x) < .001f
                    )
                );
                Check(
                    "occupied towers cannot be deconstructed",
                    !new WorkGiver_Deconstruct().HasJobOnThing(helper, towers[0], true)
                );
                Check("other pawn cannot take occupied platform", !towers[0].CanEnter(helper));
                Find.Selector.Select(pawns[0]);
                Record("platform");
            });
            await Capture("occupied", 12);
            await Capture("occupied-wide", 35);
            await On(() =>
            {
                pawns[0]
                    .jobs.TryTakeOrderedJob(
                        JobMaker.MakeJob(
                            JobDefOf.Goto,
                            towers[0].Approach + towers[0].Rotation.FacingCell * 3
                        )
                    );
            });
            await Until(
                "drafted move schedules descent",
                () => pawns[0].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerDescend
            );
            await Step(30);
            await On(() =>
            {
                Check(
                    "interruption descends in time, not teleport",
                    pawns[0].Position == towers[0].Position
                        && towers[0].Elevation > .1f
                        && towers[0].Elevation < .95f
                );
                Record("interrupted-descent");
            });
            await Until(
                "move resumes after descent",
                () => towers[0].Occupant == null && pawns[0].CurJobDef == JobDefOf.Goto
            );
            await On(() => StartWatch(pawns[0], towers[0]));
            await Until("lookout returns", () => towers[0].Watching);
            await On(() =>
            {
                var source = pawns[0].TryGetComp<CompFog>().FieldOfViewWatcher;
                for (int x = -2; x <= 2; x++)
                    AddWall(towers[0].Position + new IntVec3(x, 0, 5));
                source.UpdateFoV(true);
                Check("elevated sight crosses freestanding wall", Sees(source, enemy.Position));
                var roofCell = towers[0].Position + new IntVec3(0, 0, 7);
                map.roofGrid.SetRoof(roofCell, RoofDefOf.RoofConstructed);
                source.UpdateFoV(true);
                Check("roof cell is not revealed", !Sees(source, roofCell));
                Check("room roof blocks beyond", !Sees(source, enemy.Position));
                map.roofGrid.SetRoof(roofCell, null);
                source.UpdateFoV(true);
                Check("roof removal refreshes elevated sight", Sees(source, enemy.Position));
                var rifle = (ThingWithComps)
                    ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_BoltActionRifle"));
                pawns[0].equipment.AddEquipment(rifle);
                var verb = pawns[0].TryGetAttackVerb(enemy);
                Check("shooting disabled by default", !verb.TryStartCastOn(enemy));
                FogSettings.TowerShootingAllowed = true;
                Check("native elevated target eligibility", verb.CanHitTarget(enemy));
                var projectile = (Projectile)ThingMaker.MakeThing(verb.verbProps.defaultProjectile);
                owned.Add(projectile);
                GenSpawn.Spawn(projectile, pawns[0].Position, map);
                projectile.Launch(pawns[0], pawns[0].DrawPos, enemy, enemy, ProjectileHitFlags.All);
                Record("outgoing-shot");
            });
            float before = await ctx.MainThread.InvokeAsync(
                () => enemy.health.summaryHealth.SummaryHealthPercent,
                cancellationToken
            );
            await Step(60);
            await On(() =>
                Check(
                    "native outgoing bullet crosses wall and damages ground pawn",
                    enemy.Dead || enemy.health.summaryHealth.SummaryHealthPercent < before
                )
            );
            await On(() =>
            {
                var verb = pawns[0].TryGetAttackVerb(enemy);
                var projectile = (Projectile)ThingMaker.MakeThing(verb.verbProps.defaultProjectile);
                owned.Add(projectile);
                GenSpawn.Spawn(projectile, enemy.Position, map);
                projectile.Launch(enemy, enemy.DrawPos, pawns[0], pawns[0], ProjectileHitFlags.All);
            });
            before = await ctx.MainThread.InvokeAsync(
                () => pawns[0].health.summaryHealth.SummaryHealthPercent,
                cancellationToken
            );
            await Step(60);
            await On(() =>
                Check(
                    "native incoming bullet crosses wall and damages lookout",
                    pawns[0].Dead || pawns[0].health.summaryHealth.SummaryHealthPercent < before
                )
            );
            await On(() =>
            {
                FogSettings.TowerShootingAllowed = false;
                // Use an untouched lookout for incapacity/rescue controls.
                HealthUtility.DamageUntilDowned(pawns[1]);
                Check(
                    "downed lookout stays rescuable at top",
                    pawns[1].Downed
                        && !pawns[1].Dead
                        && pawns[1].Position == towers[1].Position
                        && towers[1].Occupant == pawns[1]
                );
                var workgiver = new WorkGiver_RescueDowned();
                Check(
                    "native rescue discovers ladder route",
                    workgiver.HasJobOnThing(helper, pawns[1], true)
                );
                var rescue = workgiver.JobOnThing(helper, pawns[1], true);
                Check(
                    "native rescue uses tower retrieval job",
                    rescue.def == WatchtowerDefOf.TotalFog_WatchtowerRetrieve
                );
                helper.jobs.TryTakeOrderedJob(rescue);
            });
            await Until("ladder rescue completes native bed handoff", () => pawns[1].InBed(), 4000);
            await On(() =>
            {
                Check(
                    "one rescued pawn in bed",
                    pawns[1].CurrentBed() == bed
                        && towers[1].Occupant == null
                        && helper.carryTracker.CarriedThing == null
                );
                var victim = pawns[2];
                towers[2].Destroy(DestroyMode.KillFinalize);
                Check(
                    "collapse ejects pawn and removes tower ownership",
                    !towers[2].Spawned
                        && TowerPresentation.TowerFor(victim) == null
                        && (victim.Dead || victim.Position.Walkable(map))
                );
                Check(
                    "collapse inflicts native injury",
                    victim.Dead || victim.health.hediffSet.hediffs.Any(h => h is Hediff_Injury)
                );
                Record("rescue-collapse");
            });

            Pawn AddPawn(IntVec3 cell, Faction faction)
            {
                var p = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, faction);
                owned.Add(p);
                GenSpawn.Spawn(p, cell, map);
                if (p.drafter != null)
                    p.drafter.Drafted = true;
                return p;
            }
            Thing AddWall(IntVec3 cell)
            {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                owned.Add(wall);
                GenSpawn.Spawn(wall, cell, map);
                return wall;
            }
            void Check(string name, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(name, value));
                if (!value)
                    throw new InvalidOperationException(name);
            }
            void Record(string phase)
            {
                states.Add(
                    new
                    {
                        phase,
                        tick = Find.TickManager.TicksGame,
                        towers = towers
                            .Select(t => new
                            {
                                t.ThingID,
                                t.Spawned,
                                elevation = t.Elevation,
                                watching = t.Watching,
                                occupant = t.Occupant?.ThingID,
                                cell = t.Position.ToString(),
                                approach = t.Spawned ? t.Approach.ToString() : null,
                                sightRange = t
                                    .Occupant?.TryGetComp<CompFog>()
                                    ?.FieldOfViewWatcher?.LastSightRange,
                                nativeDraw = t.Occupant?.DrawPos.ToString(),
                                visualOffset = t.Occupant == null
                                    ? null
                                    : TowerPresentation.Offset(t.Occupant).ToString(),
                            })
                            .ToArray(),
                    }
                );
                proof.environment.details = new
                {
                    productionTower = true,
                    area = area.ToString(),
                    states,
                };
            }
            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            async Task Step(int ticks)
            {
                var result = await ctx.Game.StepTicksAsync(
                    ticks,
                    cancellationToken: cancellationToken
                );
                if (!result.Success)
                    throw new InvalidOperationException("Tick stepping: " + result.Status);
            }
            async Task Until(string label, Func<bool> done, int limit = 1500)
            {
                for (int elapsed = 0; elapsed <= limit; elapsed += 10)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                        return;
                    if (elapsed < limit)
                        await Step(10);
                }
                await On(() => Record("failed-" + label));
                throw new InvalidOperationException("Timed out: " + label);
            }
            async Task Capture(string label, float zoom)
            {
                var frame = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ + 3,
                        width = 28,
                        height = 12,
                        rootSize = zoom,
                    },
                    cancellationToken: cancellationToken
                );
                if (!frame.Succeeded())
                    throw new InvalidOperationException("Frame " + label);
                await ctx.Game.FramesAsync(12, cancellationToken);
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ + 3,
                        width = 28,
                        height = 12,
                        rootSize = zoom,
                        paddingCells = 0,
                        fileName = evidenceLabel + "-" + label,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                proof.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + label, shot));
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = label,
                        kind = "cell_rect",
                        path = shot.TryReadResult<string>(out var path, "path") ? path : "",
                        success = shot.Succeeded(),
                        details = shot.Result,
                    }
                );
                if (!shot.Succeeded())
                    throw new InvalidOperationException("Capture " + label);
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
                        foreach (var tower in towers)
                            if (tower.Spawned)
                                tower.Destroy(DestroyMode.Vanish);
                        foreach (var thing in owned)
                            if (!thing.Destroyed)
                                thing.Destroy(DestroyMode.Vanish);
                        FogSettings.TowerShootingAllowed = oldShooting;
                        Prefs.DotHighlightDisplayMode = oldHighlight;
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

    private static void StartWatch(Pawn pawn, Building_Watchtower tower) =>
        pawn.jobs.TryTakeOrderedJob(
            JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower)
        );

    private static bool Sees(CompSightSource source, IntVec3 cell) =>
        (
            (VisibilityMask)AccessTools.Field(typeof(CompSightSource), "current").GetValue(source)
        ).Contains(source.parent.Map.cellIndices.CellToIndex(cell));
}
