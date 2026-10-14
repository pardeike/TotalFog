using System;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class RememberedInteractionTests : IDisposable
{
    private readonly bool previous = FogSettings.InteractWithRememberedObjects;

    public RememberedInteractionTests() => FogSettings.InteractWithRememberedObjects = true;

    public void Dispose() => FogSettings.InteractWithRememberedObjects = previous;

    [Theory]
    [InlineData(ThingCategory.Building)]
    [InlineData(ThingCategory.Plant)]
    [InlineData(ThingCategory.Item)]
    public void RememberedWorkCandidatesRemainWhileCombatCandidatesRequireSight(
        ThingCategory category
    )
    {
        var thing = Remembered(category);
        var context = new RimWorld.FloatMenuContext { ClickedCell = thing.Position };
        context.ClickedThings.Add(thing);
        InterfaceVisibility.ContextMenuPostfix(context);
        Assert.Equal(new[] { thing }, context.ClickedThings);
        Assert.True(ThingVisibility.IsActionable(thing, Faction.OfPlayer));
        Assert.False(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position));
        Assert.Empty(InterfaceVisibility.FilterTargetThings(new() { thing }, thing.Position));
        thing.Map.Fog.InSight[0] = true;
        Assert.True(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position));
    }

    [Fact]
    public void StrictPreferenceBlocksRememberedWorkUntilSightReturns()
    {
        var thing = Remembered(ThingCategory.Plant);
        FogSettings.InteractWithRememberedObjects = false;
        Assert.False(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position, true));
        thing.Map.Fog.InSight[0] = true;
        Assert.True(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position, true));
    }

    [Fact]
    public void UnknownTerrainAndNativeFogKeepWorkTargetsUnavailable()
    {
        var thing = Remembered(ThingCategory.Building);
        thing.Map.Fog.knownCells[0] = false;
        Assert.False(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position, true));
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.fogGrid.Fogged = true;
        Assert.False(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position, true));
    }

    [Fact]
    public void AReplacementOrMovedTargetCannotUseAnotherObjectsObservation()
    {
        var thing = Remembered(ThingCategory.Plant);
        var replacement = new ThingWithComps { Map = thing.Map };
        Assert.False(
            InterfaceVisibility.CanReadMenuTarget(replacement, replacement.Position, true)
        );
        thing.PositionHeld = new IntVec3(1, 0);
        thing.Map.Fog.knownCells[1] = true;
        Assert.False(InterfaceVisibility.CanReadMenuTarget(thing, thing.Position, true));
    }

    [Fact]
    public void RememberedContextDoesNotRestoreHiddenEnemyPawnCandidates()
    {
        var thing = Remembered(ThingCategory.Building);
        var pawn = new Pawn { Map = thing.Map, Faction = new Faction() };
        var context = new RimWorld.FloatMenuContext { ClickedCell = thing.Position };
        context.ClickedThings.AddRange(new Thing[] { pawn, thing });
        context.ClickedPawns.Add(pawn);
        InterfaceVisibility.ContextMenuPostfix(context);
        Assert.Equal(new[] { thing }, context.ClickedThings);
        Assert.Empty(context.ClickedPawns);
    }

    private static ThingWithComps Remembered(ThingCategory category)
    {
        var thing = new ThingWithComps();
        thing.def.category = category;
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        thing.Map.Fog.InSight[0] = true;
        var main = new CompFog();
        main.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = main };
        thing.Component = main;
        main.HideFromPlayer.PostSpawnSetup(false);
        thing.Map.Fog.InSight[0] = false;
        main.HideFromPlayer.UpdateVisibility(true);
        return thing;
    }
}
