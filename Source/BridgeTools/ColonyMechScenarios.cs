using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed class ColonyMechScenarios
{
    [Tool(
        "totalfog/colony_mech_vision",
        Description = "Exercise colony mech vision at 1, 0.2, 0.5 and 2 through native sight refresh, alongside a human colonist and hostile mech. Uses a single-player isolated fixture, restores settings and reloads the unchanged save. Does not prove Multiplayer synchronization."
    )]
    public static async Task<object> Vision(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName = "MortalFleshbeastAttack"
    )
    {
        var integration = typeof(FogSettings).Assembly.GetType(
            "TotalFog.Compatibility.MultiplayerIntegration"
        );
        var samples = new List<object>();
        var pawns = new List<Pawn>();
        var original = await ctx.MainThread.InvokeAsync(
            () =>
            {
                if (
                    (bool)
                        integration
                            .GetProperty("Active", BindingFlags.Static | BindingFlags.NonPublic)
                            .GetValue(null)
                )
                    throw new InvalidOperationException("Use a single-player fixture.");
                return (FogSettings.ColonyMechVisionModifier, FogSettings.AISmart);
            },
            cancellationToken
        );
        int[] baseline = null;
        bool passed = true;
        try
        {
            var load = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName,
                    readiness = "visual",
                    pauseIfNeeded = true,
                    ignoreModCompatibility = true,
                },
                cancellationToken: cancellationToken
            );
            if (!load.Succeeded())
                throw new InvalidOperationException("Mech fixture load failed: " + load.Error);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    var map = Find.CurrentMap;
                    if (!ModsConfig.BiotechActive || !map.GetVisibility().Initialized)
                        throw new InvalidOperationException("Need Biotech and initialized fog.");
                    FogSettings.ColonyMechVisionModifier = 1;
                    FogSettings.AISmart = true;
                    var kind = DefDatabase<PawnKindDef>.GetNamed("Mech_Lifter");
                    pawns.Add(PawnGenerator.GeneratePawn(kind, Faction.OfPlayer));
                    pawns.Add(PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer));
                    pawns.Add(PawnGenerator.GeneratePawn(kind, Faction.OfMechanoids));
                    var cells = map
                        .AllCells.Where(c =>
                            c.x > 8
                            && c.z > 8
                            && c.x < map.Size.x - 8
                            && c.z < map.Size.z - 8
                            && c.Standable(map)
                            && c.GetThingList(map).All(t => t is Plant or Filth)
                        )
                        .Take(3)
                        .ToArray();
                    if (cells.Length != 3)
                        throw new InvalidOperationException("Need three free cells.");
                    for (int i = 0; i < pawns.Count; i++)
                    {
                        var pawn = pawns[i];
                        pawn.equipment?.DestroyAllEquipment();
                        map.fogGrid.Unfog(cells[i]);
                        GenSpawn.Spawn(pawn, cells[i], map);
                        var wait = JobMaker.MakeJob(JobDefOf.Wait_Combat);
                        wait.expiryInterval = 1000;
                        wait.canUseRangedWeapon = false;
                        pawn.jobs.StartJob(wait, JobCondition.InterruptForced);
                        pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    }
                    if (!pawns[0].IsColonyMech || pawns[1].IsColonyMech || pawns[2].IsColonyMech)
                        throw new InvalidOperationException(
                            "Native colony mech classification failed."
                        );
                    baseline = pawns
                        .Select(p => p.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange)
                        .ToArray();
                    if (baseline.Any(r => r <= 0))
                        throw new InvalidOperationException("All controls must provide sight.");
                    Capture("default", 1);
                },
                cancellationToken
            );
            foreach (float modifier in new[] { .2f, .5f, 2f, 1f })
            {
                await ctx.MainThread.InvokeAsync(
                    () => FogSettings.ColonyMechVisionModifier = modifier,
                    cancellationToken
                );
                // No forced fog refresh after changing the setting. Let native
                // source ticks apply it through the ordinary 30-tick interval.
                await ctx.Game.StepTicksAsync(35, cancellationToken: cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () => Capture("native-refresh", modifier),
                    cancellationToken
                );
            }
            return new
            {
                success = passed,
                samples,
                settingsRestoredByCleanup = true,
            };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () => (FogSettings.ColonyMechVisionModifier, FogSettings.AISmart) = original,
                CancellationToken.None
            );
            var reset = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName,
                    readiness = "visual",
                    pauseIfNeeded = true,
                    ignoreModCompatibility = true,
                },
                cancellationToken: CancellationToken.None
            );
            if (!reset.Succeeded())
                throw new InvalidOperationException("Mech fixture cleanup failed: " + reset.Error);
        }

        void Capture(string phase, float modifier)
        {
            int[] actual = pawns
                .Select(p => p.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange)
                .ToArray();
            // Dawn can change a human's range while ticks advance. Compare
            // both settings under the same native conditions for the opposite
            // controls, allowing one cell of normal sight-refresh delay.
            float[] currentRanges = pawns
                .Select(p =>
                    p.TryGetComp<CompFog>()
                        .FieldOfViewWatcher.CalcPawnSightRange(p.Position, false, false)
                )
                .ToArray();
            FogSettings.ColonyMechVisionModifier = 1;
            float[] unscaled;
            try
            {
                unscaled = pawns
                    .Select(p =>
                        p.TryGetComp<CompFog>()
                            .FieldOfViewWatcher.CalcPawnSightRange(p.Position, false, false)
                    )
                    .ToArray();
            }
            finally
            {
                FogSettings.ColonyMechVisionModifier = modifier;
            }
            int[] expected =
            {
                Mathf.RoundToInt(unscaled[0] * modifier),
                Mathf.RoundToInt(unscaled[1]),
                Mathf.RoundToInt(unscaled[2]),
            };
            bool oppositeUnchanged =
                currentRanges[1] == unscaled[1] && currentRanges[2] == unscaled[2];
            bool scaled = Mathf.Approximately(currentRanges[0], unscaled[0] * modifier);
            bool matched =
                scaled
                && oppositeUnchanged
                && actual
                    .Select((range, i) => Math.Abs(range - expected[i]) <= 1)
                    .All(equal => equal);
            passed &= matched;
            samples.Add(
                new
                {
                    phase,
                    modifier,
                    tick = Find.TickManager.TicksGame,
                    actual,
                    expected,
                    matched,
                    oppositeUnchanged,
                    scaled,
                    currentRanges,
                    unscaled,
                    colonyMech = pawns[0].ThingID,
                    colonist = pawns[1].ThingID,
                    hostileMech = pawns[2].ThingID,
                    mechTargetingRange = pawns[0]
                        .TryGetComp<CompFog>()
                        .FieldOfViewWatcher.CalcPawnSightRange(pawns[0].Position, true, false),
                }
            );
        }
    }
}
