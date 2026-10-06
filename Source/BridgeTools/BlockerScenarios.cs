using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using TotalFog.Core;
using Verse;
using Verse.AI;
namespace TotalFog.BridgeTools;
public sealed class BlockerScenarios
{
    private static BurstProbe activeBurst;

    [Tool("totalfog/blocker_burst", Description = "Measure repeated native blocker-change bursts on a paused, saved stress fixture with at least sixteen active player observers. Temporarily holds colonists still, closes one open fog-blocker cell near each observer, records production map-tick casting/cost over four real playback ticks, then restores blocker counts and reloads the fixture. Diagnostic only; changes no global settings.")]
    public static async Task<object> BlockerBurst(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string saveName, int cycles = 7)
    {
        if (cycles < 3 || cycles > 15) throw new ArgumentOutOfRangeException(nameof(cycles));
        var harmony = new Harmony("brrainz.totalfog.blocker-burst-probe");
        MapVisibility fog = null;
        CompSightSource[] sources = null;
        IntVec3[] cells = null;
        IntVec3[] positions = null;
        var staged = new List<IntVec3>();
        var rows = new List<object>();
        BurstProbe probe = null;
        object lifecycle = null;
        bool lifecyclePassed = false, samplesValid = true;
        int startTick = -1;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                if (activeBurst != null) throw new InvalidOperationException("A blocker burst is already running.");
                var map = Find.CurrentMap ?? throw new InvalidOperationException("Load the stress save first.");
                fog = map.GetComponent<MapVisibility>();
                if (!Find.TickManager.Paused || !fog.Initialized)
                    throw new InvalidOperationException("Use a paused initialized stress fixture.");
                sources = fog.fowWatchers.Where(s => s.parent is Pawn p && p.Faction == Faction.OfPlayer &&
                    !p.Dead && s.LastSightRange > 4).OrderBy(s => s.parent.ThingID, StringComparer.Ordinal).ToArray();
                if (sources.Length < 16) throw new InvalidOperationException("Use at least sixteen active player observers.");
                positions = sources.Select(s => s.parent.Position).ToArray();
                var occupied = new HashSet<IntVec3>();
                cells = sources.Select(source => GenRadial.RadialCellsAround(source.parent.Position, 4, false)
                    .First(cell => cell.InBounds(map) && !fog.viewBlockerCells[map.cellIndices.CellToIndex(cell)] &&
                        !occupied.Contains(cell) && cell != source.parent.Position && occupied.Add(cell))).ToArray();
                foreach (var source in sources)
                {
                    var pawn = (Pawn)source.parent;
                    pawn.pather?.StopDead();
                    var job = JobMaker.MakeJob(JobDefOf.Wait, pawn.Position);
                    job.expiryInterval = 100000;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    source.UpdateFoV(true);
                }
                // Warm the existing map drain before recording the burst.
                fog.MapComponentTick();
                probe = new BurstProbe { Fog = fog };
                activeBurst = probe;
                harmony.Patch(AccessTools.Method(typeof(MapVisibility), nameof(MapVisibility.MapComponentTick)),
                    prefix: new HarmonyMethod(typeof(BlockerScenarios), nameof(BeforeBurstTick)),
                    postfix: new HarmonyMethod(typeof(BlockerScenarios), nameof(AfterBurstTick)));
                harmony.Patch(AccessTools.Method(typeof(FieldOfView), nameof(FieldOfView.ComputeMask)),
                    prefix: new HarmonyMethod(typeof(BlockerScenarios), nameof(ObserveBurstMask)));
                harmony.Patch(AccessTools.Method(typeof(CompSightSource), nameof(CompSightSource.UpdateFoV)),
                    postfix: new HarmonyMethod(typeof(BlockerScenarios), nameof(RequeueBurstSource)));
                foreach (var cell in cells) { fog.SetWallBlocker(cell, true); staged.Add(cell); }
                int queued = Pending(fog);
                // Reference-count changes that keep a cell blocked must not
                // duplicate pending work.
                foreach (var cell in cells) fog.SetWallBlocker(cell, true);
                foreach (var cell in cells) fog.SetWallBlocker(cell, false);
                bool coalesced = Pending(fog) == queued && queued == sources.Length;
                sources[0].UpdateFoV(true);
                bool synchronousRefreshCancels = Pending(fog) == queued - 1 && !IsPending(fog, sources[0]);
                sources[1].UpdateFoV();
                bool unchangedCheckPreserves = IsPending(fog, sources[1]);
                var changing = (Pawn)sources[1].parent;
                changing.SetFaction(null); sources[1].UpdateFoV(true);
                bool disabledSourceRemoved = !IsPending(fog, sources[1]) && RegistryMatches(fog);
                changing.SetFaction(Faction.OfPlayer); sources[1].UpdateFoV(true);
                bool enabledSourceRegistered = RegistryMatches(fog);
                var respawned = (Pawn)sources[sources.Length - 1].parent;
                var respawnCell = respawned.Position;
                respawned.DeSpawn();
                bool despawnUnlinks = !IsPending(fog, sources[sources.Length - 1]) && RegistryMatches(fog);
                GenSpawn.Spawn(respawned, respawnCell, map);
                bool respawnRegistersOnce = RegistryMatches(fog);
                foreach (var cell in staged) fog.SetWallBlocker(cell, false);
                staged.Clear();
                foreach (var source in sources) source.UpdateFoV(true);
                fog.MapComponentTick();
                // A mod callback invalidating an already refreshed source must
                // survive the current drain rather than being cleared with it.
                var callbackCell = cells[0];
                fog.SetWallBlocker(callbackCell, true); staged.Add(callbackCell);
                probe.RequeueCell = callbackCell;
                probe.RequeueSource = sources.First(source => IsPending(fog, source));
                var callbackSource = probe.RequeueSource;
                fog.MapComponentTick();
                bool callbackPreserved = IsPending(fog, callbackSource);
                fog.MapComponentTick();
                bool callbackCompleted = !IsPending(fog, callbackSource);
                foreach (var cell in staged) fog.SetWallBlocker(cell, false);
                staged.Clear();
                foreach (var source in sources) source.UpdateFoV(true);
                fog.MapComponentTick();
                lifecyclePassed = coalesced && synchronousRefreshCancels && unchangedCheckPreserves &&
                    disabledSourceRemoved && enabledSourceRegistered && despawnUnlinks && respawnRegistersOnce &&
                    callbackPreserved && callbackCompleted;
                lifecycle = new { coalesced, synchronousRefreshCancels, unchangedCheckPreserves,
                    disabledSourceRemoved, enabledSourceRegistered, despawnUnlinks, respawnRegistersOnce,
                    callbackPreserved, callbackCompleted, filteredRegistryPresent =
                        AccessTools.Field(typeof(MapVisibility), "blockerSources") != null };
                startTick = Find.TickManager.TicksGame;
            }, cancellationToken);
            for (int cycle = 0; cycle < cycles; cycle++)
            {
                int firstTick = -1, pendingBefore = -1, masksBefore = 0;
                double enqueueMs = 0;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    probe.Ticks.Clear(); masksBefore = probe.Masks;
                    long start = Stopwatch.GetTimestamp();
                    foreach (var cell in cells) { fog.SetWallBlocker(cell, true); staged.Add(cell); }
                    enqueueMs = Elapsed(start);
                    pendingBefore = Pending(fog);
                    firstTick = Find.TickManager.TicksGame;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                }, cancellationToken);
                bool elapsed = false;
                for (int frame = 0; frame < 1200; frame++)
                {
                    await ctx.Game.FramesAsync(1, cancellationToken);
                    elapsed = await ctx.MainThread.InvokeAsync(() => Find.TickManager.TicksGame >= firstTick + 4, cancellationToken);
                    if (elapsed) break;
                }
                await ctx.MainThread.InvokeAsync(() =>
                {
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    if (!elapsed) throw new InvalidOperationException("The four-tick playback window did not finish.");
                    int pendingAfter = Pending(fog);
                    bool stationary = sources.Select((s, i) => s.parent.Position == positions[i]).All(value => value);
                    samplesValid &= stationary && pendingAfter == 0 && probe.Masks - masksBefore == sources.Length && RegistryMatches(fog);
                    rows.Add(new { cycle, firstTick, lastTick = Find.TickManager.TicksGame, pendingBefore, pendingAfter,
                        enqueueMs, casts = probe.Masks - masksBefore, stationary, ticks = probe.Ticks.ToArray() });
                    foreach (var cell in staged) fog.SetWallBlocker(cell, false);
                    staged.Clear();
                    foreach (var source in sources) source.UpdateFoV(true);
                    fog.MapComponentTick();
                }, cancellationToken);
            }
            return new { success = lifecyclePassed && samplesValid, lifecycle, samplesValid,
                saveName, cycles, sources = sources.Length, changedCells = cells.Length,
                registeredWatchers = fog.fowWatchers.Count, startTick, rows,
                endTick = await ctx.MainThread.InvokeAsync(() => Find.TickManager.TicksGame, cancellationToken) };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                harmony.UnpatchAll(harmony.Id);
                if (activeBurst == probe) activeBurst = null;
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                if (fog != null)
                {
                    foreach (var cell in staged) fog.SetWallBlocker(cell, false);
                    if (sources != null) foreach (var source in sources) source.UpdateFoV(true);
                    fog.MapComponentTick();
                }
            }, CancellationToken.None);
            var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual",
                pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
            if (!restored.Succeeded()) throw new InvalidOperationException("Stress fixture restoration failed.");
        }
    }

    private static int Pending(MapVisibility fog)
    {
        var queue = AccessTools.Field(typeof(MapVisibility), "dirtySources").GetValue(fog);
        return (int)queue.GetType().GetProperty("Count").GetValue(queue);
    }
    private static bool IsPending(MapVisibility fog, CompSightSource source)
        => ((System.Collections.IEnumerable)AccessTools.Field(typeof(MapVisibility), "dirtySources").GetValue(fog))
            .Cast<CompSightSource>().Contains(source);
    private static bool RegistryMatches(MapVisibility fog)
    {
        var field = AccessTools.Field(typeof(MapVisibility), "blockerSources");
        if (field == null) return true; // Baseline has no filtered registry.
        var actual = (List<CompSightSource>)field.GetValue(fog);
        return actual.Count == actual.Distinct().Count() && actual.All(s => s.parent.Spawned && s.parent.Map == fog.map) &&
            new HashSet<CompSightSource>(actual).SetEquals(fog.fowWatchers.Where(s => s.LastSightRange > 0));
    }
    private static double Elapsed(long start) => (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
    private static void BeforeBurstTick(MapVisibility __instance, out long __state)
    {
        __state = activeBurst?.Fog == __instance ? Stopwatch.GetTimestamp() : 0;
        if (__state != 0) activeBurst.MasksBeforeTick = activeBurst.Masks;
    }
    private static void AfterBurstTick(MapVisibility __instance, long __state)
    {
        if (__state != 0 && activeBurst?.Fog == __instance)
            activeBurst.Ticks.Add(new { tick = Find.TickManager.TicksGame, elapsedMs = Elapsed(__state),
                casts = activeBurst.Masks - activeBurst.MasksBeforeTick, pendingAfter = Pending(__instance) });
    }
    private static void ObserveBurstMask() { if (activeBurst != null) activeBurst.Masks++; }
    private static void RequeueBurstSource(CompSightSource __instance)
    {
        var probe = activeBurst;
        if (probe?.RequeueSource != __instance) return;
        probe.RequeueSource = null;
        probe.Fog.SetWallBlocker(probe.RequeueCell, false);
    }
    private sealed class BurstProbe
    {
        public MapVisibility Fog;
        public int Masks, MasksBeforeTick;
        public CompSightSource RequeueSource;
        public IntVec3 RequeueCell;
        public readonly List<object> Ticks = new();
    }

    [Tool("totalfog/listener_registration", Description = "Verify production visibility-listener registration, duplicate suppression, shared cells, boundary rejection and removal on a detached native map component. Leaves the live map unchanged.")]
    public static async Task<object> ListenerRegistration(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        return await ctx.MainThread.InvokeAsync(() =>
        {
            var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
            if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the map first.");
            if (map.Size.x < 2 || map.Size.z < 2) throw new InvalidOperationException("Use a map at least 2x2.");
            int componentCount = map.components.Count;
            var probe = new MapVisibility(map);
            try
            {
                var state = AccessTools.Field(typeof(MapVisibility), "hiddenAt").GetValue(probe);
                List<CompVisibility> At(int x, int z)
                {
                    int index = z * map.Size.x + x;
                    if (state is Dictionary<int, List<CompVisibility>> dictionary)
                        return dictionary.TryGetValue(index, out var list) ? list : null;
                    if (state is List<CompVisibility>[] lists) return lists[index];
                    var cell = ((ListenerCell<CompVisibility>[])state)[index];
                    if (cell.Count == 0) return null;
                    var result = new List<CompVisibility>();
                    for (int i = 0; i < cell.Count; i++) result.Add(cell[i]);
                    return result;
                }
                int Occupied() => state is Dictionary<int, List<CompVisibility>> dictionary ? dictionary.Count :
                    state is List<CompVisibility>[] lists ? lists.Count(list => list != null) :
                    ((ListenerCell<CompVisibility>[])state).Count(cell => cell.Count > 0);
                var a = new CompVisibility(); var b = new CompVisibility(); var absent = new CompVisibility();
                probe.RegisterCompHideFromPlayerPosition(a, 0, 0);
                probe.RegisterCompHideFromPlayerPosition(b, 0, 0);
                probe.RegisterCompHideFromPlayerPosition(a, 0, 0);
                int duplicateCount = At(0, 0).Count;
                bool insertionOrder = ReferenceEquals(At(0, 0)[0], a) && ReferenceEquals(At(0, 0)[1], b);
                int edgeX = map.Size.x - 1, edgeZ = map.Size.z - 1;
                probe.RegisterCompHideFromPlayerPosition(a, edgeX, edgeZ);
                foreach (var cell in new[] { new IntVec3(-1, 0, 1), new IntVec3(map.Size.x, 0, 0),
                    new IntVec3(0, 0, -1), new IntVec3(0, 0, map.Size.z) })
                {
                    probe.RegisterCompHideFromPlayerPosition(absent, cell.x, cell.z);
                    probe.DeregisterCompHideFromPlayerPosition(a, cell.x, cell.z);
                }
                bool boundsRejected = Occupied() == 2 && At(edgeX, edgeZ).Count == 1 && At(0, 0).Count == 2;
                probe.DeregisterCompHideFromPlayerPosition(absent, 0, 0);
                bool absentRemovalPreserved = At(0, 0).Count == 2;
                probe.DeregisterCompHideFromPlayerPosition(a, 0, 0);
                probe.DeregisterCompHideFromPlayerPosition(a, 0, 0);
                bool sharedCellPreserved = At(0, 0).Count == 1 && ReferenceEquals(At(0, 0)[0], b);
                var c = new CompVisibility();
                probe.RegisterCompHideFromPlayerPosition(a, 0, 0);
                probe.RegisterCompHideFromPlayerPosition(c, 0, 0);
                probe.DeregisterCompHideFromPlayerPosition(b, 0, 0);
                bool promotionPreserved = At(0, 0).Count == 2 && ReferenceEquals(At(0, 0)[0], a) && ReferenceEquals(At(0, 0)[1], c);
                probe.DeregisterCompHideFromPlayerPosition(c, 0, 0);
                bool overflowRemovalPreserved = At(0, 0).Count == 1 && ReferenceEquals(At(0, 0)[0], a);
                probe.DeregisterCompHideFromPlayerPosition(a, 0, 0);
                bool emptySlotReleased = At(0, 0) == null;
                probe.DeregisterCompHideFromPlayerPosition(a, edgeX, edgeZ);
                probe.DeregisterCompHideFromPlayerPosition(a, edgeX, edgeZ);
                bool allRemoved = Occupied() == 0;
                bool detached = !map.components.Contains(probe) && map.components.Count == componentCount;
                return new { success = duplicateCount == 2 && insertionOrder && boundsRejected &&
                    absentRemovalPreserved && sharedCellPreserved && promotionPreserved && overflowRemovalPreserved && emptySlotReleased && allRemoved && detached,
                    representation = state.GetType().Name, duplicateCount, insertionOrder, boundsRejected,
                    absentRemovalPreserved, sharedCellPreserved, promotionPreserved, overflowRemovalPreserved, emptySlotReleased, allRemoved, detached };
            }
            finally { map.components.Remove(probe); }
        }, cancellationToken);
    }

    [Tool("totalfog/blocker_geometry", Description = "Check an actual rare-ticking 3x2 opaque building across rotation and despawn, with the map's real cell stride.")]
    public static async Task<object> BlockerGeometry(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        Building building = null;
        CompVisibility visibility = null;
        Array registrations = null;
        int mapWidth = 0;
        bool RegisteredAt(IntVec3 cell)
        {
            int index = cell.z * mapWidth + cell.x;
            return registrations is List<CompVisibility>[] lists ? lists[index]?.Contains(visibility) == true :
                ((ListenerCell<CompVisibility>[])registrations)[index].Contains(visibility);
        }
        IntVec3[] before = null;
        object initial = null, rotated = null, removed = null;
        await ctx.MainThread.InvokeAsync(() =>
        {
            var map = Find.CurrentMap;
            var def = new ThingDef { defName = "TotalFogProbeBlocker", label = "geometry probe", thingClass = typeof(Building),
                category = ThingCategory.Building, drawerType = DrawerType.None, tickerType = TickerType.Rare,
                size = new IntVec2(3,2), blockLight = true, fillPercent = 1, passability = Traversability.Impassable,
                building = new BuildingProperties(), modContentPack = LoadedModManager.GetMod<TotalFogMod>().Content };
            PresentationScenarios.AssignHash(def); DefGenerator.AddImpliedDef(def);
            var cell = map.AllCells.First(c => c.x > 3 && c.z > 3 && c.x < map.Size.x-4 && c.z < map.Size.z-4 &&
                new CellRect(c.x-2,c.z-2,5,5).All(p => p.GetEdifice(map) == null && !map.GetComponent<MapVisibility>().viewBlockerCells[map.cellIndices.CellToIndex(p)]));
            building = (Building)ThingMaker.MakeThing(def); GenSpawn.Spawn(building, cell, map);
            visibility = building.TryGetComp<CompFog>().HideFromPlayer;
            mapWidth = map.Size.x;
            registrations = (Array)AccessTools.Field(typeof(MapVisibility), "hiddenAt")
                .GetValue(map.GetComponent<MapVisibility>());
            before = building.OccupiedRect().ToArray();
            initial = new { occupied = before.Length, allRegistered = before.All(RegisteredAt),
                allBlocked = before.All(p => map.GetComponent<MapVisibility>().viewBlockerCells[map.cellIndices.CellToIndex(p)]) };
            building.Rotation = Rot4.East;
        }, cancellationToken);
        try
        {
            await ctx.Game.StepTicksAsync(250, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(() =>
            {
                var map = building.Map;var fog = map.GetComponent<MapVisibility>();var cells = building.OccupiedRect().ToArray();
                rotated = new { occupied = cells.Length, allRegistered = cells.All(RegisteredAt),
                    oldExclusiveUnregistered = before.Except(cells).All(p => !RegisteredAt(p)),
                    allBlocked = cells.All(p => fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]), oldExclusiveCleared = before.Except(cells).All(p => !fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]) };
                building.Destroy();
                removed = new { allUnregistered = cells.All(p => !RegisteredAt(p)), allCleared = cells.All(p => !fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]) };
            }, cancellationToken);
            return new { initial, rotated, removed };
        }
        finally { await ctx.MainThread.InvokeAsync(() => { if (building is { Destroyed: false }) building.Destroy(); }, cancellationToken); }
    }
}
