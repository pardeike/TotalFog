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

public sealed class SymbiantFeedingSightScenarios
{
    [Tool("totalfog/symbiant_feeding_sight", Description = "Use the existing linked feeding fixture and real observer movement to check visible-core/hidden-root, hidden-core/visible-root and all-hidden native feed menus. Require every offered corpse to be in current sight, then start a native menu action and let its hauling/feed job complete at Normal speed. Uses a scoped three-cell base sight range for adjacent-cell disagreements. Restores options and reloads the unchanged named base; not every feed phase or ordinary-range acceptance.")]
    public static async Task<object> SymbiantFeedingSight(IRimBridgeContext ctx,
        CancellationToken cancellationToken, string saveName)
    {
        int oldRange = FogSettings.BaseViewRange;
        var rows = new List<object>();
        bool passed = true;
        object carrying = null, completed = null;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                if (Find.CurrentMap == null || !Find.TickManager.Paused || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use a paused paired base with colony fog enabled.");
            }, cancellationToken);
            var fixture = await ctx.Tools.CallAsync("zombieland/symbiant_feeding_contract",
                new { cleanup = false }, cancellationToken: cancellationToken);
            if (!fixture.Succeeded() || !fixture.ReadResult<bool>("success"))
                throw new InvalidOperationException("The retained feeding fixture did not pass.");

            Map map = null;
            Pawn symbiant = null, feeder = null;
            IntVec3 core = IntVec3.Invalid, coreView = IntVec3.Invalid;
            System.Reflection.MethodInfo optionsMethod = null, labelMethod = null;
            int beforeCells = 0;
            var cellCount = AccessTools.Property(AccessTools.TypeByName("ZombieLand.ZombieSymbiant"), "CellCount");
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap;
                symbiant = map.mapPawns.AllPawnsSpawned.Single(p => p.GetType().FullName == "ZombieLand.ZombieSymbiant");
                feeder = (Pawn)AccessTools.Property(symbiant.GetType(), "LinkedHost").GetValue(symbiant);
                core = (IntVec3)AccessTools.Property(symbiant.GetType(), "SelectionCoreCell").GetValue(symbiant);
                beforeCells = (int)cellCount.GetValue(symbiant);
                var owner = AccessTools.TypeByName("ZombieLand.Patches+FloatMenuMakerMap_GetOptions_Patch");
                optionsMethod = AccessTools.DeclaredMethod(owner, "SymbiantFeedOptions");
                labelMethod = AccessTools.DeclaredMethod(owner, "SymbiantFeedLabel");
                if (optionsMethod == null || labelMethod == null || beforeCells != 12 || core == symbiant.Position)
                    throw new InvalidOperationException("The linked non-root feeding fixture is incomplete.");
                IEnumerable<Thing> Offered() => (IEnumerable<Thing>)optionsMethod.Invoke(null, new object[] { feeder, symbiant, core });
                var candidateFeeds = Offered().ToArray();
                FogSettings.BaseViewRange = 3;
                foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer &&
                    p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null).ToArray())
                {
                    pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    Move(pawn, new IntVec3(map.Size.x - 20, 0, map.Size.z - 20));
                }
                foreach (var watcher in map.GetVisibility().fowWatchers) watcher.UpdateFoV(true);

                var labels = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                    .Select(thing => (string)labelMethod.Invoke(null, new object[] { thing })).ToHashSet();
                void Record(string name, bool expectedCore, bool? expectedRoot)
                {
                    var menu = FloatMenuMakerMap.GetOptions(new List<Pawn> { feeder }, core.ToVector3Shifted(), out _);
                    var feeds = menu.Where(option => labels.Contains(option.Label)).ToArray();
                    var offered = Offered().Select(thing => new { id = thing.ThingID, visible = Visibility.IsVisible(thing) }).ToArray();
                    bool visibleCore = Visibility.IsVisible(map, core), visibleRoot = Visibility.IsVisible(map, symbiant.Position);
                    bool valid = visibleCore == expectedCore && (!expectedRoot.HasValue || visibleRoot == expectedRoot) &&
                        (expectedCore ? feeds.Length > 0 && offered.All(thing => thing.visible) : feeds.Length == 0);
                    passed &= valid;
                    rows.Add(new { name, passed = valid, visibleCore, visibleRoot, feederCell = feeder.Position.ToString(),
                        actualSightRange = feeder.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange,
                        feedOptionCount = feeds.Length, feedLabels = feeds.Select(option => option.Label).ToArray(), offered,
                        candidateCorpses = candidateFeeds.Select(thing => new { id = thing.ThingID, visible = Visibility.IsVisible(thing) }).ToArray() });
                }
                Record("all-hidden", false, false);
                var candidates = CellRect.CenteredOn(core, 4).Where(cell => cell.InBounds(map) && cell.Standable(map)).ToArray();
                bool Place(bool visibleCore, bool visibleRoot)
                {
                    foreach (var cell in candidates)
                    {
                        Move(feeder, cell);
                        if (Visibility.IsVisible(map, core) == visibleCore && Visibility.IsVisible(map, symbiant.Position) == visibleRoot &&
                            (!visibleCore || Offered().Any(Visibility.IsVisible))) return true;
                    }
                    return false;
                }
                if (!Place(false, true)) throw new InvalidOperationException("No real observer position separates hidden core from visible root.");
                Record("hidden-core-visible-root", false, true);
                bool mixedFeedView = false;
                foreach (var cell in candidates)
                {
                    Move(feeder, cell);
                    if (!Visibility.IsVisible(map, core) || !candidateFeeds.Any(Visibility.IsVisible) ||
                        !candidateFeeds.Any(thing => !Visibility.IsVisible(thing))) continue;
                    mixedFeedView = true;
                    break;
                }
                if (!mixedFeedView) throw new InvalidOperationException("No visible-core view separates visible and hidden eligible corpses.");
                Record("visible-core-hidden-feed", true, null);
                if (!Place(true, false)) throw new InvalidOperationException("No real observer position separates visible core from hidden root.");
                coreView = feeder.Position;
                Record("visible-core-hidden-root", true, false);
                var human = Offered().OfType<Corpse>().FirstOrDefault(c => c.InnerPawn.RaceProps.Humanlike && Visibility.IsVisible(c));
                if (human == null) throw new InvalidOperationException("The visible-core view has no visible human feed.");
                string label = (string)labelMethod.Invoke(null, new object[] { human });
                FloatMenuMakerMap.GetOptions(new List<Pawn> { feeder }, core.ToVector3Shifted(), out _)
                    .Single(option => option.Label == label).action();
                if (feeder.CurJob?.def.defName != "FeedZombieSymbiant" || feeder.CurJob.targetA.Thing != symbiant ||
                    feeder.CurJob.targetB.Thing != human || feeder.CurJob.targetC.Cell != core)
                    throw new InvalidOperationException("The visible-core native menu action did not start the expected job.");
            }, cancellationToken);
            var initialPlayback = await ctx.Tools.CallAsync("rimworld/play_for",
                new { durationMs = 500, speed = "Normal", forceRequestedSpeed = false }, cancellationToken: cancellationToken);
            if (!initialPlayback.Succeeded() || !initialPlayback.ReadResult<bool>("success"))
                throw new InvalidOperationException("Initial native playback failed.");
            await ctx.MainThread.InvokeAsync(() =>
            {
                bool valid = feeder.CurJob?.def.defName == "FeedZombieSymbiant" && feeder.carryTracker.CarriedThing is Corpse &&
                    (int)cellCount.GetValue(symbiant) == beforeCells;
                passed &= valid;
                carrying = new { passed = valid, job = feeder.CurJob?.def.defName, corpse = feeder.carryTracker.CarriedThing?.ThingID,
                    cells = (int)cellCount.GetValue(symbiant), coreInitiallyVisible = true, rootInitiallyVisible = false,
                    initialViewerCell = coreView.ToString() };
            }, cancellationToken);
            var finalPlayback = await ctx.Tools.CallAsync("rimworld/play_for",
                new { durationMs = 3000, speed = "Normal", forceRequestedSpeed = false }, cancellationToken: cancellationToken);
            if (!finalPlayback.Succeeded() || !finalPlayback.ReadResult<bool>("success"))
                throw new InvalidOperationException("Completion playback failed.");
            await ctx.MainThread.InvokeAsync(() =>
            {
                bool valid = feeder.CurJob?.def.defName != "FeedZombieSymbiant" && feeder.carryTracker.CarriedThing == null &&
                    (int)cellCount.GetValue(symbiant) == beforeCells + 6;
                passed &= valid;
                completed = new { passed = valid, job = feeder.CurJob?.def.defName, corpse = feeder.carryTracker.CarriedThing?.ThingID,
                    cells = (int)cellCount.GetValue(symbiant), host = feeder.ThingID, symbiant = symbiant.ThingID };
            }, cancellationToken);
            return new { passed, rows, carrying, completed, initialPlayback, finalPlayback };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() => FogSettings.BaseViewRange = oldRange, CancellationToken.None);
            var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual",
                pauseIfNeeded = true, timeoutMs = 120000 }, cancellationToken: CancellationToken.None);
            if (!restored.Succeeded() || !restored.ReadResult<bool>("success"))
                throw new InvalidOperationException("The unchanged base did not restore.");
        }
    }

    private static void Move(Pawn pawn, IntVec3 cell)
    {
        pawn.Position = cell;
        pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
    }
}
