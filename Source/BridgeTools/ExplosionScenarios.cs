using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class ExplosionScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Map probeMap;
    private static IntVec3 probeCell;
    private static int shakes,
        heatCalls,
        affectedCells;
    private static float shakeMagnitude;

    [Tool(
        "totalfog/explosion_camera",
        Description = "Run native bomb, Zombieland suicide-bomb, toxic-splatter and electrical-shock explosions through hidden/visible/hidden-again/colony-bypass controls. Observe real camera-shake calls, heat and cell damage processing, then step the native blast wave to completion. This stages explosion producers, not zombie attack AI or particle pixels. Restores sight/options/probes and reloads the unchanged named fixture."
    )]
    public static async Task<object> Camera(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        int x = 212,
        int z = 79
    )
    {
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.explosion-camera-probe");
        MapVisibility fog = null;
        bool oldBypass = FogSettings.OnlyOutsideColony,
            addedSight = false,
            passed = true;
        Thing wall = null;
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    probeMap =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    probeCell = new IntVec3(x, 0, z);
                    fog = probeMap.GetVisibility();
                    if (
                        !Find.TickManager.Paused
                        || !fog.Initialized
                        || !probeMap.IsPlayerHome
                        || !probeCell.InBounds(probeMap)
                        || probeMap.fogGrid.IsFogged(probeCell)
                        || fog.IsShown(Faction.OfPlayer, probeCell)
                        || probeCell.GetEdifice(probeMap) != null
                        || GenRadial
                            .RadialCellsAround(probeCell, 8, true)
                            .Any(c =>
                                !c.InBounds(probeMap)
                                || c.GetThingList(probeMap).OfType<Pawn>().Any()
                            )
                    )
                        throw new InvalidOperationException(
                            "Use a paused, explored remote home-map cell without nearby pawns."
                        );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(CameraShaker),
                            nameof(CameraShaker.DoShake),
                            new[] { typeof(float) }
                        ),
                        prefix: new HarmonyMethod(typeof(ExplosionScenarios), nameof(ObserveShake))
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(GenTemperature),
                            nameof(GenTemperature.PushHeat),
                            new[] { typeof(IntVec3), typeof(Map), typeof(float) }
                        ),
                        prefix: new HarmonyMethod(typeof(ExplosionScenarios), nameof(ObserveHeat))
                    );
                    harmony.Patch(
                        AccessTools.Method(
                            typeof(DamageWorker),
                            nameof(DamageWorker.ExplosionAffectCell)
                        ),
                        postfix: new HarmonyMethod(typeof(ExplosionScenarios), nameof(ObserveCell))
                    );
                },
                cancellationToken
            );
            var camera = await ctx.Tools.CallAsync(
                "rimworld/jump_camera_to_cell",
                new { x, z },
                cancellationToken: cancellationToken
            );
            if (!camera.Succeeded())
                throw new InvalidOperationException("Explosion camera setup failed.");
            foreach (
                string kind in new[]
                {
                    "engine-bomb",
                    "suicide-bomb",
                    "toxic-splatter",
                    "electrical-shock",
                }
            )
            foreach (string state in new[] { "hidden", "visible", "hidden-again", "colony-bypass" })
            {
                int hitPoints = 0,
                    tick = 0,
                    shakeCalls = 0;
                float magnitude = 0,
                    nativeMagnitude = 0;
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        SetSight(state == "visible");
                        FogSettings.OnlyOutsideColony = state == "colony-bypass";
                        if (wall?.Destroyed == false)
                            wall.Destroy();
                        wall = GenSpawn.Spawn(
                            ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                            probeCell,
                            probeMap
                        );
                        hitPoints = wall.HitPoints;
                        tick = Find.TickManager.TicksGame;
                        Find.CameraDriver.shaker.StopAllShaking();
                        shakes = heatCalls = affectedCells = 0;
                        shakeMagnitude = 0;
                        if (kind == "suicide-bomb")
                        {
                            var type =
                                AccessTools.TypeByName("ZombieLand.Explosion")
                                ?? throw new InvalidOperationException("Zombieland is required.");
                            AccessTools
                                .Method(type, "Explode")
                                .Invoke(Activator.CreateInstance(type, probeMap, probeCell), null);
                        }
                        else
                        {
                            var def =
                                kind == "engine-bomb"
                                    ? DamageDefOf.Bomb
                                    : DefDatabase<DamageDef>.GetNamed(
                                        kind == "toxic-splatter"
                                            ? "ToxicSplatter"
                                            : "ElectricalShock"
                                    );
                            GenExplosion.DoExplosion(
                                probeCell,
                                probeMap,
                                1.9f,
                                def,
                                null,
                                damAmount: 10
                            );
                        }
                        shakeCalls = shakes;
                        magnitude = shakeMagnitude;
                        nativeMagnitude = Find.CameraDriver.shaker.CurShakeMag;
                    },
                    cancellationToken
                );
                await ctx.Game.StepTicksAsync(60, cancellationToken: cancellationToken);
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool visible = state is "visible" or "colony-bypass";
                        bool damageExpected = kind is "engine-bomb" or "suicide-bomb";
                        bool damage = wall.Destroyed || wall.HitPoints < hitPoints;
                        bool heatExpected = damageExpected;
                        bool completed = !probeMap
                            .listerThings.ThingsOfDef(ThingDefOf.Explosion)
                            .Any();
                        bool valid =
                            affectedCells > 0
                            && completed
                            && (!damageExpected || damage)
                            && (heatExpected ? heatCalls > 0 : heatCalls == 0)
                            && (
                                visible
                                    ? shakeCalls == 1 && magnitude > 0 && nativeMagnitude > 0
                                    : shakeCalls == 0 && nativeMagnitude == 0
                            );
                        passed &= valid;
                        rows.Add(
                            new
                            {
                                kind,
                                state,
                                passed = valid,
                                shakeCalls,
                                magnitude,
                                nativeMagnitude,
                                heatCalls,
                                affectedCells,
                                damage,
                                completed,
                                elapsedNativeTicks = Find.TickManager.TicksGame - tick,
                            }
                        );
                        Find.CameraDriver.shaker.StopAllShaking();
                    },
                    cancellationToken
                );
            }
            return new
            {
                passed,
                stagedProducers = true,
                naturalZombieAttack = false,
                particlePixels = false,
                rows,
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
                        if (wall?.Destroyed == false)
                            wall.Destroy();
                        if (addedSight)
                            SetSight(false);
                        FogSettings.OnlyOutsideColony = oldBypass;
                        Find.CameraDriver?.shaker.StopAllShaking();
                        probeMap = null;
                    },
                    CancellationToken.None
                );
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
                if (!reload.Succeeded())
                    throw new InvalidOperationException("Explosion fixture restoration failed.");
            }
            finally
            {
                gate.Release();
            }
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight)
                return;
            int index = probeMap.cellIndices.CellToIndex(probeCell);
            if (visible)
                fog.IncrementSeen(Faction.OfPlayer, index);
            else
                fog.DecrementSeen(Faction.OfPlayer, index);
            addedSight = visible;
        }
    }

    private static void ObserveShake(float mag)
    {
        if (probeMap == null)
            return;
        shakes++;
        shakeMagnitude += mag;
    }

    private static void ObserveHeat(IntVec3 c, Map map, float energy)
    {
        if (map == probeMap && c == probeCell && energy > 0)
            heatCalls++;
    }

    private static void ObserveCell(Explosion explosion)
    {
        if (explosion.Map == probeMap && explosion.Position == probeCell)
            affectedCells++;
    }
}
