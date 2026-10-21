using System;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class TransportPodArrivalTests : IDisposable
{
    private readonly bool previous = FogSettings.InteractWithRememberedObjects;

    public TransportPodArrivalTests() => FogSettings.InteractWithRememberedObjects = true;

    public void Dispose() => FogSettings.InteractWithRememberedObjects = previous;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitArrivalMovesExistingKnowledgeWithoutGrantingSight(bool explored)
    {
        var cargo = Cargo(observed: true);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        var map = Destination(explored);
        Spawn(cargo, map, new IntVec3(1, 1));

        Assert.True(ThingVisibility.CanInspect(cargo));
        Assert.True(
            cargo.TryGetComp<CompFog>().HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
        );
        Assert.True(map.Fog.knownCells[3]);
        Assert.False(map.Fog.knownCells[0]);
        Assert.False(map.Fog.InSight[3]);
        Assert.False(Visibility.IsVisible(cargo));
    }

    [Fact]
    public void ArrivalDoesNotObserveUnknownPayloadOrShareOtherFactionsKnowledge()
    {
        var cargo = Cargo(observed: false);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        Spawn(cargo, Destination(true), new IntVec3(1, 1));
        Assert.False(ThingVisibility.CanInspect(cargo));
        Assert.False(cargo.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));

        cargo = Cargo(observed: true);
        cargo
            .TryGetComp<CompFog>()
            .HideFromPlayer.RememberNextArrival(new Faction { IsPlayer = true, loadID = 2 });
        Spawn(cargo, Destination(true), new IntVec3(1, 1));
        Assert.False(ThingVisibility.CanInspect(cargo));
    }

    [Fact]
    public void ArrivalPermissionIsConsumedAndDoesNotAuthorizeAnotherHiddenMove()
    {
        var cargo = Cargo(observed: true);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        var map = Destination(true);
        Spawn(cargo, map, new IntVec3(1, 1));
        Assert.True(ThingVisibility.CanInspect(cargo));

        map.Fog.knownCells[0] = true;
        Spawn(cargo, map, new IntVec3(0, 0));
        Assert.False(ThingVisibility.CanInspect(cargo));
    }

    [Fact]
    public void OrdinaryPodSpawnCannotMoveKnowledgeWithoutThePlayerArrivalAction()
    {
        var cargo = Cargo(observed: true);
        Spawn(cargo, Destination(true), new IntVec3(1, 1));
        Assert.False(ThingVisibility.CanInspect(cargo));
        Assert.False(
            cargo.TryGetComp<CompFog>().HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
        );
    }

    private static ThingWithComps Cargo(bool observed)
    {
        var cargo = new ThingWithComps();
        cargo.def.category = ThingCategory.Item;
        cargo.Map.Fog.Initialized = true;
        cargo.Map.Fog.knownCells[0] = true;
        cargo.Map.Fog.InSight[0] = observed;
        var comp = new CompFog();
        comp.HideFromPlayer = new CompVisibility { parent = cargo, mainComponent = comp };
        cargo.Component = comp;
        comp.HideFromPlayer.PostSpawnSetup(false);
        cargo.Map.Fog.InSight[0] = false;
        return cargo;
    }

    private static Map Destination(bool explored)
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.knownCells[3] = explored;
        map.fogGrid.Fogged = !explored;
        return map;
    }

    private static void Spawn(ThingWithComps cargo, Map map, IntVec3 cell)
    {
        cargo.Map = map;
        cargo.PositionHeld = cell;
        cargo.TryGetComp<CompFog>().HideFromPlayer.PostSpawnSetup(false);
    }
}
