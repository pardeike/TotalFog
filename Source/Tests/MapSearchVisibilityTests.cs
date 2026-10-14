using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class MapSearchVisibilityTests
{
    private sealed class InspectionPawn : Pawn { }

    private static ThingWithComps Item(bool seen)
    {
        var item = new ThingWithComps();
        item.Map.Fog.Initialized = true;
        item.Map.Fog.knownCells[0] = true;
        item.Map.Fog.InSight[0] = seen;
        var main = new CompFog();
        main.Hiddenable = new CompPresentationState { parent = item, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = item, mainComponent = main };
        item.Component = main;
        main.HideFromPlayer.PostSpawnSetup(false);
        item.Map.Fog.InSight[0] = false;
        return item;
    }

    [Fact]
    public void ExplorationAloneDoesNotMakeANewItemSearchable()
    {
        var item = Item(false);
        bool result = true;
        MapSearchVisibility.VisibilityPostfix(item, ref result);
        Assert.False(result);
        Assert.False(MapSearchVisibility.LocationPrefix(item));
    }

    [Fact]
    public void SeenStaticItemsRemainSearchableUnderTheExistingDrawingPolicy()
    {
        var item = Item(true);
        Assert.True(MapSearchVisibility.AllowsResult(item));
        Assert.True(MapSearchVisibility.LocationPrefix(item));
        Assert.True(ThingVisibility.CanInspect(item));
        item.PositionHeld = new IntVec3(1, 0);
        item.Map.Fog.knownCells[1] = true;
        Assert.False(ThingVisibility.CanInspect(item));
        Assert.True(MapSearchVisibility.AllowsResult(item));
        item.Map.Fog.knownCells[1] = false;
        Assert.False(MapSearchVisibility.AllowsResult(item));
    }

    [Fact]
    public void HiddenEnemyHoldersCannotDisclosePreviouslyObservedContents()
    {
        var item = Item(true);
        item.HeldBy = new Pawn { Map = item.Map, Faction = new Faction() };
        Assert.False(MapSearchVisibility.AllowsResult(item));
        item.HeldBy.Faction = Faction.OfPlayer;
        Assert.True(MapSearchVisibility.AllowsResult(item));
    }

    [Fact]
    public void NativeFogAndNativeRejectionsKeepPrecedence()
    {
        var item = Item(true);
        bool result = false;
        MapSearchVisibility.VisibilityPostfix(item, ref result);
        Assert.False(result);
        item.Map.fogGrid.Fogged = true;
        Assert.False(MapSearchVisibility.AllowsResult(item));
    }

    [Fact]
    public void HiddenPawnsDoNotUseStaticItemMemory()
    {
        var item = Item(true);
        var enemy = new Pawn { Map = item.Map, Faction = new Faction() };
        Assert.False(MapSearchVisibility.AllowsResult(enemy));
        Assert.False(MapSearchVisibility.LocationPrefix(enemy));
        enemy.Faction = Faction.OfPlayer;
        Assert.True(MapSearchVisibility.AllowsResult(enemy));
    }

    [Fact]
    public void NativeDebugSearchCanDeliberatelyIgnoreRestrictions()
    {
        try
        {
            DebugSettings.searchIgnoresRestrictions = true;
            Assert.True(MapSearchVisibility.AllowsResult(Item(false)));
            Assert.False(MapSearchVisibility.AllowsResult(null));
        }
        finally
        {
            DebugSettings.searchIgnoresRestrictions = false;
        }
    }

    [Fact]
    public void AVisibleInspectionCoreMakesItsHiddenRootSearchable()
    {
        var pawn = new InspectionPawn { Faction = new Faction() };
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[1] = true;
        Visibility.RegisterInspectionCell(typeof(InspectionPawn), _ => new IntVec3(1, 0));
        try
        {
            Assert.False(ThingVisibility.IsVisible(pawn));
            Assert.True(MapSearchVisibility.AllowsResult(pawn));
        }
        finally
        {
            Visibility.RegisterInspectionCell(typeof(InspectionPawn), null);
        }
    }
}
