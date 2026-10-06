using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class FactionVisibilityTests
{
    [Fact]
    public void ALocalViewerCannotSuppressAnotherFactionsObservationBeforeItsOwnGridExists()
    {
        var primary = Faction.OfPlayer;
        var viewer = new Faction { loadID = 2, IsPlayer = true };
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = true;
        item.Map.Fog.knownCells[0] = true;
        item.Map.Fog.InSight[0] = true;
        item.Map.Fog.FactionSight[viewer] = new bool[4];
        try
        {
            Faction.OfPlayer = viewer;
            var comp = AttachVisibility(item);
            Assert.True(comp.WasSeenBy(primary));
            Assert.False(comp.WasSeenBy(viewer));
        }
        finally
        {
            Faction.OfPlayer = primary;
        }
    }

    [Fact]
    public void DiscoveryAndRememberedThingsBelongToTheirObserverFaction()
    {
        var first = Faction.OfPlayer;
        var second = new Faction { loadID = 2, IsPlayer = true };
        var item = ObservedItem();
        var fog = item.Map.Fog;
        var comp = item.TryGetComp<CompFog>().HideFromPlayer;
        fog.FactionKnown[second] = new[] { true, false, false, false };
        fog.FactionSight[second] = new bool[4];
        fog.OtherPlayerFactions.Add(second);
        try
        {
            fog.InSight[0] = false;
            Faction.OfPlayer = second;
            Assert.False(comp.SeenByPlayer);
            Assert.False(ThingVisibility.IsVisible(item));
            Assert.False(Visibility.IsVisible(item));
            fog.FactionSight[second][0] = true;
            comp.RecordObservation(second, fog);
            Assert.True(comp.SeenByPlayer);
            fog.FactionSight[second][0] = false;
            Assert.True(ThingVisibility.IsVisible(item));
            Assert.False(Visibility.IsVisible(item));
            Faction.OfPlayer = first;
            Assert.True(comp.SeenByPlayer);
            Assert.True(ThingVisibility.IsVisible(item));
        }
        finally
        {
            Faction.OfPlayer = first;
        }
    }

    [Fact]
    public void MovingToAnotherFactionsMapDoesNotTransferObservationOwnership()
    {
        var first = Faction.OfPlayer;
        var item = ObservedItem();
        var comp = item.TryGetComp<CompFog>().HideFromPlayer;
        item.Map = new Map();
        item.Map.Fog.Initialized = true;
        item.Map.Fog.PrimaryPlayerFactionId = 2;
        item.Map.Fog.knownCells[0] = true;
        try
        {
            Faction.OfPlayer = new Faction { loadID = 2, IsPlayer = true };
            Assert.False(comp.SeenByPlayer);
            Assert.False(ThingVisibility.IsVisible(item));
            Assert.True(comp.WasSeenBy(first));
        }
        finally
        {
            Faction.OfPlayer = first;
        }
    }

    [Fact]
    public void ExplicitObservationDoesNotUseTheLocalViewingFactionOrPreview()
    {
        var first = Faction.OfPlayer;
        var second = new Faction { loadID = 2, IsPlayer = true };
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = true;
        item.Map.Fog.FactionKnown[second] = new bool[4];
        item.Map.Fog.FactionSight[second] = new[] { true, false, false, false };
        var comp = AttachVisibility(item);
        comp.RecordObservation(second, item.Map.Fog);
        Assert.True(comp.WasSeenBy(second));
        Assert.False(comp.WasSeenBy(first));
        Assert.False(ThingVisibility.IsVisible(item));
    }

    [Fact]
    public void HearingCueIsDrawnOnlyForTheFactionThatHeardIt()
    {
        var cue = new Mote_HearingCue { ObserverFactionId = Faction.OfPlayer.loadID };
        Assert.True(ThingVisibility.IsVisible(cue));
        Assert.False(
            ThingVisibility.IsVisible(
                cue,
                observerFaction: new Faction { loadID = 2, IsPlayer = true }
            )
        );
    }

    [Fact]
    public void LegacyHeldObservationBelongsToTheMapsOriginalFaction()
    {
        var item = new ThingWithComps { Spawned = false };
        var main = new CompFog();
        var comp = new CompVisibility { parent = item, mainComponent = main };
        typeof(CompVisibility)
            .GetField(
                "seenByPlayer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic
            )
            .SetValue(comp, true);
        Assert.True(comp.WasSeenBy(Faction.OfPlayer));
        Assert.False(comp.WasSeenBy(new Faction { loadID = 2, IsPlayer = true }));
    }

    [Fact]
    public void FogTransitionsPreserveTheEngineTooltipRegistry()
    {
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = true;
        item.def.hasTooltip = true;
        item.Map.tooltipGiverList.Notify_ThingSpawned(item);
        var comp = AttachVisibility(item);
        item.Map.Fog.InSight[0] = true;
        comp.UpdateVisibility(true);
        item.Map.Fog.InSight[0] = false;
        comp.UpdateVisibility(true);
        Assert.Equal(1, item.Map.tooltipGiverList.Added);
        Assert.Equal(0, item.Map.tooltipGiverList.Removed);
    }

    private static ThingWithComps ObservedItem()
    {
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = true;
        item.Map.Fog.knownCells[0] = true;
        item.Map.Fog.InSight[0] = true;
        AttachVisibility(item);
        return item;
    }

    private static CompVisibility AttachVisibility(ThingWithComps item)
    {
        var main = new CompFog();
        main.ComponentsPositionTracker = new CompCellRegistration
        {
            parent = item,
            mainComponent = main,
        };
        main.Hiddenable = new CompPresentationState { parent = item, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = item, mainComponent = main };
        item.Component = main;
        main.Hiddenable.PostSpawnSetup(false);
        main.HideFromPlayer.PostSpawnSetup(false);
        return main.HideFromPlayer;
    }
}
