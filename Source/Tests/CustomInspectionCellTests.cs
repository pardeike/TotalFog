using System;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class CustomInspectionCellTests
{
    private class CorePawn : Pawn { }

    private sealed class DerivedPawn : CorePawn { }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void InspectionFollowsCoreWithoutMovingOrChangingRootRendering(
        bool rootVisible,
        bool coreVisible
    )
    {
        var pawn = new CorePawn();
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[0] = rootVisible;
        pawn.Map.Fog.InSight[1] = coreVisible;
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            Assert.Equal(coreVisible, Visibility.IsVisible(pawn));
            Assert.Equal(coreVisible && rootVisible, InterfaceVisibility.PawnLabelPrefix(pawn));
            Assert.Equal(
                coreVisible ? new IntVec3(1, 0) : IntVec3.Invalid,
                InterfaceVisibility.TooltipPosition(pawn)
            );
            Assert.Equal(
                rootVisible ? 1 : 0,
                InterfaceVisibility.FilterTargetThings(new() { pawn }, new IntVec3(0, 0)).Count
            );
            Assert.Equal(
                coreVisible ? 1 : 0,
                InterfaceVisibility.FilterTargetThings(new() { pawn }, new IntVec3(1, 0)).Count
            );
            Assert.Equal(rootVisible, ThingVisibility.IsVisible(pawn));
            Assert.Equal(new IntVec3(0, 0), pawn.PositionHeld);
            InterfaceVisibility.DrawOverlay(pawn);
            Assert.Equal(coreVisible && rootVisible ? 1 : 0, pawn.OverlayCalls);
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CorePreservesInitializationAndColonyBypass(bool initialized, bool bypass)
    {
        var pawn = new CorePawn();
        pawn.Map.Fog.Initialized = initialized;
        pawn.Map.IsPlayerHome = bypass;
        FogSettings.OnlyOutsideColony = bypass;
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            Assert.Equal(!initialized || bypass, Visibility.IsVisible(pawn));
        }
        finally
        {
            FogSettings.OnlyOutsideColony = false;
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void VanillaFogAndInvalidCoresPrecedeOwnershipAndBypass()
    {
        var pawn = new CorePawn { Faction = Faction.OfPlayer };
        pawn.Map.IsPlayerHome = true;
        pawn.Map.Fog.InSight[1] = true;
        FogSettings.OnlyOutsideColony = true;
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            pawn.Map.fogGrid.Fogged = true;
            Assert.False(Visibility.IsVisible(pawn));
            pawn.Map.fogGrid.Fogged = false;
            Visibility.RegisterInspectionCell(typeof(CorePawn), _ => IntVec3.Invalid);
            Assert.False(Visibility.IsVisible(pawn));
        }
        finally
        {
            FogSettings.OnlyOutsideColony = false;
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void RegistrationIsExactAndUnregisterRestoresTheOrdinaryFootprint()
    {
        var pawn = new CorePawn();
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[1] = true;
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            Assert.True(Visibility.IsVisible(pawn));
            Assert.False(Visibility.IsVisible(new DerivedPawn { Map = pawn.Map }));
            Assert.False(Visibility.IsVisible(new Pawn { Map = pawn.Map }));
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
            Assert.False(Visibility.IsVisible(pawn));
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void BrokenProviderFailsClosedWithoutRetryUntilReplaced()
    {
        var pawn = new CorePawn();
        pawn.Map.Fog.Initialized = pawn.Map.Fog.InSight[0] = true;
        int calls = 0;
        Visibility.RegisterInspectionCell(
            typeof(CorePawn),
            _ =>
            {
                calls++;
                throw new InvalidOperationException("fixture fault");
            }
        );
        try
        {
            Assert.False(Visibility.IsVisible(pawn));
            Assert.False(Visibility.IsVisible(pawn));
            Assert.Equal(1, calls);
            Visibility.RegisterInspectionCell(typeof(CorePawn), _ => pawn.PositionHeld);
            Assert.True(Visibility.IsVisible(pawn));
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void MaplessPolicyDoesNotInvokeTheProvider()
    {
        Visibility.RegisterInspectionCell(
            typeof(CorePawn),
            _ => throw new InvalidOperationException("must not query")
        );
        try
        {
            Assert.True(Visibility.IsVisible(new CorePawn { Map = null }));
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void VisibleProxyCannotDiscloseAHiddenCore()
    {
        var target = new CorePawn();
        target.Map.Fog.Initialized = target.Map.Fog.InSight[0] = true;
        var proxy = new Thing
        {
            Map = target.Map,
            Proxy = new RimWorld.CompSelectProxy { thingToSelect = target },
        };
        target.Map.Things.Add(proxy);
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            InterfaceVisibility.DrawOverlay(proxy);
            Assert.Equal(0, proxy.OverlayCalls);
            Assert.Empty(InterfaceVisibility.MouseoverThings(proxy.Position, proxy.Map));
            target.Map.Fog.InSight[1] = true;
            Assert.Same(
                target.Map.Things,
                InterfaceVisibility.MouseoverThings(proxy.Position, proxy.Map)
            );
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }

    [Fact]
    public void CoreSightLossClosesSelectionEvenWhileRootRemainsVisible()
    {
        var pawn = new CorePawn();
        pawn.Map.Fog.Initialized = pawn.Map.Fog.InSight[0] = pawn.Map.Fog.InSight[1] = true;
        var main = new CompFog();
        main.HideFromPlayer = new CompVisibility { parent = pawn, mainComponent = main };
        pawn.Component = main;
        Visibility.RegisterInspectionCell(typeof(CorePawn), _ => new IntVec3(1, 0));
        try
        {
            main.HideFromPlayer.PostSpawnSetup(false);
            Find.Selector.Selected.Add(pawn);
            pawn.Map.Fog.InSight[1] = false;
            main.HideFromPlayer.UpdateVisibility(true);
            Assert.False(Find.Selector.IsSelected(pawn));
            Assert.True(ThingVisibility.IsVisible(pawn));
        }
        finally
        {
            Find.Selector.Deselect(pawn);
            Visibility.RegisterInspectionCell(typeof(CorePawn), null);
        }
    }
}
