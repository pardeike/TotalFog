using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Creates one native fixture to save and compare unchanged against both binaries.</summary>
public sealed class StressScenarios
{
    private static int requestedSize;

    [Tool(
        "totalfog/create_stress_map",
        Description = "From the main menu, generate a native 350x350 test map, add 50 moving colonists and 250 ordinary wild animals, then pause. Save this isolated fixture once for unchanged original/candidate comparisons. Works with either binary; diagnostic DLL only."
    )]
    public static async Task<object> CreateStressMap(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        if (Interlocked.CompareExchange(ref requestedSize, 350, 0) != 0)
            throw new InvalidOperationException("Stress-map generation is already running.");
        var harmony = new Harmony("brrainz.totalfog.stress-map");
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (Current.ProgramState != ProgramState.Entry || Current.Game != null)
                        throw new InvalidOperationException(
                            "Start from the main menu without a loaded game."
                        );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(Root_Play),
                            nameof(Root_Play.SetupForQuickTestPlay)
                        ),
                        postfix: new HarmonyMethod(typeof(StressScenarios), nameof(SetMapSize))
                    );
                },
                cancellationToken
            );
            var generation = await ctx.Tools.CallAsync(
                "rimworld/start_debug_game_ready",
                new
                {
                    timeoutMs = 180000,
                    readiness = "visual",
                    pauseIfNeeded = true,
                },
                cancellationToken: cancellationToken
            );
            if (!generation.Succeeded())
                throw new InvalidOperationException("Native stress-map generation failed.");
            return await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Generated map is missing.");
                    if (map.Size.x != 350 || map.Size.z != 350 || !Find.TickManager.Paused)
                        throw new InvalidOperationException(
                            "Generated map dimensions or pause state differ."
                        );
                    var colonistKind = DefDatabase<PawnKindDef>.GetNamed("Colonist");
                    var animalKind = DefDatabase<PawnKindDef>.GetNamed("Muffalo");
                    IntVec3 WalkCell() =>
                        CellFinder.RandomClosewalkCellNear(
                            new IntVec3(
                                Rand.Range(10, map.Size.x - 10),
                                0,
                                Rand.Range(10, map.Size.z - 10)
                            ),
                            map,
                            10
                        );
                    for (int i = 0; i < 50; i++)
                    {
                        var pawn = PawnGenerator.GeneratePawn(colonistKind, Faction.OfPlayer);
                        GenSpawn.Spawn(pawn, WalkCell(), map);
                        pawn.jobs.TryTakeOrderedJob(
                            JobMaker.MakeJob(JobDefOf.Goto, WalkCell()),
                            JobTag.Misc
                        );
                    }
                    for (int i = 0; i < 250; i++)
                        GenSpawn.Spawn(PawnGenerator.GeneratePawn(animalKind), WalkCell(), map);
                    return new
                    {
                        success = true,
                        mapX = map.Size.x,
                        mapZ = map.Size.z,
                        pawns = map.mapPawns.AllPawnsSpawned.Count,
                        colonists = map.mapPawns.FreeColonistsSpawned.Count,
                        animals = map.mapPawns.AllPawnsSpawned.Count(p => p.RaceProps.Animal),
                        paused = Find.TickManager.Paused,
                    };
                },
                cancellationToken
            );
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            Interlocked.Exchange(ref requestedSize, 0);
        }
    }

    public static void SetMapSize() => Find.GameInitData.mapSize = Volatile.Read(ref requestedSize);
}
