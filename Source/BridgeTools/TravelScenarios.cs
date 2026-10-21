using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.Planet;
using TotalFog.Presentation;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class TravelScenarios
{
    [Tool(
        "totalfog/travel_arrival",
        Description = "Test native gravship packing/placement, caravan entry or transport-pod landing between maps in an isolated paused fixture. Creates a second native map if needed. Status inspects saved cargo by ID. Reload the unchanged fixture afterward."
    )]
    public static async Task<object> Arrival(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string operation = "gravship",
        int rotation = 0,
        int destinationMapID = -1,
        string thingID = "",
        bool stageOnly = false
    )
    {
        var samples = new List<object>();
        Thing cargo = null;
        Thing unknownCargo = null;
        Pawn viewer = null;
        Func<object> place = null;
        var result = await ctx.MainThread.InvokeAsync<object>(
            () =>
            {
                var origin = Find.CurrentMap;
                if (
                    origin == null
                    || !Find.TickManager.Paused
                    || !origin.GetVisibility().Initialized
                )
                    throw new InvalidOperationException(
                        "Use a paused initialized isolated fixture."
                    );
                if (operation == "status")
                    return State(
                        Find.Maps.SelectMany(m => m.listerThings.AllThings)
                            .Single(t => t.ThingID == thingID)
                    );
                if (operation is not ("gravship" or "caravan" or "pod" or "pod-cargo"))
                    throw new ArgumentException("Use gravship, caravan, pod, pod-cargo or status.");
                if (rotation < 0 || rotation > 3)
                    throw new ArgumentOutOfRangeException(nameof(rotation));
                var destination =
                    destinationMapID < 0
                        ? CreateDestination(origin)
                        : Find.Maps.Single(m => m.uniqueID == destinationMapID);
                if (destination == origin)
                    throw new ArgumentException("Choose another map.");
                var start = ClearPatch(
                    origin,
                    origin.AllCells.First(c =>
                        c.x > 12
                        && c.z > 12
                        && c.x < origin.Size.x - 12
                        && c.z < origin.Size.z - 12
                        && CellRect
                            .CenteredOn(c, 9)
                            .All(p =>
                                !origin.GetVisibility().IsShown(Faction.OfPlayer, p)
                                && p.GetThingList(origin).All(t => t is not Pawn)
                            )
                    ),
                    9
                );
                var landing = ClearPatch(destination, destination.Center, 11);
                viewer = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(viewer, start + new IntVec3(-3, 0, 0), origin);
                viewer.drafter.Drafted = true;
                origin.fogGrid.Unfog(viewer.Position);
                cargo = GenSpawn.Spawn(
                    ThingMaker.MakeThing(
                        DefDatabase<ThingDef>.GetNamed(
                            operation == "gravship" ? "TableStonecutter" : "MedicineIndustrial"
                        ),
                        operation == "gravship" ? ThingDefOf.WoodLog : null
                    ),
                    start + new IntVec3(-4, 0, 0),
                    origin
                );
                viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                cargo.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
                samples.Add(new { phase = "observed-at-origin", cargo = State(cargo) });
                if (
                    !cargo
                        .TryGetComp<CompFog>()
                        .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
                )
                    throw new InvalidOperationException(
                        "Native sight did not observe the fixture cargo."
                    );
                if (operation == "gravship")
                {
                    viewer.Position = origin.AllCells.First(c =>
                        c.Standable(origin) && c.DistanceTo(start) > FogSettings.BaseViewRange + 30
                    );
                    origin.fogGrid.Unfog(viewer.Position);
                    viewer.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    var floor = new CellRect(start.x - 7, start.z - 4, 15, 9);
                    foreach (var cell in floor)
                        origin.terrainGrid.SetFoundation(cell, TerrainDefOf.Substructure);
                    var engine = (Building_GravEngine)
                        GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.GravEngine), start, origin);
                    engine.SetFaction(Faction.OfPlayer);
                    var console = GenSpawn.Spawn(
                        ThingMaker.MakeThing(ThingDefOf.PilotConsole),
                        start + new IntVec3(0, 0, 2),
                        origin
                    );
                    console.SetFaction(Faction.OfPlayer);
                    engine.AddComponent(console.TryGetComp<CompGravshipFacility>());
                    foreach (var cell in floor.EdgeCells)
                    {
                        var wall = GenSpawn.Spawn(
                            ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                            cell,
                            origin
                        );
                        wall.SetFaction(Faction.OfPlayer);
                    }
                    var unseen = GenSpawn.Spawn(
                        ThingMaker.MakeThing(ThingDefOf.Stool, ThingDefOf.WoodLog),
                        start + new IntVec3(5, 0, 0),
                        origin
                    );
                    place = () =>
                        Place(
                            engine,
                            cargo,
                            unseen,
                            landing,
                            destination,
                            origin,
                            rotation,
                            samples
                        );
                    return null;
                }
                cargo.DeSpawn();
                if (operation == "caravan")
                {
                    viewer.inventory.innerContainer.TryAdd(cargo);
                    var caravan = CaravanExitMapUtility.ExitMapAndCreateCaravan(
                        new[] { viewer },
                        viewer.Faction,
                        origin.Tile,
                        origin.Tile,
                        PlanetTile.Invalid,
                        sendMessage: false
                    );
                    caravan.Tile = destination.Tile;
                    CaravanEnterMapUtility.Enter(
                        caravan,
                        destination,
                        _ => landing,
                        CaravanDropInventoryMode.DropInstantly,
                        draftColonists: true
                    );
                    samples.Add(new { phase = "native-caravan-arrival", cargo = State(cargo) });
                }
                else
                {
                    // Represent a previously explored landing area. No observer
                    // is present there, so this cannot observe arriving cargo.
                    foreach (var cell in CellRect.CenteredOn(landing, 5))
                    {
                        destination.fogGrid.Unfog(cell);
                        destination
                            .GetVisibility()
                            .RevealCell(destination.cellIndices.CellToIndex(cell));
                    }
                    var info = new ActiveTransporterInfo { openDelay = 110, leaveSlag = false };
                    info.innerContainer.TryAdd(cargo);
                    if (operation == "pod")
                    {
                        viewer.DeSpawn();
                        info.innerContainer.TryAdd(viewer);
                    }
                    else
                    {
                        unknownCargo = ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
                        info.innerContainer.TryAdd(unknownCargo);
                    }
                    new TransportersArrivalAction_LandInSpecificCell(
                        destination.Parent,
                        landing
                    ).Arrived(new List<ActiveTransporterInfo> { info }, destination.Tile);
                    samples.Add(new { phase = "in-native-pod", cargo = State(cargo) });
                    if (stageOnly)
                        return new
                        {
                            staged = true,
                            cargo = State(cargo),
                            unknown = unknownCargo == null ? null : State(unknownCargo),
                            destination = destination.uniqueID,
                        };
                }
                return null;
            },
            cancellationToken
        );
        if (result != null)
            return result;
        if (place != null)
        {
            await ctx.Game.StepTicksAsync(1, cancellationToken: cancellationToken);
            return await ctx.MainThread.InvokeAsync(place, cancellationToken);
        }
        await ctx.Game.StepTicksAsync(
            operation.StartsWith("pod", StringComparison.Ordinal) ? 650 : 30,
            cancellationToken: cancellationToken
        );
        return await ctx.MainThread.InvokeAsync<object>(
            () =>
                new
                {
                    success = cargo.Spawned
                        && (
                            operation == "pod-cargo"
                                ? !cargo
                                    .Map.GetVisibility()
                                    .IsShown(Faction.OfPlayer, cargo.Position)
                                    && !ThingVisibility.CanInspect(unknownCargo)
                                    && !unknownCargo
                                        .TryGetComp<CompFog>()
                                        .HideFromPlayer.WasSeenBy(Faction.OfPlayer)
                                : viewer.Spawned && cargo.Map == viewer.Map
                        )
                        && cargo
                            .TryGetComp<CompFog>()
                            .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
                        && ThingVisibility.CanInspect(cargo),
                    samples,
                    arrived = State(cargo),
                    unknown = unknownCargo == null ? null : State(unknownCargo),
                    viewer = viewer.ThingID,
                    destination = cargo.Map?.uniqueID,
                },
            cancellationToken
        );
    }

    private static object Place(
        Building_GravEngine engine,
        Thing cargo,
        Thing unseen,
        IntVec3 landing,
        Map destination,
        Map origin,
        int rotation,
        List<object> samples
    )
    {
        var ship = new Gravship(engine) { Rotation = new Rot4(rotation) };
        if (ship.Pawns.Any())
            throw new InvalidOperationException("Keep all pawns off the empty-room ship fixture.");
        samples.Add(
            new
            {
                phase = "before-packing",
                cargo = State(cargo),
                unseen = State(unseen),
            }
        );
        if (unseen.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer))
            throw new InvalidOperationException(
                "The negative-control cargo was already observed at origin."
            );
        var carried = ship.Things.ToArray();
        if (!carried.Contains(cargo) || !carried.Contains(unseen))
            throw new InvalidOperationException(
                "Native packing did not include the fixture contents."
            );
        foreach (var thing in carried)
            thing.DeSpawn();
        var cells = ship
            .OccupiedRects.SelectMany(r => r)
            .Select(c => landing + c)
            .Distinct()
            .ToArray();
        int knownBefore = destination.GetVisibility().knownCells.Count(k => k);
        GravshipPlacementUtility.PlaceGravshipInMap(ship, landing, destination, out _);
        samples.Add(
            new
            {
                phase = "native-gravship-arrival",
                cargo = State(cargo),
                unseen = State(unseen),
            }
        );
        bool floorKnown = cells.All(c =>
            destination
                .GetVisibility()
                .IsKnown(Faction.OfPlayer, destination.cellIndices.CellToIndex(c))
            && !destination.fogGrid.IsFogged(c)
        );
        int added = destination.GetVisibility().knownCells.Count(k => k) - knownBefore;
        return new
        {
            success = ThingVisibility.CanInspect(cargo)
                && cargo
                    .TryGetComp<CompFog>()
                    .HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
                && !destination.GetVisibility().IsShown(Faction.OfPlayer, cargo.Position)
                && !unseen.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer)
                && !ThingVisibility.CanInspect(unseen)
                && floorKnown
                && added == cells.Length,
            origin = origin.uniqueID,
            destination = destination.uniqueID,
            rotation,
            floorCells = cells.Length,
            addedKnownCells = added,
            floorKnown,
            samples,
        };
    }

    private static object State(Thing thing) =>
        new
        {
            id = thing.ThingID,
            map = thing.Map?.uniqueID,
            thing.Spawned,
            thing.Destroyed,
            position = thing.PositionHeld.ToString(),
            seen = thing.TryGetComp<CompFog>()?.HideFromPlayer?.WasSeenBy(Faction.OfPlayer),
            seenHere = thing
                .TryGetComp<CompFog>()
                ?.HideFromPlayer?.WasSeenAtCurrentLocation(Faction.OfPlayer),
            known = thing.Map != null
                && thing
                    .Map.GetVisibility()
                    .IsKnown(Faction.OfPlayer, thing.Map.cellIndices.CellToIndex(thing.Position)),
            sight = thing.Map != null
                && thing.Map.GetVisibility().IsShown(Faction.OfPlayer, thing.Position),
            inspect = ThingVisibility.CanInspect(thing),
        };

    private static Map CreateDestination(Map origin)
    {
        var neighbors = new List<PlanetTile>();
        Find.WorldGrid.GetTileNeighbors(origin.Tile, neighbors);
        var tile = neighbors.First(t =>
            !Find.WorldObjects.AnyMapParentAt(t) && !Find.World.Impassable(t)
        );
        var parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
        parent.Tile = tile;
        parent.SetFaction(Faction.OfPlayer);
        Find.WorldObjects.Add(parent);
        var destination = MapGenerator.GenerateMap(
            new IntVec3(120, 1, 120),
            parent,
            MapGeneratorDefOf.Encounter
        );
        destination.GetVisibility().MapComponentTick();
        return destination;
    }

    private static IntVec3 ClearPatch(Map map, IntVec3 center, int radius)
    {
        foreach (var cell in CellRect.CenteredOn(center, radius))
        {
            foreach (var thing in cell.GetThingList(map).ToArray())
                if (thing is not Pawn)
                    thing.Destroy();
            map.roofGrid.SetRoof(cell, null);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
        }
        return center;
    }
}
