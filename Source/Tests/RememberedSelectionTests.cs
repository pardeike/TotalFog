using System;
using System.Collections.Generic;
using System.Linq;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class RememberedSelectionTests : IDisposable
{
    private sealed class RegisteredBench : ThingWithComps { }

    private readonly bool previousSetting = FogSettings.SelectRememberedObjects;

    public RememberedSelectionTests() => FogSettings.SelectRememberedObjects = true;

    public void Dispose() => FogSettings.SelectRememberedObjects = previousSetting;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SeenStaticThingCanBeSelectedInMemoryWithoutGrantingEventOrTargetSight(bool building)
    {
        var thing = ObservedThing(building);
        Assert.True(ThingVisibility.CanInspect(thing));
        Assert.False(Visibility.IsVisible(thing));
        Assert.Empty(InterfaceVisibility.FilterTargetThings(new() { thing }, thing.Position));
        Assert.Equal(thing.Position, InterfaceVisibility.TooltipPosition(thing));
        InterfaceVisibility.DrawOverlay(thing);
        Assert.Equal(1, thing.OverlayCalls);
    }

    [Fact]
    public void StrictOptionRequiresCurrentSightWhileLeavingRememberedDrawing()
    {
        var thing = ObservedThing();
        FogSettings.SelectRememberedObjects = false;
        Assert.False(ThingVisibility.CanInspect(thing));
        Assert.True(ThingVisibility.IsVisible(thing));
        thing.Map.Fog.InSight[0] = true;
        Assert.True(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void RejectedTooltipDoesNotDependOnTheEngineTreatingInvalidCellsAsFogged()
    {
        var thing = ObservedThing();
        FogSettings.SelectRememberedObjects = false;
        Assert.False(thing.Map.fogGrid.IsFogged(IntVec3.Invalid));
        Assert.True(
            InterfaceVisibility.TooltipFogged(InterfaceVisibility.TooltipPosition(thing), thing.Map)
        );
        FogSettings.SelectRememberedObjects = true;
        Assert.False(
            InterfaceVisibility.TooltipFogged(InterfaceVisibility.TooltipPosition(thing), thing.Map)
        );
        thing.Map.fogGrid.Fogged = true;
        Assert.True(InterfaceVisibility.TooltipFogged(thing.Position, thing.Map));
    }

    [Fact]
    public void UnknownFogAndNativeFogBlockRememberedSelection()
    {
        var thing = ObservedThing();
        thing.Map.Fog.knownCells[0] = false;
        Assert.False(ThingVisibility.CanInspect(thing));
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.fogGrid.Fogged = true;
        Assert.False(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void NewReplacementInKnownTerrainHasNoObservation()
    {
        var oldThing = ObservedThing();
        var replacement = ObservedThing(observe: false);
        replacement.Map = oldThing.Map;
        replacement.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        Assert.False(ThingVisibility.CanInspect(replacement));
    }

    [Fact]
    public void OwnershipAndHomeWorkAccessDoNotInventASelectionObservation()
    {
        var thing = ObservedThing(building: true, observe: false);
        thing.Faction = Faction.OfPlayer;
        thing.Map.areaManager.Home.Active = true;
        thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.IsActionable(thing, Faction.OfPlayer));
        Assert.False(thing.TryGetComp<CompFog>().HideFromPlayer.SeenByPlayer);
        Assert.False(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void MovingWhileHiddenRevokesSelectionUntilObservedAtTheNewPlace()
    {
        var thing = ObservedThing();
        var comp = thing.TryGetComp<CompFog>().HideFromPlayer;
        Find.Selector.Selected.Add(thing);
        try
        {
            thing.PositionHeld = new IntVec3(1, 0);
            thing.Map.Fog.knownCells[1] = true;
            comp.UpdateVisibility(true);
            Assert.False(Find.Selector.IsSelected(thing));
            Assert.False(ThingVisibility.CanInspect(thing));
            thing.Map.Fog.InSight[1] = true;
            comp.UpdateVisibility(true);
            thing.Map.Fog.InSight[1] = false;
            comp.UpdateVisibility(true);
            Assert.True(ThingVisibility.CanInspect(thing));
        }
        finally
        {
            Find.Selector.Selected.Remove(thing);
        }
    }

    [Fact]
    public void MovingWhileVisibleRefreshesTheLastObservedLocation()
    {
        var thing = ObservedThing();
        thing.Map.Fog.InSight[1] = true;
        thing.Map.Fog.knownCells[1] = true;
        thing.PositionHeld = new IntVec3(1, 0);
        thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        thing.Map.Fog.InSight[1] = false;
        thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        Assert.True(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void ChangingMapsAtTheSameCellDoesNotTransferLocationKnowledge()
    {
        var thing = ObservedThing();
        thing.Map = new Map();
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        thing.TryGetComp<CompFog>().HideFromPlayer.PostSpawnSetup(true);
        Assert.False(ThingVisibility.CanInspect(thing));
        thing.Map.Fog.InSight[0] = true;
        thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        thing.Map.Fog.InSight[0] = false;
        Assert.True(ThingVisibility.CanInspect(thing));
    }

    [Fact]
    public void EachFactionRetainsOnlyItsOwnObservedLocation()
    {
        var thing = ObservedThing();
        var comp = thing.TryGetComp<CompFog>().HideFromPlayer;
        var primary = Faction.OfPlayer;
        var second = new Faction { loadID = 2, IsPlayer = true };
        thing.Map.Fog.OtherPlayerFactions.Add(second);
        try
        {
            thing.PositionHeld = new IntVec3(1, 0);
            thing.Map.Fog.knownCells[1] = true;
            thing.Map.Fog.FactionSight[second] = new[] { false, true, false, false };
            thing.Map.Fog.FactionKnown[second] = new[] { false, true, false, false };
            comp.UpdateVisibility(true);
            Assert.False(comp.WasSeenAtCurrentLocation(primary));
            Assert.True(comp.WasSeenAtCurrentLocation(second));
            Faction.OfPlayer = second;
            thing.Map.Fog.FactionSight[second][1] = false;
            Assert.True(ThingVisibility.CanInspect(thing));
            Faction.OfPlayer = primary;
            Assert.False(ThingVisibility.CanInspect(thing));
        }
        finally
        {
            Faction.OfPlayer = primary;
        }
    }

    [Fact]
    public void CurrentSelectionSurvivesSightLossOnlyWithRememberedSelectionEnabled()
    {
        var thing = ObservedThing();
        Find.Selector.Selected.Add(thing);
        try
        {
            thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
            Assert.True(Find.Selector.IsSelected(thing));
            FogSettings.SelectRememberedObjects = false;
            thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
            Assert.False(Find.Selector.IsSelected(thing));
        }
        finally
        {
            Find.Selector.Selected.Remove(thing);
        }
    }

    [Fact]
    public void TurningOffRememberedSelectionReconcilesAnAlreadySelectedObjectWhilePaused()
    {
        var thing = ObservedThing();
        Find.Selector.Selected.Add(thing);
        try
        {
            InterfaceVisibility.ReconcileSelection();
            Assert.True(Find.Selector.IsSelected(thing));
            FogSettings.SelectRememberedObjects = false;
            InterfaceVisibility.ReconcileSelection();
            Assert.False(Find.Selector.IsSelected(thing));
        }
        finally
        {
            Find.Selector.Selected.Remove(thing);
        }
    }

    [Fact]
    public void ARegisteredInspectionCoreKeepsItsExplicitCurrentSightContract()
    {
        var thing = new RegisteredBench();
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.Fog.InSight[0] = true;
        var main = new CompFog();
        main.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = main };
        thing.Component = main;
        main.HideFromPlayer.PostSpawnSetup(false);
        thing.Map.Fog.InSight[0] = false;
        Visibility.RegisterInspectionCell(typeof(RegisteredBench), _ => new IntVec3(1, 0));
        try
        {
            Assert.False(ThingVisibility.CanInspect(thing));
            thing.Map.Fog.InSight[1] = true;
            Assert.True(ThingVisibility.CanInspect(thing));
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(RegisteredBench), null);
        }
    }

    [Fact]
    public void ClickAndRectangleCandidatesApplyTheSameMemoryPreference()
    {
        var thing = ObservedThing();
        var hiddenPawn = new Pawn { Map = thing.Map, Faction = new Faction() };
        IEnumerable<Thing> rectangle = new Thing[] { hiddenPawn, thing };
        InterfaceVisibility.MultiSelectCandidatesPostfix(ref rectangle);
        Assert.Equal(new[] { thing }, rectangle);
        IEnumerable<object> click = new object[] { hiddenPawn, thing };
        InterfaceVisibility.MouseSelectionCandidatesPostfix(ref click);
        Assert.Equal(new[] { thing }, click.Cast<Thing>());
        FogSettings.SelectRememberedObjects = false;
        InterfaceVisibility.MultiSelectCandidatesPostfix(ref rectangle);
        Assert.Empty(rectangle);
    }

    [Fact]
    public void ASeenFootprintDoesNotRequireTheAnchorCellToBeKnown()
    {
        var thing = ObservedThing(observe: false);
        thing.SizeX = 2;
        thing.Map.Fog.InSight[1] = true;
        thing.TryGetComp<CompFog>().HideFromPlayer.UpdateVisibility(true);
        thing.Map.Fog.InSight[1] = false;
        thing.Map.Fog.knownCells[0] = false;
        thing.Map.Fog.knownCells[1] = true;
        Assert.True(ThingVisibility.CanInspect(thing));
    }

    private static ThingWithComps ObservedThing(bool building = false, bool observe = true)
    {
        var thing = new ThingWithComps();
        thing.def.category = building ? ThingCategory.Building : ThingCategory.Item;
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.Fog.InSight[0] = observe;
        var main = new CompFog();
        main.Hiddenable = new CompPresentationState { parent = thing, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = main };
        thing.Component = main;
        main.HideFromPlayer.PostSpawnSetup(false);
        thing.Map.Fog.InSight[0] = false;
        main.HideFromPlayer.UpdateVisibility(true);
        return thing;
    }
}
