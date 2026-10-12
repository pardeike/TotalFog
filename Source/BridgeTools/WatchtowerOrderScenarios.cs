using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Native facing, float-menu orders, timers and adjacent wall links.</summary>
public sealed class WatchtowerOrderScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/watchtower_orders",
        Description = "Check all four ladder facings, native drafted/undrafted orders, timed watch completion and persistence, and wood/granite wall links. Restore the unchanged paused named save. Reject Multiplayer."
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
        var proof = RimBridgeEvidence.CreateManifest("totalfog/watchtower_orders", evidenceLabel);
        proof.saveName = saveName;
        var towers = new List<Building_Watchtower>();
        var pawns = new List<Pawn>();
        var states = new List<object>();
        string temporarySave = "TotalFog_Tower_Orders_" + evidenceLabel;
        string temporaryPath = null;
        bool staged = false;
        bool oldPause = Prefs.PauseOnLoad;
        Map map = null;
        CellRect area = default;
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
                    throw new InvalidOperationException("Use a paused single-player fixture.");
                string path = GenFilePaths.FilePathForSavedGame(temporarySave);
                if (File.Exists(path))
                    throw new InvalidOperationException("Temporary fixture save already exists.");
                temporaryPath = path;
                var origin = GenRadial
                    .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                    .First(c =>
                        new CellRect(c.x, c.z, 28, 16).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !p.Roofed(map)
                            && !p.Fogged(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                area = new CellRect(origin.x, origin.z, 28, 16);
                staged = true;
                Prefs.PauseOnLoad = true;
                foreach (var cell in area)
                foreach (var thing in cell.GetThingList(map).ToArray())
                    thing.Destroy(DestroyMode.Vanish);
                Rand.PushState(941013);
                try
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var stuff =
                            i % 2 == 0
                                ? ThingDefOf.WoodLog
                                : DefDatabase<ThingDef>.GetNamed("BlocksGranite");
                        var tower = (Building_Watchtower)
                            ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, stuff);
                        tower.SetFaction(Faction.OfPlayer);
                        GenSpawn.Spawn(
                            tower,
                            new IntVec3(area.minX + 4 + i * 6, 0, area.minZ + 8),
                            map,
                            new Rot4(i)
                        );
                        towers.Add(tower);
                        var pawn = PawnGenerator.GeneratePawn(
                            PawnKindDefOf.Colonist,
                            Faction.OfPlayer
                        );
                        GenSpawn.Spawn(pawn, tower.Approach, map);
                        pawn.drafter.Drafted = true;
                        pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
                        if (pawn.needs.food != null)
                            pawn.needs.food.CurLevel = .09f;
                        if (pawn.needs.rest != null)
                            pawn.needs.rest.CurLevel = 1;
                        pawns.Add(pawn);
                        Check(
                            "native wall flags on footprint " + i,
                            tower
                                .OccupiedRect()
                                .All(c => (map.linkGrid.LinkFlagsAt(c) & LinkFlags.Wall) != 0)
                        );
                        foreach (var direction in GenAdj.CardinalDirections)
                        {
                            if (direction == tower.Rotation.FacingCell)
                                continue;
                            var wall = ThingMaker.MakeThing(ThingDefOf.Wall, stuff);
                            wall.SetFaction(Faction.OfPlayer);
                            GenSpawn.Spawn(wall, tower.Position + direction * 2, map);
                            Check(
                                "native wall links into tower " + i + " " + direction,
                                wall.Graphic is Graphic_Linked linked
                                    && linked.ShouldLinkWith(wall.Position - direction, wall)
                            );
                        }
                        Check(
                            "drafted menu has hold and three durations " + i,
                            Options(pawn, tower).Length == 4
                        );
                        Choose(pawn, tower, "TotalFog_TowerWatchDrafted".Translate());
                        Check(
                            "drafted hold uses normal ordered job " + i,
                            pawn.CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerWatch
                                && pawn.CurJob.count == 0
                        );
                    }
                }
                finally
                {
                    Rand.PopState();
                }
            });
            await Until(
                () => towers.All(t => t.Elevation > .1f && t.Elevation < .9f),
                "partial ascent"
            );
            await On(() => CheckFacing("ascent"));
            await Capture("ascent");
            await Until(() => towers.All(t => t.Watching), "drafted hold");
            await Step(10);
            await On(() =>
                Check("hungry drafted pawns retain explicit hold", towers.All(t => t.Watching))
            );
            await CheckMove();
            await Step(GenDate.TicksPerHour + 10);
            await On(() =>
            {
                Check("drafted hold has no one-hour timeout", towers.All(t => t.Watching));
                Check(
                    "drafted inspection explains release condition",
                    towers.All(t =>
                        t.GetInspectString().Contains("TotalFog_TowerHoldingDrafted".Translate())
                    )
                );
                foreach (var pawn in pawns)
                    pawn.drafter.Drafted = false;
            });
            await Until(
                () => towers.All(t => t.Elevation > .1f && t.Elevation < .9f),
                "undraft descent"
            );
            await On(() => CheckFacing("descent after undrafting"));
            await Capture("descent");
            await Until(() => towers.All(t => t.Occupant == null), "undraft releases platforms");
            await Until(
                () => pawns.All(p => p.CurJobDef != WatchtowerDefOf.TotalFog_WatchtowerDescend),
                "undrafted pawns finish clearing ladder"
            );
            await On(() =>
                Check(
                    "undrafted duty clears every ladder before idle wait",
                    pawns.Select((p, i) => p.Position != towers[i].Approach).All(v => v)
                )
            );
            await CheckPlatformMoves();
            await On(() =>
            {
                for (int i = 0; i < 4; i++)
                {
                    Check(
                        "undrafted menu has only three durations " + i,
                        Options(pawns[i], towers[i]).Length == 3
                    );
                    if (i < 3)
                    {
                        int ticks = (i == 2 ? 4 : i + 1) * GenDate.TicksPerHour;
                        Choose(
                            pawns[i],
                            towers[i],
                            "TotalFog_TowerWatchTimed".Translate(ticks.ToStringTicksToPeriod())
                        );
                        Check(
                            "timed menu sets native duration " + i,
                            pawns[i].CurJob.count == ticks
                        );
                    }
                    else
                        pawns[i]
                            .jobs.TryTakeOrderedJob(
                                JobMaker.MakeJob(
                                    WatchtowerDefOf.TotalFog_WatchtowerWatch,
                                    towers[i]
                                )
                            );
                }
            });
            await Until(() => towers.All(t => t.Watching), "undrafted timed watch");
            await Step(300);
            string[] towerIds = null,
                pawnIds = null;
            int[] remaining = null;
            await On(() =>
            {
                Check("timed watch keeps pawns undrafted", pawns.All(p => !p.Drafted));
                Check(
                    "inspection displays remaining time",
                    towers.All(t =>
                        t.GetInspectString()
                            .Contains(
                                "TotalFog_TowerWatchRemaining".Translate(
                                    Driver(t.Occupant).WatchTicksRemaining.ToStringTicksToPeriod()
                                )
                            )
                    )
                );
                towerIds = towers.Select(t => t.ThingID).ToArray();
                pawnIds = pawns.Select(p => p.ThingID).ToArray();
                Record("timed-before-save");
            });
            var saved = await ctx.Tools.CallAsync(
                "rimworld/save_game",
                new { saveName = temporarySave },
                cancellationToken: cancellationToken
            );
            Check("save timed watch", saved.Succeeded() && File.Exists(temporaryPath));
            await Step(1);
            await On(() => remaining = pawns.Select(p => Driver(p).WatchTicksRemaining).ToArray());
            var loaded = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName = temporarySave,
                    readiness = "visual",
                    pauseIfNeeded = true,
                },
                cancellationToken: cancellationToken
            );
            Check("load timed watch", loaded.Succeeded());
            await On(() =>
            {
                map = Find.CurrentMap;
                var things = map.listerThings.AllThings.ToDictionary(t => t.ThingID);
                towers.Clear();
                towers.AddRange(towerIds.Select(id => (Building_Watchtower)things[id]));
                pawns.Clear();
                pawns.AddRange(pawnIds.Select(id => (Pawn)things[id]));
                Check(
                    "reload preserves native timer and draft state",
                    pawns
                        .Select(
                            (p, i) => !p.Drafted && Driver(p).WatchTicksRemaining == remaining[i]
                        )
                        .All(v => v)
                );
                Record("timed-loaded");
            });
            await Capture("timed-wall-links");
            await Until(
                () => towers[0].Occupant == null && towers[3].Occupant == null,
                "one-hour watch and legacy order expire",
                GenDate.TicksPerHour + 300
            );
            await On(() =>
            {
                Check(
                    "two/four-hour watches continue after one hour",
                    towers[1].Watching && towers[2].Watching
                );
                Check(
                    "completed watch does not draft colonists",
                    !pawns[0].Drafted && !pawns[3].Drafted
                );
                pawns[1].drafter.Drafted = true;
                Check(
                    "drafting timed lookout holds the same platform",
                    towers[1].Watching
                        && Driver(pawns[1]).HoldingWhileDrafted
                        && pawns[1].CurJob.count == 0
                );
                Record("drafting-retains-platform");
            });
            await Step(60);
            await On(() =>
            {
                Check(
                    "drafted timed lookout remains on top",
                    towers[1].Watching && towers[1].Elevation == 1
                );
                pawns[1].drafter.Drafted = false;
            });
            await Until(
                () => towers[1].Occupant == null,
                "native undraft releases converted timed watch"
            );
            await On(() => Record("finished"));

            Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
            JobDriver_Watchtower Driver(Pawn p) => (JobDriver_Watchtower)p.jobs.curDriver;
            FloatMenuOption[] Options(Pawn p, Building_Watchtower t) =>
                t.GetFloatMenuOptions(p).Where(o => !o.Disabled).ToArray();
            void Choose(Pawn p, Building_Watchtower t, string label) =>
                Options(p, t).Single(o => o.Label == label).action();
            void Check(string label, bool value)
            {
                proof.assertions.Add(RimBridgeEvidence.IsTrue(label, value));
                if (!value)
                    throw new InvalidOperationException(label);
            }
            void CheckFacing(string label)
            {
                foreach (var p in pawns)
                    p.rotationTracker.ProcessPostTickVisuals(0);
                Check(
                    "native rotation tracker preserves all four facings " + label,
                    pawns
                        .Select(
                            (p, i) =>
                                Driver(p).Climbing && p.Rotation == towers[i].Rotation.Opposite
                        )
                        .All(v => v)
                );
                Record(label);
            }
            void Record(string phase) =>
                states.Add(
                    new
                    {
                        phase,
                        tick = Find.TickManager.TicksGame,
                        towers = towers
                            .Select(
                                (t, i) =>
                                    new
                                    {
                                        t.ThingID,
                                        rotation = t.Rotation.AsInt,
                                        t.Elevation,
                                        t.Watching,
                                        occupant = t.Occupant?.ThingID,
                                        pawn = pawns[i].ThingID,
                                        facing = pawns[i].Rotation.AsInt,
                                        drafted = pawns[i].Drafted,
                                        job = pawns[i].CurJobDef?.defName,
                                        count = pawns[i].CurJob?.count,
                                        remaining = pawns[i].jobs.curDriver
                                            is JobDriver_Watchtower driver
                                            ? driver.WatchTicksRemaining
                                            : 0,
                                    }
                            )
                            .ToArray(),
                    }
                );
            async Task Step(int ticks)
            {
                // The native stepping service has a per-request timeout.
                // A whole game hour must not be submitted as one request.
                for (int elapsed = 0; elapsed < ticks; elapsed += 100)
                {
                    var result = await ctx.Game.StepTicksAsync(
                        Math.Min(100, ticks - elapsed),
                        cancellationToken: cancellationToken
                    );
                    if (!result.Success)
                        throw new InvalidOperationException("Tick stepping: " + result.Status);
                }
            }
            async Task CheckMove()
            {
                IntVec3 destination = default;
                await On(() =>
                {
                    destination = towers[1].Approach + towers[1].Rotation.FacingCell;
                    Check(
                        "move destination is reachable ground",
                        destination.Standable(map)
                            && pawns[1].CanReach(destination, PathEndMode.OnCell, Danger.Deadly)
                    );
                    Check(
                        "other pawn cannot route out of tower footprint",
                        !map.reachability.CanReach(
                            towers[1].Position,
                            destination,
                            PathEndMode.OnCell,
                            TraverseParms.For(pawns[0])
                        )
                    );
                    var blocker = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog);
                    blocker.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(blocker, towers[1].Approach, map);
                    Check(
                        "blocked ladder does not promote reachability",
                        !pawns[1].CanReach(destination, PathEndMode.OnCell, Danger.Deadly)
                    );
                    blocker.Destroy(DestroyMode.Vanish);
                    Check(
                        "clearing ladder restores native route",
                        pawns[1].CanReach(destination, PathEndMode.OnCell, Danger.Deadly)
                    );
                    pawns[1].jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, destination));
                });
                await Until(
                    () => towers[1].Occupant == null && pawns[1].Position == destination,
                    "drafted move descends then reaches ordered cell"
                );
                await On(() =>
                {
                    var achtung = HarmonyLib.AccessTools.TypeByName("AchtungMod.Tools");
                    if (achtung == null)
                        Choose(pawns[1], towers[1], "TotalFog_TowerWatchDrafted".Translate());
                    else
                    {
                        var order = HarmonyLib.AccessTools.Method(
                            achtung,
                            "OrderToLocal",
                            new[] { typeof(Pawn), typeof(int), typeof(int) }
                        );
                        order.Invoke(
                            null,
                            new object[] { pawns[1], towers[1].Position.x, towers[1].Position.z }
                        );
                        Check(
                            "Achtung native order uses ladder job",
                            pawns[1].CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerWatch
                                && pawns[1].CurJob.targetA.Thing == towers[1]
                        );
                    }
                });
                await Until(() => towers[1].Watching, "drafted watch resumes after move");
            }
            async Task CheckPlatformMoves()
            {
                await On(() =>
                {
                    foreach (var p in pawns)
                        p.drafter.Drafted = true;
                    for (int i = 0; i < towers.Count; i++)
                    {
                        var tower = towers[i];
                        var p = pawns[i];
                        var top = (
                            tower.Position.ToVector3Shifted()
                            + new UnityEngine.Vector3(0, 0, Core.TowerGeometry.PawnProjection)
                        ).ToIntVec3();
                        Check(
                            "logical center preserves drafted move anchor " + i,
                            TowerMoveOrders.MoveAnchor(tower.Position, map, 2.9f) == tower.Position
                        );
                        Check(
                            "drawn top resolves native destination " + i,
                            RCellFinder.BestOrderedGotoDestNear(top, p) == tower.Position
                        );
                        Check(
                            "native tower move eligibility " + i,
                            FloatMenuOptionProvider_DraftedMove.PawnCanGoto(p, top).Accepted
                        );
                        var deck = TowerMoveOrders.DeckCell(tower);
                        Check(
                            "visible deck resolves native destination " + i,
                            RCellFinder.BestOrderedGotoDestNear(deck, p) == tower.Position
                                && TowerMoveOrders.MoveAnchor(deck, map, 2.9f) == deck
                        );
                        var center = tower.Position;
                        Check(
                            "formation pawn and marker draw above logical center " + i,
                            TowerMoveOrders.FormationDrawPosition(ref center, 0)
                                == center.ToVector3Shifted()
                                    + new UnityEngine.Vector3(
                                        0,
                                        0,
                                        Core.TowerGeometry.PawnProjection
                                    )
                        );
                    }
                });
                // Literal UI drags include Selector's separate starting-cell
                // lookup. Direct StartInteraction calls missed that lookup.
                foreach (int count in new[] { 3, 4 })
                {
                    await On(() =>
                    {
                        Find.Selector.ClearSelection();
                        foreach (var p in pawns.Take(count))
                        {
                            p.drafter.Drafted = true;
                            Find.Selector.Select(
                                p,
                                playSound: false,
                                forceDesignatorDeselect: true
                            );
                        }
                    });
                    var from = TowerMoveOrders.DeckCell(towers[0]);
                    var to = TowerMoveOrders.DeckCell(towers[count - 1]);
                    var dragged = await ctx.Tools.CallAsync(
                        "rimworld/drag_cell",
                        new
                        {
                            fromX = from.x,
                            fromZ = from.z,
                            toX = to.x,
                            toZ = to.z,
                            button = "right",
                        },
                        cancellationToken: cancellationToken
                    );
                    Check("literal right drag dispatched " + count, dragged.Succeeded());
                    await On(() =>
                        Check(
                            "native dragged line creates ladder jobs " + count,
                            pawns
                                .Take(count)
                                .Select(
                                    (p, i) =>
                                        p.CurJobDef == WatchtowerDefOf.TotalFog_WatchtowerWatch
                                        && p.CurJob.targetA.Thing == towers[i]
                                )
                                .All(v => v)
                        )
                    );
                    await Until(
                        () => towers.Take(count).All(t => t.Watching),
                        "dragged line occupies platforms " + count
                    );
                    await On(() =>
                    {
                        foreach (var p in pawns.Take(count))
                            p.rotationTracker.ProcessPostTickVisuals(0);
                        Check(
                            "idle lookouts face the player " + count,
                            pawns.Take(count).All(p => p.Rotation == Rot4.South)
                        );
                        int start = pawns[0].CurJob.startTick;
                        FloatMenuOptionProvider_DraftedMove.PawnGotoAction(
                            TowerMoveOrders.DeckCell(towers[0]),
                            pawns[0],
                            TowerMoveOrders.DeckCell(towers[0])
                        );
                        Check(
                            "repeated same-platform order keeps current watch " + count,
                            pawns[0].CurJob.startTick == start && towers[0].Watching
                        );
                        foreach (var p in pawns)
                            p.drafter.Drafted = false;
                    });
                    await Until(
                        () => towers.All(t => t.Occupant == null),
                        "platform move undraft descends " + count
                    );
                    await Until(
                        () =>
                            pawns.All(p =>
                                p.CurJobDef != WatchtowerDefOf.TotalFog_WatchtowerDescend
                            ),
                        "platform move clears ladder " + count
                    );
                }
            }
            async Task Until(Func<bool> done, string label, int limit = 1500)
            {
                for (int elapsed = 0; elapsed <= limit; elapsed += 10)
                {
                    if (await ctx.MainThread.InvokeAsync(done, cancellationToken))
                    {
                        Check(label, true);
                        return;
                    }
                    if (elapsed < limit)
                        await Step(Math.Min(10, limit - elapsed));
                }
                await On(() => Record("failed-" + label));
                throw new InvalidOperationException("Timed out: " + label);
            }
            async Task Capture(string label)
            {
                await ctx.Game.FramesAsync(8, cancellationToken);
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX,
                        z = area.minZ + 3,
                        width = 28,
                        height = 12,
                        paddingCells = 0,
                        rootSize = 24,
                        fileName = evidenceLabel + "-" + label,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                Check("capture " + label, shot.Succeeded());
                proof.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = label,
                        kind = "cell_rect",
                        path = shot.ReadResult<string>("path"),
                        success = true,
                        details = shot.Result,
                    }
                );
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
                proof.environment.details = new
                {
                    states,
                    area = area.ToString(),
                    gameplayMvid = typeof(Building_Watchtower)
                        .Assembly
                        .ManifestModule
                        .ModuleVersionId,
                };
                if (staged)
                {
                    var restored = await ctx.Tools.CallAsync(
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
                        RimBridgeEvidence.ToolSucceeded("restore unchanged user fixture", restored)
                    );
                }
                if (temporaryPath != null && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
                await ctx.MainThread.InvokeAsync(
                    () => Prefs.PauseOnLoad = oldPause,
                    CancellationToken.None
                );
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
