using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Development-only cost probe for Total Fog's InitializeComps prefix.</summary>
public sealed class CompSetupScenarios
{
    private static Harmony counter;
    private static int calls;
    private static int startTick;
    private static Stopwatch counterClock;

    [Tool(
        "totalfog/comp_setup_cost",
        Description = "Paused map: time the deployed InitializeComps prefix against local copies of the old scan and the cached first-slot check on the map's own things, then native MakeThing for the same def/stuff mix. mode=count-start/count-stop counts native InitializeComps calls across a load or ordinary playback. Changes no saved state; MakeThing advances the thing ID counter."
    )]
    public static async Task<object> CompSetupCost(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string mode = "measure",
        int iterations = 500000,
        int makeThings = 20000
    )
    {
        if (mode == "count-start")
            return await ctx.MainThread.InvokeAsync(StartCounting, cancellationToken);
        if (mode == "count-stop")
            return await ctx.MainThread.InvokeAsync(StopCounting, cancellationToken);
        if (mode != "measure")
            throw new ArgumentException("Use measure, count-start or count-stop.", nameof(mode));
        if (iterations < 10000 || iterations > 5000000 || makeThings < 0 || makeThings > 100000)
            throw new ArgumentOutOfRangeException(nameof(iterations));
        return await ctx.MainThread.InvokeAsync(
            () => Measure(iterations, makeThings),
            cancellationToken
        );
    }

    private static object StartCounting()
    {
        if (counter != null)
            throw new InvalidOperationException("Counting is already active.");
        calls = 0;
        startTick = Current.Game == null ? -1 : Find.TickManager.TicksGame;
        counter = new Harmony("brrainz.totalfog.comp-setup-count");
        counter.Patch(
            AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.InitializeComps)),
            postfix: new HarmonyMethod(typeof(CompSetupScenarios), nameof(CountPostfix))
        );
        counterClock = Stopwatch.StartNew();
        return new { success = true, startTick };
    }

    private static object StopCounting()
    {
        if (counter == null)
            throw new InvalidOperationException("Counting is not active.");
        counter.UnpatchAll(counter.Id);
        counter = null;
        int endTick = Current.Game == null ? -1 : Find.TickManager.TicksGame;
        return new
        {
            success = true,
            calls,
            startTick,
            endTick,
            elapsedMs = counterClock.ElapsedMilliseconds,
        };
    }

    private static void CountPostfix() => Interlocked.Increment(ref calls);

    private static object Measure(int iterations, int makeThings)
    {
        var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
        if (!Find.TickManager.Paused)
            throw new InvalidOperationException("Pause the map first.");
        var things = map.listerThings.AllThings.OfType<ThingWithComps>().ToArray();
        if (things.Length == 0)
            throw new InvalidOperationException("The map has no ThingWithComps.");
        var defs = things.Select(t => t.def).Distinct().ToArray();
        var before = defs.ToDictionary(d => d, d => d.comps?.Count ?? -1);

        var variants = new (string name, Action<ThingWithComps> run)[]
        {
            ("deployed", TotalFogMod.InitializeCompsPrefix),
            ("oldScan", OldPrefix),
            ("cachedFirstSlot", CandidatePrefix),
        };
        foreach (var variant in variants)
            for (int i = 0; i < things.Length; i++)
                variant.run(things[i]);

        var timings = variants.ToDictionary(v => v.name, v => new List<double>());
        var allocations = variants.ToDictionary(v => v.name, v => new List<double?>());
        for (int round = 0; round < 5; round++)
        {
            var order = round % 2 == 0 ? variants : Enumerable.Reverse(variants).ToArray();
            foreach (var variant in order)
            {
                GC.Collect();
                int collections = GC.CollectionCount(0);
                long memory = GC.GetTotalMemory(false);
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++)
                    variant.run(things[i % things.Length]);
                long ticks = Stopwatch.GetTimestamp() - start;
                long allocated = GC.GetTotalMemory(false) - memory;
                timings[variant.name].Add(ticks * 1e9 / Stopwatch.Frequency / iterations);
                allocations[variant.name]
                    .Add(
                        GC.CollectionCount(0) == collections ? allocated / (double)iterations : null
                    );
            }
        }

        var changed = defs.Where(d => (d.comps?.Count ?? -1) != before[d]).Select(d => d.defName);
        if (changed.Any())
            throw new InvalidOperationException(
                "A prefix changed def comps: " + string.Join(", ", changed.Take(5))
            );

        object creation = null;
        if (makeThings > 0)
            creation = MeasureMakeThing(things, makeThings);

        return new
        {
            success = true,
            things = things.Length,
            defs = defs.Length,
            fogFirstDefs = defs.Count(d =>
                d.comps is { Count: > 0 } && d.comps[0] == CompFog.CompDef
            ),
            meanCompsPerDef = defs.Average(d => d.comps?.Count ?? 0),
            iterations,
            nsPerCall = timings.ToDictionary(t => t.Key, t => Median(t.Value)),
            nsPerCallRounds = timings,
            bytesPerCall = allocations,
            creation,
        };
    }

    private static object MeasureMakeThing(ThingWithComps[] things, int count)
    {
        var inputs = things
            .Where(t =>
                t.def.category
                    is ThingCategory.Item
                        or ThingCategory.Building
                        or ThingCategory.Plant
                && t is not Corpse
                && t is not MinifiedThing
                && t.def.thingClass != null
            )
            .Select(t => (t.def, t.Stuff))
            .ToList();
        var usable = new List<(ThingDef def, ThingDef stuff)>();
        foreach (var input in inputs.Distinct())
            try
            {
                ThingMaker.MakeThing(input.def, input.Stuff);
                usable.Add(input);
            }
            catch { }
        var weighted = inputs.Where(usable.Contains).ToArray();
        if (weighted.Length == 0)
            return null;
        var samples = new List<double>();
        for (int round = 0; round < 3; round++)
        {
            GC.Collect();
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < count; i++)
            {
                var input = weighted[i % weighted.Length];
                ThingMaker.MakeThing(input.def, input.Stuff);
            }
            samples.Add((Stopwatch.GetTimestamp() - start) * 1e9 / Stopwatch.Frequency / count);
        }
        return new
        {
            count,
            distinctInputs = usable.Count,
            nsPerMakeThing = Median(samples),
            rounds = samples,
        };
    }

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted[sorted.Count / 2];
    }

    // Copy of the prefix before the cached first-slot check.
    private static void OldPrefix(ThingWithComps __instance)
    {
        var def = __instance.def;
        if (
            def.category
                is not (
                    ThingCategory.Pawn
                    or ThingCategory.Building
                    or ThingCategory.Item
                    or ThingCategory.Filth
                    or ThingCategory.Gas
                    or ThingCategory.Plant
                )
            && !def.IsBlueprint
        )
            return;
        def.comps ??= new List<CompProperties>();
        if (!def.comps.Any(c => c.compClass == typeof(CompFog)))
            def.comps.Insert(0, CompFog.CompDef);
    }

    // Copy of the candidate prefix.
    private static void CandidatePrefix(ThingWithComps __instance)
    {
        var def = __instance.def;
        var comps = def.comps;
        if (comps is { Count: > 0 } && comps[0] == CompFog.CompDef)
            return;
        if (
            def.category
                is not (
                    ThingCategory.Pawn
                    or ThingCategory.Building
                    or ThingCategory.Item
                    or ThingCategory.Filth
                    or ThingCategory.Gas
                    or ThingCategory.Plant
                )
            && !def.IsBlueprint
        )
            return;
        comps = def.comps ??= new List<CompProperties>();
        for (int i = 0; i < comps.Count; i++)
            if (comps[i].compClass == typeof(CompFog))
                return;
        comps.Insert(0, CompFog.CompDef);
    }
}
