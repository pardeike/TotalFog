using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class CornerWallScenarios
{
    [Tool(
        "totalfog/corner_walls",
        Description = "Check native sight in a room enclosed by corner-touching walls, then open and close a real wall on each side. Uses a paused isolated save, restores settings and reloads it unchanged."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName
    )
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/corner_walls");
        proof.saveName = saveName;
        var owned = new List<Thing>();
        var rows = new List<object>();
        int oldRange = FogSettings.BaseViewRange;
        bool oldOutside = FogSettings.OnlyOutsideColony;
        Map map = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map = Find.CurrentMap;
                    if (
                        map?.GetVisibility().Initialized != true
                        || !Find.TickManager.Paused
                        || Compatibility.MultiplayerIntegration.Active
                    )
                        throw new InvalidOperationException(
                            "Use a paused initialized single-player fixture."
                        );
                    var fog = map.GetVisibility();
                    var origin = map.AllCells.First(c =>
                        c.x > 4
                        && c.z > 4
                        && c.x < map.Size.x - 5
                        && c.z < map.Size.z - 5
                        && CellRect
                            .CenteredOn(c, 4)
                            .All(p =>
                                p.Standable(map)
                                && !fog.viewBlockerCells[map.cellIndices.CellToIndex(p)]
                                && !fog.IsShown(Faction.OfPlayer, p)
                                && p.GetThingList(map).All(t => t is not Pawn)
                            )
                    );
                    FogSettings.BaseViewRange = 24;
                    FogSettings.OnlyOutsideColony = false;
                    var walls = new Dictionary<IntVec3, Thing>();
                    for (int z = -3; z <= 3; z++)
                    for (int x = -3; x <= 3; x++)
                        if (Math.Abs(x) + Math.Abs(z) == 3)
                            walls.Add(
                                origin + new IntVec3(x, 0, z),
                                Wall(origin + new IntVec3(x, 0, z))
                            );
                    var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    owned.Add(pawn);
                    GenSpawn.Spawn(pawn, origin, map);
                    var source = pawn.TryGetComp<CompFog>().FieldOfViewWatcher;
                    source.UpdateFoV(true);
                    Check(
                        "native observer range reaches beyond the enclosure",
                        source.LastSightRange >= 6
                    );
                    Check(
                        "native walls register every diagonal blocker",
                        walls.Keys.All(c => fog.viewBlockerCells[map.cellIndices.CellToIndex(c)])
                    );
                    var outside = CellRect
                        .CenteredOn(origin, 4)
                        .Where(c =>
                            Math.Abs(c.x - origin.x) + Math.Abs(c.z - origin.z) > 3
                            && c.DistanceTo(origin) <= source.LastSightRange
                        )
                        .ToArray();
                    Check(
                        "sealed diagonal room hides all outside cells",
                        outside.All(c => !fog.IsShown(Faction.OfPlayer, c))
                    );
                    foreach (var direction in GenAdj.CardinalDirections)
                    {
                        var opening = origin + direction * 3;
                        var target = origin + direction * 4;
                        walls[opening].Destroy(DestroyMode.Vanish);
                        source.UpdateFoV(true);
                        bool removed = !fog.viewBlockerCells[map.cellIndices.CellToIndex(opening)];
                        bool open = fog.IsShown(Faction.OfPlayer, target);
                        Check("native opening admits sight " + direction, removed && open);
                        walls[opening] = Wall(opening);
                        source.UpdateFoV(true);
                        bool closed = !fog.IsShown(Faction.OfPlayer, target);
                        Check("native rebuild closes sight " + direction, closed);
                        Check(
                            "rebuild preserves legitimate exploration memory " + direction,
                            fog.IsKnown(Faction.OfPlayer, map.cellIndices.CellToIndex(target))
                        );
                        rows.Add(
                            new
                            {
                                direction = direction.ToString(),
                                removed,
                                open,
                                closed,
                            }
                        );
                    }
                    Check(
                        "all rebuilt sides seal the diagonal room",
                        outside.All(c => !fog.IsShown(Faction.OfPlayer, c))
                    );
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    proof.environment.details = new
                    {
                        origin = origin.ToString(),
                        rows,
                        source.LastSightRange,
                        gameplayMvid = typeof(MapVisibility)
                            .Assembly
                            .ManifestModule
                            .ModuleVersionId,
                        mods = LoadedModManager
                            .RunningModsListForReading.Select(m => m.PackageId)
                            .ToArray(),
                    };

                    Thing Wall(IntVec3 cell)
                    {
                        var wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                        owned.Add(wall);
                        wall.SetFaction(Faction.OfPlayer);
                        return GenSpawn.Spawn(wall, cell, map);
                    }
                },
                cancellationToken
            );
        }
        catch (Exception error)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy(DestroyMode.Vanish);
                    FogSettings.BaseViewRange = oldRange;
                    FogSettings.OnlyOutsideColony = oldOutside;
                },
                CancellationToken.None
            );
            if (map != null)
            {
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
                proof.assertions.Add(
                    RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
                );
            }
        }
        RimBridgeEvidence.Complete(proof);
        return proof;

        void Check(string name, bool passed) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, passed));
    }
}
