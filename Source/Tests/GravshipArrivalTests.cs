using System;
using RimWorld;
using RimWorld.Planet;
using TotalFog.Compatibility;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class GravshipArrivalTests : IDisposable
{
    private readonly bool previous = FogSettings.InteractWithRememberedObjects;

    public GravshipArrivalTests() => FogSettings.InteractWithRememberedObjects = true;

    public void Dispose() => FogSettings.InteractWithRememberedObjects = previous;

    [Fact]
    public void LandingRestoresSeenShipContentsAtTheirNewMapAndCell()
    {
        var thing = Cargo(observed: true);
        var destination = Destination();
        Move(thing, destination);
        Assert.False(ThingVisibility.CanInspect(thing));

        GravshipVisibility.Placed(Ship(thing), new IntVec3(1, 1), destination);

        Assert.True(ThingVisibility.CanInspect(thing));
        Assert.True(thing.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        Assert.False(Visibility.IsVisible(thing));
        Assert.False(destination.Fog.InSight[3]);
    }

    [Fact]
    public void LandingOnlyExploresTheCarriedFootprint()
    {
        var thing = Cargo(observed: true);
        var destination = Destination();
        Move(thing, destination);
        GravshipVisibility.Placed(Ship(thing), new IntVec3(1, 1), destination);

        Assert.Equal(new[] { false, false, false, true }, destination.Fog.knownCells);
        Assert.False(destination.fogGrid.IsFogged(new IntVec3(1, 1)));
        Assert.True(destination.fogGrid.IsFogged(new IntVec3(0, 0)));
    }

    [Fact]
    public void LandingDoesNotDiscoverUnseenCargo()
    {
        var thing = Cargo(observed: false);
        var destination = Destination();
        Move(thing, destination);
        GravshipVisibility.Placed(Ship(thing), new IntVec3(1, 1), destination);

        Assert.True(destination.Fog.knownCells[3]);
        Assert.False(thing.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        Assert.False(ThingVisibility.CanInspect(thing));
        Assert.False(Visibility.IsVisible(thing));
    }

    [Fact]
    public void EnemyShipsDoNotRevealTheirFootprintOrContents()
    {
        var thing = Cargo(observed: true);
        var destination = Destination();
        Move(thing, destination);
        var ship = Ship(thing);
        ship.Engine.Faction = new Faction { loadID = 3 };
        GravshipVisibility.Placed(ship, new IntVec3(1, 1), destination);

        Assert.Equal(new bool[4], destination.Fog.knownCells);
        Assert.True(destination.fogGrid.IsFogged(thing.Position));
        Assert.False(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void LandingDoesNotShareExplorationWithOtherPlayerFactions()
    {
        var thing = Cargo(observed: true);
        var destination = Destination();
        var other = new Faction { IsPlayer = true, loadID = 2 };
        destination.Fog.FactionKnown[other] = new bool[4];
        Move(thing, destination);
        GravshipVisibility.Placed(Ship(thing), new IntVec3(1, 1), destination);

        Assert.True(destination.Fog.IsKnown(Faction.OfPlayer, 3));
        Assert.False(destination.Fog.IsKnown(other, 3));
        Assert.False(ThingVisibility.IsVisible(thing, observerFaction: other));
    }

    private static ThingWithComps Cargo(bool observed)
    {
        var thing = new ThingWithComps();
        thing.def.category = ThingCategory.Building;
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.Fog.InSight[0] = observed;
        var comp = new CompFog();
        comp.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = comp };
        thing.Component = comp;
        comp.HideFromPlayer.PostSpawnSetup(false);
        thing.Map.Fog.InSight[0] = false;
        return thing;
    }

    private static Map Destination()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.fogGrid.Fogged = true;
        return map;
    }

    private static void Move(ThingWithComps thing, Map map)
    {
        thing.Map = map;
        thing.PositionHeld = new IntVec3(1, 1);
        thing.TryGetComp<CompFog>().HideFromPlayer.PostSpawnSetup(false);
    }

    private static Gravship Ship(Thing thing)
    {
        var ship = new Gravship { Engine = new Building_GravEngine { Faction = Faction.OfPlayer } };
        ship.OccupiedRects.Add(CellRect.SingleCell(new IntVec3(0, 0)));
        ship.Things.Add(thing);
        return ship;
    }
}
