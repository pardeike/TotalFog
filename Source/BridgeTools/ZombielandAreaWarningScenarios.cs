using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed partial class ZombielandEffectsScenarios
{
    private static int areaWarningDrawCalls;

    [Tool(
        "totalfog/zombieland_area_warnings",
        Description = "Observe the real Zombieland danger-area warning renderer over hidden/visible/hidden zombie states and mixed colonist/zombie areas. Optionally test a Symbiant with root/core sight disagreement: warnings report the root's danger-area position, so that cell must be visible. Stages only warning-cache inputs and pauses its asynchronous updater; simulation entries must survive filtering, owned-colonist warnings stay visible. Advances frames, not ticks; restores cache/updater/sight/options and reloads the unchanged named base."
    )]
    public static async Task<object> AreaWarnings(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int frames = 12,
        bool includeSymbiant = false
    )
    {
        if (frames < 12 || frames > 30)
            throw new ArgumentException("Use 12..30 frames per state.");
        var type =
            AccessTools.TypeByName("ZombieLand.ZombieAreaManager")
            ?? throw new InvalidOperationException("Load Zombieland first.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.area-warning-probe");
        var cacheField = AccessTools.Field(type, "pawnsInDanger");
        var updaterField = AccessTools.Field(type, "stateUpdater");
        var warningField = AccessTools.Field(type, "warningShowing");
        IDictionary cache = null;
        var originalEntries = new List<(object pawn, object area)>();
        object originalUpdater = null;
        bool oldBypass = FogSettings.OnlyOutsideColony,
            originalWarning = false,
            addedSight = false,
            passed = true;
        Map map = null;
        MapVisibility fog = null;
        Pawn zombie = null,
            colonist = null,
            symbiant = null;
        var symbiantSight = new List<int>();
        Area_Allowed zombieArea = null,
            colonistArea = null;
        int index = -1,
            startTick = -1;
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named base first.");
                    fog = map.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    FogSettings.OnlyOutsideColony = false;
                    colonist = map.mapPawns.FreeColonistsSpawned.First();
                    var cell = map.AllCells.First(c =>
                        c.x > 10
                        && c.z > 10
                        && c.x < map.Size.x - 10
                        && c.z < map.Size.z - 10
                        && c.Standable(map)
                        && c.GetThingList(map).Count == 0
                        && !map.fogGrid.IsFogged(c)
                        && !fog.IsShown(Faction.OfPlayer, c)
                    );
                    zombie = (Pawn)
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
                                    Enum.Parse(
                                        AccessTools.TypeByName("ZombieLand.ZombieType"),
                                        "Normal"
                                    ),
                                    true,
                                }
                            );
                    if (zombie == null)
                        throw new InvalidOperationException(
                            "The area-warning zombie did not spawn."
                        );
                    index = map.cellIndices.CellToIndex(cell);
                    if (
                        !map.areaManager.TryMakeNewAllowed(out zombieArea)
                        || !map.areaManager.TryMakeNewAllowed(out colonistArea)
                    )
                        throw new InvalidOperationException(
                            "The fixture needs two temporary allowed areas."
                        );
                    zombieArea[cell] = true;
                    colonistArea[colonist.Position] = true;
                    cache = (IDictionary)cacheField.GetValue(null);
                    foreach (DictionaryEntry entry in cache)
                        originalEntries.Add((entry.Key, entry.Value));
                    originalUpdater = updaterField.GetValue(null);
                    originalWarning = (bool)warningField.GetValue(null);
                    // Pause cache refresh without changing gameplay classification or settings.
                    updaterField.SetValue(
                        null,
                        Enumerable.Repeat<object>(null, int.MaxValue).GetEnumerator()
                    );
                    harmony.Patch(
                        AccessTools.Method(type, "DrawDangerous"),
                        postfix: new HarmonyMethod(
                            typeof(ZombielandEffectsScenarios),
                            nameof(ObserveAreaWarningDraw)
                        )
                    );
                    startTick = Find.TickManager.TicksGame;
                },
                cancellationToken
            );
            foreach (
                string state in new[]
                {
                    "hidden-only",
                    "visible-only",
                    "hidden-after-reveal",
                    "colonist-and-hidden",
                    "hidden-other-area-before-colonist",
                }
            )
            {
                int callsBefore = 0;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool visible = state == "visible-only";
                        if (visible != addedSight)
                        {
                            if (visible)
                                fog.IncrementSeen(Faction.OfPlayer, index);
                            else
                                fog.DecrementSeen(Faction.OfPlayer, index);
                            addedSight = visible;
                        }
                        cache.Clear();
                        if (state == "colonist-and-hidden")
                            cache.Add(colonist, zombieArea);
                        cache.Add(zombie, zombieArea);
                        if (state == "hidden-other-area-before-colonist")
                            cache.Add(colonist, colonistArea);
                        warningField.SetValue(null, false);
                        callsBefore = areaWarningDrawCalls;
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool shown = (bool)warningField.GetValue(null);
                        bool expected =
                            state
                            is "visible-only"
                                or "colonist-and-hidden"
                                or "hidden-other-area-before-colonist";
                        bool valid =
                            shown == expected
                            && areaWarningDrawCalls > callsBefore
                            && cache.Contains(zombie)
                            && cache[zombie] == zombieArea
                            && Find.TickManager.TicksGame == startTick
                            && Visibility.IsVisible(zombie) == addedSight;
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                state,
                                passed = valid,
                                expectedWarning = expected,
                                actualWarning = shown,
                                nativeDrawCalls = areaWarningDrawCalls - callsBefore,
                                zombieVisible = Visibility.IsVisible(zombie),
                                cachedEntries = cache.Count,
                                simulationEntryRetained = cache.Contains(zombie),
                                tick = Find.TickManager.TicksGame,
                            }
                        );
                    },
                    cancellationToken
                );
            }
            if (includeSymbiant)
            {
                var root = await ctx.MainThread.InvokeAsync(
                    () =>
                        map.AllCells.First(c =>
                            c.x > 10
                            && c.z > 10
                            && c.x < map.Size.x - 11
                            && c.z < map.Size.z - 10
                            && new[] { c, c + IntVec3.East }.All(p =>
                                p.Standable(map)
                                && p.GetThingList(map).Count == 0
                                && !map.fogGrid.IsFogged(p)
                                && !fog.IsShown(Faction.OfPlayer, p)
                            )
                        ),
                    cancellationToken
                );
                var blob = await ctx.Tools.CallAsync(
                    "zombieland/symbiant_render_blob",
                    new
                    {
                        cells = "0,0;1,0",
                        x = root.x,
                        z = root.z,
                        replaceExisting = false,
                        select = false,
                        jump = false,
                    },
                    cancellationToken: cancellationToken
                );
                if (!blob.Succeeded() || !blob.ReadResult<bool>("success"))
                    throw new InvalidOperationException(
                        "The temporary two-cell Symbiant did not spawn."
                    );
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        symbiant = map.mapPawns.AllPawnsSpawned.Single(p =>
                            p.GetType().FullName == "ZombieLand.ZombieSymbiant"
                        );
                        AccessTools
                            .Method(symbiant.GetType(), "ClearSelectionCoreMotion")
                            .Invoke(symbiant, null);
                        AccessTools
                            .Field(symbiant.GetType(), "selectionCoreRelative")
                            .SetValue(symbiant, IntVec3.East);
                        zombieArea[root] = true;
                    },
                    cancellationToken
                );
                foreach (bool rootVisible in new[] { false, true })
                {
                    int callsBefore = 0;
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            foreach (int seen in symbiantSight)
                                fog.DecrementSeen(Faction.OfPlayer, seen);
                            symbiantSight.Clear();
                            int seenIndex = map.cellIndices.CellToIndex(
                                rootVisible ? root : root + IntVec3.East
                            );
                            fog.IncrementSeen(Faction.OfPlayer, seenIndex);
                            symbiantSight.Add(seenIndex);
                            cache.Clear();
                            cache.Add(symbiant, zombieArea);
                            warningField.SetValue(null, false);
                            callsBefore = areaWarningDrawCalls;
                        },
                        cancellationToken
                    );
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            bool shown = (bool)warningField.GetValue(null);
                            bool coreVisible = Visibility.IsVisible(symbiant);
                            bool valid =
                                shown == rootVisible
                                && areaWarningDrawCalls > callsBefore
                                && Visibility.IsVisible(map, root) == rootVisible
                                && coreVisible != rootVisible
                                && cache.Contains(symbiant)
                                && cache[symbiant] == zombieArea
                                && Find.TickManager.TicksGame == startTick;
                            passed &= valid;
                            rows.Add(
                                new
                                {
                                    state = rootVisible
                                        ? "symbiant-root-visible-core-hidden"
                                        : "symbiant-core-visible-root-hidden",
                                    passed = valid,
                                    expectedWarning = rootVisible,
                                    actualWarning = shown,
                                    rootVisible,
                                    coreVisible,
                                    root = root.ToString(),
                                    core = (root + IntVec3.East).ToString(),
                                    nativeDrawCalls = areaWarningDrawCalls - callsBefore,
                                    cachedEntries = cache.Count,
                                    simulationEntryRetained = cache.Contains(symbiant),
                                    tick = Find.TickManager.TicksGame,
                                }
                            );
                        },
                        cancellationToken
                    );
                }
            }
            return new
            {
                passed,
                startTick,
                rows,
                stagedCache = true,
                advancesSimulation = false,
            };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        if (addedSight)
                            fog.DecrementSeen(Faction.OfPlayer, index);
                        foreach (int seen in symbiantSight)
                            fog.DecrementSeen(Faction.OfPlayer, seen);
                        if (cache != null)
                        {
                            cache.Clear();
                            foreach (var entry in originalEntries)
                                cache.Add(entry.pawn, entry.area);
                            updaterField.SetValue(null, originalUpdater);
                            warningField.SetValue(null, originalWarning);
                        }
                        if (zombie?.Destroyed == false)
                            zombie.Destroy();
                        if (symbiant?.Destroyed == false)
                            AccessTools
                                .Method(symbiant.GetType(), "DebugDestroyWithoutHostTrauma")
                                .Invoke(symbiant, null);
                        var removeArea = AccessTools.Method(
                            typeof(AreaManager),
                            "Remove",
                            new[] { typeof(Area) }
                        );
                        if (zombieArea != null)
                            removeArea.Invoke(map.areaManager, new object[] { zombieArea });
                        if (colonistArea != null)
                            removeArea.Invoke(map.areaManager, new object[] { colonistArea });
                        FogSettings.OnlyOutsideColony = oldBypass;
                    },
                    CancellationToken.None
                );
                var restored = await ctx.Tools.CallAsync(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                        timeoutMs = 120000,
                    },
                    cancellationToken: CancellationToken.None
                );
                if (!restored.Succeeded() || !restored.ReadResult<bool>("success"))
                    throw new InvalidOperationException(
                        "The unchanged area-warning base did not restore."
                    );
            }
            finally
            {
                gate.Release();
            }
        }
    }

    private static void ObserveAreaWarningDraw() => Interlocked.Increment(ref areaWarningDrawCalls);
}
