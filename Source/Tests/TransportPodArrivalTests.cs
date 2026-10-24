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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrivingKnownCargoKeepsItsLocationWhenAbsorbedByAnotherStack(bool explored)
    {
        var cargo = Cargo(observed: true);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        var receiver = Cargo(observed: false);
        var map = Destination(explored);
        Spawn(receiver, map, new IntVec3(1, 1));

        receiver.TryGetComp<CompFog>().HideFromPlayer.AbsorbArrival(cargo, 5);

        Assert.True(ThingVisibility.CanInspect(receiver));
        Assert.True(
            receiver.TryGetComp<CompFog>().HideFromPlayer.WasSeenAtCurrentLocation(Faction.OfPlayer)
        );
        Assert.True(map.Fog.knownCells[3]);
        Assert.False(map.Fog.InSight[3]);
        Assert.False(Visibility.IsVisible(receiver));
        // A partially absorbed arrival must still authorize its remaining stack.
        Spawn(cargo, map, new IntVec3(0, 0));
        Assert.True(ThingVisibility.CanInspect(cargo));
    }

    [Theory]
    [InlineData(false, true, 5)]
    [InlineData(true, false, 5)]
    [InlineData(true, true, 0)]
    public void OrdinaryUnknownAndEmptyMergesDoNotGrantArrivalKnowledge(
        bool observed,
        bool arrival,
        int count
    )
    {
        var cargo = Cargo(observed);
        if (arrival)
            cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        var receiver = Cargo(observed: false);
        var map = Destination(true);
        Spawn(receiver, map, new IntVec3(1, 1));

        receiver.TryGetComp<CompFog>().HideFromPlayer.AbsorbArrival(cargo, count);

        Assert.False(ThingVisibility.CanInspect(receiver));
        Assert.False(receiver.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownSplitCargoRetainsKnowledgeBeforeOrAfterArrivalIsMarked(bool markBeforeSplit)
    {
        var cargo = Cargo(observed: true);
        var source = cargo.TryGetComp<CompFog>().HideFromPlayer;
        if (markBeforeSplit)
            source.RememberNextArrival(Faction.OfPlayer);
        var split = Cargo(observed: false);
        source.CopySplitKnowledgeTo(split.TryGetComp<CompFog>().HideFromPlayer);
        Assert.True(split.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        if (!markBeforeSplit)
            split.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        var map = Destination(false);
        Spawn(split, map, new IntVec3(1, 1));
        Assert.True(ThingVisibility.CanInspect(split));
        Assert.False(Visibility.IsVisible(split));
        Assert.True(source.WasSeenBy(Faction.OfPlayer));
        // A consumed copied marker cannot authorize another hidden relocation.
        map.Fog.knownCells[0] = true;
        Spawn(split, map, new IntVec3(0, 0));
        Assert.False(ThingVisibility.CanInspect(split));
    }

    [Fact]
    public void UnknownSplitCargoCannotGainKnowledgeThroughAnArrival()
    {
        var cargo = Cargo(observed: false);
        var split = Cargo(observed: false);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        cargo
            .TryGetComp<CompFog>()
            .HideFromPlayer.CopySplitKnowledgeTo(split.TryGetComp<CompFog>().HideFromPlayer);
        split.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        Spawn(split, Destination(true), new IntVec3(1, 1));
        Assert.False(split.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        Assert.False(ThingVisibility.CanInspect(split));
    }

    [Fact]
    public void SplitKnowledgeRetainsItsOldLocationWithoutGrantingAnOrdinaryHiddenMove()
    {
        var cargo = Cargo(observed: true);
        var split = Cargo(observed: false);
        cargo
            .TryGetComp<CompFog>()
            .HideFromPlayer.CopySplitKnowledgeTo(split.TryGetComp<CompFog>().HideFromPlayer);
        Spawn(split, cargo.Map, cargo.PositionHeld);
        Assert.True(ThingVisibility.CanInspect(split));
        Spawn(split, Destination(true), new IntVec3(1, 1));
        Assert.True(split.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        Assert.False(ThingVisibility.CanInspect(split));
    }

    [Fact]
    public void SplitKnowledgeDoesNotResurrectAConsumedArrivalPermission()
    {
        var cargo = Cargo(observed: true);
        cargo.TryGetComp<CompFog>().HideFromPlayer.RememberNextArrival(Faction.OfPlayer);
        Spawn(cargo, Destination(true), new IntVec3(1, 1));
        var split = Cargo(observed: false);
        cargo
            .TryGetComp<CompFog>()
            .HideFromPlayer.CopySplitKnowledgeTo(split.TryGetComp<CompFog>().HideFromPlayer);
        Assert.True(split.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(Faction.OfPlayer));
        Spawn(split, Destination(true), new IntVec3(1, 1));
        Assert.False(ThingVisibility.CanInspect(split));
    }

    [Fact]
    public void SplitCopiesFactionHistoryWithoutSharingMutableObservationRecords()
    {
        var cargo = Cargo(observed: true);
        var source = cargo.TryGetComp<CompFog>().HideFromPlayer;
        var second = new Faction { loadID = 2, IsPlayer = true };
        var third = new Faction { loadID = 3, IsPlayer = true };
        cargo.Map.Fog.FactionSight[second] = new[] { true, false, false, false };
        cargo.Map.Fog.FactionKnown[second] = new[] { true, false, false, false };
        source.RecordObservation(second, cargo.Map.Fog);
        var split = Cargo(observed: false);
        var child = split.TryGetComp<CompFog>().HideFromPlayer;
        source.CopySplitKnowledgeTo(child);
        Spawn(split, cargo.Map, cargo.PositionHeld);
        Assert.True(child.WasSeenAtCurrentLocation(second));
        cargo.Map.Fog.FactionSight[third] = new[] { true, false, false, false };
        cargo.Map.Fog.FactionKnown[third] = new[] { true, false, false, false };
        child.RecordObservation(third, cargo.Map.Fog);
        Assert.True(child.WasSeenBy(third));
        Assert.False(source.WasSeenBy(third));
        cargo.Map.Fog.FactionSight[second][3] = true;
        cargo.Map.Fog.FactionKnown[second][3] = true;
        Spawn(split, cargo.Map, new IntVec3(1, 1));
        child.RecordObservation(second, cargo.Map.Fog);
        Assert.True(child.WasSeenAtCurrentLocation(second));
        Assert.True(source.WasSeenAtCurrentLocation(second));
        Assert.True(source.WasSeenAtCurrentLocation(Faction.OfPlayer));
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
