using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class EnvironmentScenarios
{
    private const string PatchID = "totalfog.test.environment";
    private static Map testMap;
    private static IntVec3 testCell;
    private static bool readingMouseover;
    private static int terrainReads,
        allowedWindows;

    [Tool(
        "totalfog/environment_readouts",
        Description = "Measure real mouseover terrain reads, environment windows and beauty samples through sight loss/reveal and the colony visibility bypass."
    )]
    public static async Task<object> EnvironmentReadouts(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        var patches = new Harmony(PatchID);
        MapVisibility fog = null;
        int index = -1;
        bool addedSight = false,
            oldBeauty = false,
            oldOutsideOnly = FogSettings.OnlyOutsideColony;
        object observed = null,
            hidden = null,
            revealed = null,
            bypassed = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    testMap =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load a test map first.");
                    oldBeauty = Find.PlaySettings.showBeauty;
                    if (!testMap.IsPlayerHome)
                        throw new InvalidOperationException(
                            "Use a player-home fixture to test the colony visibility bypass."
                        );
                    fog = testMap.GetComponent<MapVisibility>();
                    testCell = testMap.AllCells.First(c =>
                        !testMap.fogGrid.IsFogged(c)
                        && fog.knownCells[testMap.cellIndices.CellToIndex(c)]
                        && !fog.IsShown(Faction.OfPlayer, c)
                        && c.Standable(testMap)
                        && c.GetRoom(testMap) != null
                        && c.GetThingList(testMap).Count == 0
                    );
                    index = testMap.cellIndices.CellToIndex(testCell);
                    Find.PlaySettings.showBeauty = true;
                    FogSettings.OnlyOutsideColony = false;
                    Find.Selector.ClearSelection();
                    patches.Patch(
                        AccessTools.Method(
                            typeof(Verse.MouseoverReadout),
                            nameof(Verse.MouseoverReadout.MouseoverReadoutOnGUI)
                        ),
                        prefix: new HarmonyMethod(
                            typeof(EnvironmentScenarios),
                            nameof(BeginReadout)
                        )
                        {
                            priority = Priority.First,
                        },
                        finalizer: new HarmonyMethod(
                            typeof(EnvironmentScenarios),
                            nameof(EndReadout)
                        )
                    );
                    patches.Patch(
                        AccessTools.Method(
                            typeof(GridsUtility),
                            nameof(GridsUtility.GetTerrain),
                            new[] { typeof(IntVec3), typeof(Map) }
                        ),
                        prefix: new HarmonyMethod(
                            typeof(EnvironmentScenarios),
                            nameof(RecordTerrain)
                        )
                    );
                    patches.Patch(
                        AccessTools.Method(
                            typeof(Verse.EnvironmentStatsDrawer),
                            "ShouldShowWindowNow"
                        ),
                        finalizer: new HarmonyMethod(
                            typeof(EnvironmentScenarios),
                            nameof(RecordWindow)
                        )
                    );
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x = testCell.x, z = testCell.z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException(
                    "Could not position the environment camera: " + camera.Error
                );
            bool open = await ctx.MainThread.InvokeAsync(
                () => Find.MainTabsRoot.OpenTab != null,
                cancellationToken
            );
            if (open)
            {
                var close = await ctx.Tools.CallAsync(
                    "rimworld/close_main_tab",
                    new { },
                    cancellationToken: cancellationToken
                );
                if (!close.Succeeded())
                    throw new InvalidOperationException(
                        "Could not expose the mouseover readout: " + close.Error
                    );
            }
            var hover = await ctx.Tools.CallAsync(
                "rimworld/set_hover_target",
                new
                {
                    x = testCell.x,
                    z = testCell.z,
                    settleMs = 0,
                    durationMs = 15000,
                },
                cancellationToken: cancellationToken
            );
            if (!hover.Succeeded())
                throw new InvalidOperationException(
                    "Could not hover the environment cell: " + hover.Error
                );
            await ctx.MainThread.InvokeAsync(ResetCounters, cancellationToken);
            await ctx.Game.FramesAsync(60, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    observed = State(fog);
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                    ResetCounters();
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(120, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    hidden = State(fog);
                    fog.IncrementSeen(Faction.OfPlayer, index);
                    addedSight = true;
                    ResetCounters();
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(60, cancellationToken);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    revealed = State(fog);
                    fog.DecrementSeen(Faction.OfPlayer, index);
                    addedSight = false;
                    FogSettings.OnlyOutsideColony = true;
                    ResetCounters();
                },
                cancellationToken
            );
            await ctx.Game.FramesAsync(60, cancellationToken);
            bypassed = await ctx.MainThread.InvokeAsync(() => State(fog), cancellationToken);
            return new
            {
                cell = new { testCell.x, testCell.z },
                observed,
                hidden,
                revealed,
                bypassed,
            };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (addedSight)
                        fog.DecrementSeen(Faction.OfPlayer, index);
                    patches.UnpatchAll(PatchID);
                    RimWorld.BeautyUtility.beautyRelevantCells.Clear();
                    if (testMap != null)
                        Find.PlaySettings.showBeauty = oldBeauty;
                    FogSettings.OnlyOutsideColony = oldOutsideOnly;
                    readingMouseover = false;
                    testMap = null;
                },
                CancellationToken.None
            );
            await ctx.Tools.CallAsync(
                "rimworld/clear_hover_target",
                new { },
                cancellationToken: CancellationToken.None
            );
        }
    }

    private static object State(MapVisibility fog)
    {
        RimWorld.BeautyUtility.FillBeautyRelevantCells(testCell, testMap);
        var samples = RimWorld.BeautyUtility.beautyRelevantCells;
        int index = testMap.cellIndices.CellToIndex(testCell);
        var counts = fog.GetFactionShownCells(Faction.OfPlayer);
        return new
        {
            inSight = counts[index] > 0,
            coverageCount = counts[index],
            visibleByPolicy = fog.IsShown(Faction.OfPlayer, testCell),
            known = fog.knownCells[index],
            terrainReads,
            allowedWindows,
            readoutEnabled = Find.MainTabsRoot.OpenTab == null,
            beautySamples = samples.Count,
            unseenBeautySamples = samples.Count(c =>
                counts[testMap.cellIndices.CellToIndex(c)] == 0
            ),
        };
    }

    private static void ResetCounters() => terrainReads = allowedWindows = 0;

    private static void BeginReadout() => readingMouseover = true;

    private static Exception EndReadout(Exception __exception)
    {
        readingMouseover = false;
        return __exception;
    }

    private static void RecordTerrain(IntVec3 c, Map map)
    {
        if (readingMouseover && map == testMap && c == testCell)
            terrainReads++;
    }

    private static void RecordWindow(bool __result)
    {
        if (__result && Find.CurrentMap == testMap && UI.MouseCell() == testCell)
            allowedWindows++;
    }
}
