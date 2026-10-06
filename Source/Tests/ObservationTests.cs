using RimWorld;
using TotalFog;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class ObservationTests
{
    private static ThingWithComps Item(bool initialized = true)
    {
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = initialized;
        item.Map.Fog.knownCells[0] = true;
        var main = new CompFog();
        main.Hiddenable = new CompPresentationState { parent = item, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = item, mainComponent = main };
        item.Component = main;
        main.Hiddenable.PostSpawnSetup(false);
        main.HideFromPlayer.PostSpawnSetup(false);
        return item;
    }

    [Fact]
    public void NewlySpawnedItemInExploredCellNeedsObservation()
    {
        var item = Item();
        Assert.False(ThingVisibility.IsVisible(item));
        Assert.True(item.TryGetComp<CompFog>().Hiddenable.Hidden);
    }

    [Fact]
    public void ObservedItemRemainsRememberedWhenSightIsLost()
    {
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.IsVisible(item));
        item.Map.Fog.InSight[0] = false;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.IsVisible(item));
        Assert.False(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void InitializationFallbackDoesNotInventAnObservation()
    {
        var item = Item(initialized: false);
        Assert.True(ThingVisibility.IsVisible(item));
        item.Map.Fog.Initialized = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        Assert.False(ThingVisibility.IsVisible(item));
    }

    [Fact]
    public void ARecentCoverageEventDoesNotRepeatItsVisibilityQueryAtTheOldDeadline()
    {
        Find.TickManager.TicksGame = 0;
        var item = Item();
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.CompTick();
        Find.TickManager.TicksGame = 5;
        comp.HideFromPlayer.UpdateVisibility(true);
        int queries = item.Map.Fog.VisibilityQueries;
        Find.TickManager.TicksGame = 12;
        comp.HideFromPlayer.CompTick();
        Assert.Equal(queries, item.Map.Fog.VisibilityQueries);
        Find.TickManager.TicksGame = 17;
        comp.HideFromPlayer.CompTick();
        Assert.Equal(queries + 1, item.Map.Fog.VisibilityQueries);
    }

    [Fact]
    public void PeriodicFallbackStillCatchesVanillaFogWithoutAComponentSignal()
    {
        Find.TickManager.TicksGame = 5;
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.False(comp.Hiddenable.Hidden);
        item.Map.fogGrid.Fogged = true;
        Find.TickManager.TicksGame = 17;
        comp.HideFromPlayer.CompTick();
        Assert.True(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void ASignalUpdatesVisibilityImmediatelyDuringTheFallbackInterval()
    {
        Find.TickManager.TicksGame = 0;
        var item = Item();
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.CompTick();
        Find.TickManager.TicksGame = 1;
        item.Map.Fog.InSight[0] = true;
        comp.HideFromPlayer.ReceiveCompSignal("changed");
        Assert.True(comp.HideFromPlayer.SeenByPlayer);
        Assert.False(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void VisibilityAdapterUsesItsOwnObservationStateWithoutLookingUpItself()
    {
        var item = Item();
        var comp = item.TryGetComp<CompFog>();
        int queries = item.ComponentQueries;
        item.Map.Fog.InSight[0] = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        item.Map.Fog.InSight[0] = false;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(comp.HideFromPlayer.SeenByPlayer);
        Assert.False(comp.Hiddenable.Hidden);
        Assert.Equal(queries, item.ComponentQueries);
    }

    [Fact]
    public void RememberedPawnStillNeedsCurrentSight()
    {
        var pawn = new Pawn();
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.knownCells[0] = true;
        pawn.Map.Fog.InSight[0] = true;
        Assert.True(ThingVisibility.IsVisible(pawn));
        pawn.Map.Fog.InSight[0] = false;
        Assert.False(ThingVisibility.IsVisible(pawn));
    }

    [Fact]
    public void RememberedStaticObjectDoesNotRevealANewEvent()
    {
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.IsVisible(item, allowMemory: false));
        item.Map.Fog.InSight[0] = false;
        Assert.True(ThingVisibility.IsVisible(item));
        Assert.False(ThingVisibility.IsVisible(item, allowMemory: false));
    }

    [Fact]
    public void OwnedObjectOutsideSightDoesNotRevealANewEvent()
    {
        var item = Item();
        item.Faction = Faction.OfPlayer;
        Assert.False(ThingVisibility.IsVisible(item));
        Assert.False(ThingVisibility.IsVisible(item, allowMemory: false));
        item.Map.Fog.InSight[0] = true;
        Assert.True(ThingVisibility.IsVisible(item, allowMemory: false));
    }

    [Fact]
    public void OwnershipDoesNotInventAStaticObjectObservation()
    {
        var item = Item();
        item.Faction = Faction.OfPlayer;
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.False(comp.HideFromPlayer.SeenByPlayer);
        Assert.True(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void OwnedStaticObjectNeedsSightBeforeItCanBeRemembered()
    {
        var item = Item();
        item.Faction = Faction.OfPlayer;
        var comp = item.TryGetComp<CompFog>();
        item.Map.Fog.InSight[0] = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(comp.HideFromPlayer.SeenByPlayer);
        Assert.False(comp.Hiddenable.Hidden);
        item.Map.Fog.InSight[0] = false;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.IsVisible(item));
        Assert.False(ThingVisibility.IsVisible(item, allowMemory: false));
    }

    [Fact]
    public void OwnedProjectilesDoNotUseTheObserverBypass()
    {
        var projectile = new Thing { Faction = Faction.OfPlayer };
        projectile.def.category = ThingCategory.Projectile;
        projectile.Map.Fog.Initialized = true;
        Assert.False(ThingVisibility.IsVisible(projectile));
    }

    [Fact]
    public void PlayerPawnPresentationKeepsTheObserverBypass()
    {
        var pawn = new Pawn { Faction = Faction.OfPlayer };
        pawn.Map.Fog.Initialized = true;
        Assert.True(ThingVisibility.IsVisible(pawn));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void VanillaFogStillHidesOwnedPawnsBeforeAndAfterInitialization(
        bool initialized,
        bool flying
    )
    {
        Thing thing = flying
            ? new PawnFlyer { FlyingPawn = new Pawn { Faction = Faction.OfPlayer } }
            : new Pawn { Faction = Faction.OfPlayer };
        thing.Map.Fog.Initialized = initialized;
        thing.Map.Fog.InSight[0] = true;
        thing.Map.fogGrid.Fogged = true;
        Assert.False(ThingVisibility.IsVisible(thing));
        Assert.False(ThingVisibility.IsVisible(thing, allowMemory: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedPawnLiveInformationStillRequiresCurrentSight(bool flying)
    {
        Thing thing = flying
            ? new PawnFlyer { FlyingPawn = new Pawn { Faction = Faction.OfPlayer } }
            : new Pawn { Faction = Faction.OfPlayer };
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        Assert.True(ThingVisibility.IsVisible(thing));
        Assert.False(ThingVisibility.IsVisible(thing, allowMemory: false));
        thing.Map.Fog.InSight[0] = true;
        Assert.True(ThingVisibility.IsVisible(thing, allowMemory: false));
    }

    [Fact]
    public void AnObservedStaticFootprintCanBeVisibleThroughANonAnchorCell()
    {
        var item = Item();
        item.SizeX = 2;
        item.Map.Fog.InSight[1] = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        item.Map.Fog.InSight[1] = false;
        item.Map.Fog.knownCells[0] = false;
        item.Map.Fog.knownCells[1] = true;
        Assert.True(ThingVisibility.IsVisible(item));
        Assert.False(ThingVisibility.IsVisible(item, allowMemory: false));
    }

    [Fact]
    public void RememberedObjectCannotRunLiveOverlayOrTooltip()
    {
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        InterfaceVisibility.DrawOverlay(item);
        Assert.Equal(1, item.OverlayCalls);
        Assert.Equal(item.Position, InterfaceVisibility.TooltipPosition(item));
        item.Map.Fog.InSight[0] = false;
        InterfaceVisibility.DrawOverlay(item);
        Assert.Equal(1, item.OverlayCalls);
        Assert.Equal(IntVec3.Invalid, InterfaceVisibility.TooltipPosition(item));
        Assert.True(ThingVisibility.IsVisible(item));
    }

    [Fact]
    public void OwnedObjectOutsideSightCannotRunLiveInterface()
    {
        var item = Item();
        item.Faction = Faction.OfPlayer;
        InterfaceVisibility.DrawOverlay(item);
        Assert.Equal(0, item.OverlayCalls);
        Assert.Equal(IntVec3.Invalid, InterfaceVisibility.TooltipPosition(item));
    }

    [Fact]
    public void MouseoverFiltersRememberedObjectsWithoutMutatingTheThingGrid()
    {
        var item = Item();
        var map = item.Map;
        map.Fog.InSight[0] = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        map.Fog.InSight[0] = false;
        var visible = new Thing { Map = map, PositionHeld = new IntVec3(1, 0) };
        map.Fog.InSight[1] = true;
        map.Things.AddRange(new[] { item, visible });
        Assert.Equal(new[] { visible }, InterfaceVisibility.MouseoverThings(item.Position, map));
        Assert.Equal(new Thing[] { item, visible }, map.Things);
    }

    [Fact]
    public void MouseoverReusesTheThingGridWhenEverythingIsVisible()
    {
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        item.Map.Things.Add(item);
        Assert.Same(item.Map.Things, InterfaceVisibility.MouseoverThings(item.Position, item.Map));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RememberedObjectsNeedCurrentSightToBecomeManualTargets(bool owned)
    {
        var item = Item();
        if (owned)
            item.Faction = Faction.OfPlayer;
        item.Map.Fog.InSight[0] = true;
        item.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        item.Map.Fog.InSight[0] = false;
        Assert.True(ThingVisibility.IsVisible(item));
        Assert.Empty(InterfaceVisibility.FilterTargetThings(new() { item }, item.Position));
        item.Map.Fog.InSight[0] = true;
        Assert.Equal(
            new[] { item },
            InterfaceVisibility.FilterTargetThings(new() { item }, item.Position)
        );
    }

    [Fact]
    public void SelectedRememberedObjectIsDeselectedWhenSightIsLost()
    {
        var item = Item();
        item.Map.Fog.InSight[0] = true;
        var comp = item.TryGetComp<CompFog>();
        comp.HideFromPlayer.UpdateVisibility(true);
        Find.Selector.Selected.Add(item);
        item.Map.Fog.InSight[0] = false;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.False(Find.Selector.IsSelected(item));
        Assert.False(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void ContextMenuFiltersBothCandidateListsWithoutChangingTheMap()
    {
        var hidden = new Pawn();
        var map = hidden.Map;
        map.Fog.Initialized = true;
        map.Fog.knownCells[0] = true;
        var visible = new Pawn { Map = map, PositionHeld = new IntVec3(1, 0) };
        map.Fog.InSight[1] = true;
        map.Things.AddRange(new Thing[] { hidden, visible });
        var context = new FloatMenuContext
        {
            ClickedCell = hidden.Position,
            ClickedThings = new() { hidden, visible },
            ClickedPawns = new() { hidden, visible },
        };
        InterfaceVisibility.ContextMenuPostfix(context);
        Assert.Equal(new Thing[] { visible }, context.ClickedThings);
        Assert.Equal(new[] { visible }, context.ClickedPawns);
        Assert.Equal(new Thing[] { hidden, visible }, map.Things);
        map.Fog.InSight[0] = true;
        context.ClickedThings.Add(hidden);
        context.ClickedPawns.Add(hidden);
        InterfaceVisibility.ContextMenuPostfix(context);
        Assert.Equal(new Thing[] { visible, hidden }, context.ClickedThings);
        Assert.Equal(new[] { visible, hidden }, context.ClickedPawns);
    }

    [Fact]
    public void VisibleSelectionProxyCannotRevealItsHiddenTargetThroughLiveUi()
    {
        var target = Item();
        target.Map.Fog.InSight[0] = true;
        target.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        target.Map.Fog.InSight[0] = false;
        var proxy = new Thing
        {
            Map = target.Map,
            PositionHeld = new IntVec3(1, 0),
            Proxy = new RimWorld.CompSelectProxy { thingToSelect = target },
        };
        proxy.Map.Fog.InSight[1] = true;
        proxy.Map.Things.Add(proxy);
        InterfaceVisibility.DrawOverlay(proxy);
        Assert.Equal(0, proxy.OverlayCalls);
        Assert.Equal(IntVec3.Invalid, InterfaceVisibility.TooltipPosition(proxy));
        Assert.Empty(InterfaceVisibility.MouseoverThings(proxy.Position, proxy.Map));
        target.Map.Fog.InSight[0] = true;
        InterfaceVisibility.DrawOverlay(proxy);
        Assert.Equal(1, proxy.OverlayCalls);
        Assert.Equal(proxy.Position, InterfaceVisibility.TooltipPosition(proxy));
        Assert.Same(
            proxy.Map.Things,
            InterfaceVisibility.MouseoverThings(proxy.Position, proxy.Map)
        );
    }
}
