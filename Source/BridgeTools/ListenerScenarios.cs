using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using TotalFog.Core;
using Verse;

namespace TotalFog.BridgeTools;

// Typed candidate diagnostics must not share compiler-generated delegate caches
// with the reflection-only timing driver used against the original namespace.
public sealed class ListenerScenarios
{
    private static MapVisibility observedMap;
    private static List<CompVisibility>[] observedCells;
    private static ListenerCell<CompVisibility>[] observedInline;
    private static long registerCalls, deregisterCalls, createdLists, releasedLists;

    [Tool("totalfog/listener_churn", Description = "Count actual listener-list storage creation and release during native playback, supporting list and inline-singleton layouts. Uses temporary registration probes and removes them afterward; does not rely on the native allocation counter.")]
    public static async Task<object> ListenerChurn(IRimBridgeContext ctx, CancellationToken cancellationToken,
        int durationMs = 8000, string speed = "Ultrafast")
    {
        var harmony = new Harmony("brrainz.totalfog.listener-churn-probe");
        object initial = null;
        bool ownsProbe = false;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                if (observedMap != null) throw new InvalidOperationException("A listener probe is already active.");
                var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
                if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the map first.");
                ownsProbe = true;
                observedMap = map.GetComponent<MapVisibility>();
                var live = AccessTools.Field(typeof(MapVisibility), "hiddenAt").GetValue(observedMap);
                observedCells = live as List<CompVisibility>[];
                observedInline = live as ListenerCell<CompVisibility>[];
                if (observedCells == null && observedInline == null) throw new InvalidOperationException("Unrecognized listener layout.");
                registerCalls = deregisterCalls = createdLists = releasedLists = 0;
                initial = Occupancy();
                harmony.Patch(AccessTools.Method(typeof(MapVisibility), nameof(MapVisibility.RegisterCompHideFromPlayerPosition)),
                    prefix: new HarmonyMethod(typeof(ListenerScenarios), nameof(RegisterBegin)),
                    postfix: new HarmonyMethod(typeof(ListenerScenarios), nameof(RegisterEnd)));
                harmony.Patch(AccessTools.Method(typeof(MapVisibility), nameof(MapVisibility.DeregisterCompHideFromPlayerPosition)),
                    prefix: new HarmonyMethod(typeof(ListenerScenarios), nameof(DeregisterBegin)),
                    postfix: new HarmonyMethod(typeof(ListenerScenarios), nameof(DeregisterEnd)));
            }, cancellationToken);
            var playback = await PerformanceScenarios.RuntimePerformance(ctx, cancellationToken,
                durationMs, speed, speed != "Normal");
            return await ctx.MainThread.InvokeAsync(() => new
            {
                success = (bool)playback.GetType().GetProperty("success").GetValue(playback),
                initial, final = Occupancy(), registerCalls, deregisterCalls,
                listStorageCreated = createdLists, listStorageReleased = releasedLists,
                layout = observedInline == null ? "List" : "InlineSingleton",
                mainMvid = typeof(MapVisibility).Assembly.ManifestModule.ModuleVersionId, playback
            }, cancellationToken);
        }
        finally
        {
            if (ownsProbe)
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id); observedMap = null; observedCells = null; observedInline = null;
                }, CancellationToken.None);
        }
    }

    private static int CountAt(int index) => observedInline != null ? observedInline[index].Count :
        observedCells[index]?.Count ?? 0;
    private static bool HasList(int index) => CountAt(index) > (observedInline == null ? 0 : 1);
    private static object Occupancy()
    {
        int cells = observedInline?.Length ?? observedCells.Length;
        int occupied = 0, singletons = 0, registrations = 0, listCells = 0;
        for (int i = 0; i < cells; i++)
        {
            int count = CountAt(i); registrations += count;
            if (count > 0) occupied++;
            if (count == 1) singletons++;
            if (HasList(i)) listCells++;
        }
        return new { cells, occupied, singletons, registrations, listCells };
    }
    private static int Index(MapVisibility map, int x, int z) => map == observedMap &&
        map.Coverage.InBounds(x, z) ? z * map.mapSizeX + x : -1;
    // These Harmony callbacks use candidate types, so keep them out of the
    // bridge's public-method discovery when it measures the original namespace.
    private static void RegisterBegin(MapVisibility __instance, int x, int z, out int __state)
    {
        int index = Index(__instance, x, z); __state = 0;
        if (index < 0) return;
        registerCalls++;
        if (CountAt(index) == (observedInline == null ? 0 : 1)) __state = index + 1;
    }
    private static void RegisterEnd(int __state)
    {
        if (__state > 0 && HasList(__state - 1)) createdLists++;
    }
    private static void DeregisterBegin(MapVisibility __instance, int x, int z, out int __state)
    {
        int index = Index(__instance, x, z); __state = 0;
        if (index < 0) return;
        deregisterCalls++;
        if (HasList(index)) __state = index + 1;
    }
    private static void DeregisterEnd(int __state)
    {
        if (__state > 0 && !HasList(__state - 1)) releasedLists++;
    }

    [Tool("totalfog/listener_storage_cost", Description = "Compare retained heap estimates for per-cell lists and inline-singleton listener storage using the paused map's actual registrations. Detached clones share listener objects and preserve their order; the live map is unchanged.")]
    public static async Task<object> ListenerStorageCost(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        return await ctx.MainThread.InvokeAsync(() =>
        {
            var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
            if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the map first.");
            var live = AccessTools.Field(typeof(MapVisibility), "hiddenAt").GetValue(map.GetComponent<MapVisibility>());
            var lists = live as List<CompVisibility>[];
            var slots = live as ListenerCell<CompVisibility>[];
            if (lists == null && slots == null) throw new InvalidOperationException("Unrecognized listener layout.");
            int cellCount = slots?.Length ?? lists.Length;
            int Count(int i) => slots != null ? slots[i].Count : lists[i]?.Count ?? 0;
            CompVisibility Item(int i, int j) => slots != null ? slots[i][j] : lists[i][j];
            int Capacity(int i)
            {
                if (lists != null) return lists[i].Capacity;
                int capacity = 4; while (capacity < Count(i)) capacity *= 2;
                return capacity;
            }
            var entries = Enumerable.Range(0, cellCount).Where(i => Count(i) > 0)
                .Select(i => (Index: i, Items: Enumerable.Range(0, Count(i)).Select(j => Item(i, j)).ToArray(), Capacity: Capacity(i))).ToArray();
            var rows = new List<object>();
            // Keep earlier cohorts live too, so their collection cannot subtract
            // from the next sample's retained-heap delta.
            var retained = new List<object>(6);
            for (int sample = 0; sample < 3; sample++)
            {
                long referenceBytes = 0, inlineBytes = 0;
                bool equal = true;
                List<CompVisibility>[] reference = null;
                ListenerCell<CompVisibility>[] inline = null;
                void BuildReference()
                {
                    long before = GC.GetTotalMemory(true);
                    reference = new List<CompVisibility>[cellCount];
                    foreach (var e in entries) reference[e.Index] = new List<CompVisibility>(e.Capacity);
                    foreach (var e in entries) reference[e.Index].AddRange(e.Items);
                    retained.Add(reference);
                    referenceBytes = GC.GetTotalMemory(true) - before;
                    GC.KeepAlive(reference);
                }
                void BuildInline()
                {
                    long before = GC.GetTotalMemory(true);
                    inline = new ListenerCell<CompVisibility>[cellCount];
                    foreach (var e in entries) foreach (var item in e.Items) inline[e.Index].Add(item);
                    retained.Add(inline);
                    inlineBytes = GC.GetTotalMemory(true) - before;
                    GC.KeepAlive(inline);
                }
                if ((sample & 1) == 0) { BuildReference(); BuildInline(); }
                else { BuildInline(); BuildReference(); }
                for (int i = 0; i < cellCount; i++)
                {
                    int count = reference[i]?.Count ?? 0;
                    if (inline[i].Count != count) { equal = false; break; }
                    for (int j = 0; j < count; j++)
                        if (!ReferenceEquals(reference[i][j], inline[i][j])) equal = false;
                }
                rows.Add(new { sample, referenceRetainedBytes = referenceBytes, inlineRetainedBytes = inlineBytes, equal });
                GC.KeepAlive(reference); GC.KeepAlive(inline);
                if (!equal) throw new InvalidOperationException("Listener storage changed registration order or identity.");
            }
            GC.KeepAlive(retained);
            return new { success = true, cells = cellCount, occupied = entries.Length,
                singletons = entries.Count(e => e.Items.Length == 1),
                registrations = entries.Sum(e => e.Items.Length),
                capacityReferences = entries.Sum(e => e.Capacity), rows,
                capacitySource = lists != null ? "actual native list capacities" : "default list growth implied by current counts",
                metric = "Full-collection retained managed heap estimates, not total allocation or process memory",
                mainMvid = typeof(MapVisibility).Assembly.ManifestModule.ModuleVersionId };
        }, cancellationToken);
    }

    [Tool("totalfog/listener_index_cost", Description = "Compare dictionary and direct-array visibility-listener lookup using the current paused map's actual registrations. Reports occupancy, construction memory and alternating warmed native timings without changing gameplay.")]
    public static async Task<object> ListenerIndexCost(IRimBridgeContext ctx, CancellationToken cancellationToken,
        int passes = 50)
    {
        if (passes < 5 || passes > 500) throw new ArgumentOutOfRangeException(nameof(passes));
        return await ctx.MainThread.InvokeAsync(() =>
        {
            var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
            if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the map first.");
            var fog = map.GetComponent<MapVisibility>();
            var live = AccessTools.Field(typeof(MapVisibility), "hiddenAt").GetValue(fog);
            var registrations = new Dictionary<int, List<CompVisibility>>();
            if (live is Dictionary<int, List<CompVisibility>> dictionary)
            {
                foreach (var entry in dictionary) registrations.Add(entry.Key, entry.Value);
            }
            else if (live is List<CompVisibility>[] cells)
            {
                for (int i = 0; i < cells.Length; i++)
                    if (cells[i] != null) registrations.Add(i, cells[i]);
            }
            else if (live is ListenerCell<CompVisibility>[] slots)
            {
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].Count > 0)
                    {
                        var list = new List<CompVisibility>();
                        for (int j = 0; j < slots[i].Count; j++) list.Add(slots[i][j]);
                        registrations.Add(i, list);
                    }
            }
            else throw new InvalidOperationException("Unrecognized listener index.");
            int cellCount = fog.Coverage.CellCount;
            if (registrations.Any(p => (uint)p.Key >= cellCount || p.Value.Count == 0 ||
                p.Value.Count != p.Value.Distinct().Count()))
                throw new InvalidOperationException("Invalid or duplicated native registration.");
            long calibrationBefore = GC.GetAllocatedBytesForCurrentThread();
            var calibration = new byte[4096];
            long allocationCalibration = GC.GetAllocatedBytesForCurrentThread() - calibrationBefore;
            GC.KeepAlive(calibration);
            bool allocationCounterReliable = allocationCalibration >= calibration.Length;
            long heapBefore = GC.GetTotalMemory(true);
            long before = GC.GetAllocatedBytesForCurrentThread();
            var reference = new Dictionary<int, List<CompVisibility>>(registrations);
            long dictionaryCounterDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            long dictionaryRetainedHeapDelta = GC.GetTotalMemory(true) - heapBefore;
            heapBefore = GC.GetTotalMemory(true);
            before = GC.GetAllocatedBytesForCurrentThread();
            var direct = new List<CompVisibility>[cellCount];
            long arrayCounterDelta = GC.GetAllocatedBytesForCurrentThread() - before;
            long arrayRetainedHeapDelta = GC.GetTotalMemory(true) - heapBefore;
            foreach (var entry in registrations) direct[entry.Key] = entry.Value;
            var rows = new List<object>();
            foreach (var trace in new[] { ("all-cells", Enumerable.Range(0, cellCount).ToArray()),
                ("occupied-cells", registrations.Keys.OrderBy(i => i).ToArray()) })
            {
                long expected = trace.Item2.Sum(i => direct[i]?.Count ?? 0) * (long)passes;
                double Measure(bool useArray, out long allocated)
                {
                    long total = 0, bytes = GC.GetAllocatedBytesForCurrentThread(), start = Stopwatch.GetTimestamp();
                    if (useArray)
                        for (int p = 0; p < passes; p++) foreach (int index in trace.Item2)
                        { var list = direct[index]; if (list != null) total += list.Count; }
                    else
                        for (int p = 0; p < passes; p++) foreach (int index in trace.Item2)
                            if (reference.TryGetValue(index, out var list)) total += list.Count;
                    long end = Stopwatch.GetTimestamp();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - bytes;
                    if (total != expected) throw new InvalidOperationException("Listener lookup changed the result.");
                    return (end - start) * 1000d / Stopwatch.Frequency;
                }
                for (int i = 0; i < 3; i++) { Measure(false, out _); Measure(true, out _); }
                var dictionaryMs = new double[5]; var arrayMs = new double[5];
                var dictionaryAllocated = new long[5]; var arrayAllocated = new long[5];
                for (int i = 0; i < 5; i++)
                    if ((i & 1) == 0)
                    { dictionaryMs[i] = Measure(false, out dictionaryAllocated[i]); arrayMs[i] = Measure(true, out arrayAllocated[i]); }
                    else
                    { arrayMs[i] = Measure(true, out arrayAllocated[i]); dictionaryMs[i] = Measure(false, out dictionaryAllocated[i]); }
                var sortedDictionary = (double[])dictionaryMs.Clone(); var sortedArray = (double[])arrayMs.Clone();
                Array.Sort(sortedDictionary); Array.Sort(sortedArray);
                rows.Add(new { trace = trace.Item1, lookupsPerSample = (long)passes * trace.Item2.Length,
                    dictionaryMs, arrayMs, dictionaryAllocated, arrayAllocated,
                    dictionaryMedianMs = sortedDictionary[2], arrayMedianMs = sortedArray[2],
                    arrayPercent = 100 * (sortedArray[2] / sortedDictionary[2] - 1) });
            }
            return new { success = true, passes, mapX = map.Size.x, mapZ = map.Size.z,
                cellCount, occupiedCells = registrations.Count, listeners = registrations.Values.Sum(v => v.Count),
                liveRepresentation = live.GetType().Name, allocationCounterReliable, allocationCalibration,
                listsMaterializedForIndexView = live is ListenerCell<CompVisibility>[],
                dictionaryCounterDelta, arrayCounterDelta, dictionaryRetainedHeapDelta, arrayRetainedHeapDelta,
                mainMvid = typeof(MapVisibility).Assembly.ManifestModule.ModuleVersionId, rows };
        }, cancellationToken);
    }
}
