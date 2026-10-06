using RimWorld;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class VisibilityIntegrationTests
{
    [Fact]
    public void EnemyTargetCellsUseTheirOwnFactionSightWithoutChangingIt()
    {
        bool old = FogSettings.AISmart;
        try
        {
            FogSettings.AISmart = true;
            var enemy = new Pawn { Faction = new Faction() };
            enemy.Map.Fog.InSight[0] = true;
            enemy.Map.Fog.FactionSight[enemy.Faction] = new[] { false, true, false, false };
            Assert.False(Visibility.AllowsTarget(enemy, new IntVec3(0, 0)));
            Assert.True(Visibility.AllowsTarget(enemy, new IntVec3(1, 0)));
            enemy.Map.Fog.FactionSight[enemy.Faction][1] = false;
            Assert.False(Visibility.AllowsTarget(enemy, new IntVec3(1, 0)));
            Assert.True(enemy.Map.Fog.InSight[0]);
            Assert.False(enemy.Map.Fog.knownCells[0]);
            Assert.Equal(0, enemy.ComponentQueries);
        }
        finally
        {
            FogSettings.AISmart = old;
        }
    }

    [Fact]
    public void TargetFilterPreservesObserversOutsideExistingEnemyFogPolicy()
    {
        bool old = FogSettings.AISmart;
        try
        {
            var enemy = new Pawn { Faction = new Faction() };
            FogSettings.AISmart = false;
            Assert.True(Visibility.AllowsTarget(enemy, new IntVec3(0, 0)));
            FogSettings.AISmart = true;
            Assert.False(Visibility.AllowsTarget(enemy, new IntVec3(0, 0)));
            Assert.True(
                Visibility.AllowsTarget(new Pawn { Faction = Faction.OfPlayer }, new IntVec3(0, 0))
            );
            Assert.True(Visibility.AllowsTarget(new Pawn(), new IntVec3(0, 0)));
            enemy.RaceProps.Humanlike = false;
            Assert.True(Visibility.AllowsTarget(enemy, new IntVec3(0, 0)));
            enemy.RaceProps.Humanlike = true;
            enemy.Map = null;
            Assert.True(Visibility.AllowsTarget(enemy, new IntVec3(0, 0)));
            Assert.True(Visibility.AllowsTarget(null, new IntVec3(0, 0)));
        }
        finally
        {
            FogSettings.AISmart = old;
        }
    }

    [Fact]
    public void LiveEffectsRequireSightEvenForOwnedPawns()
    {
        var pawn = new Pawn { Faction = Faction.OfPlayer };
        pawn.Map.Fog.Initialized = true;
        Assert.True(ThingVisibility.IsVisible(pawn));
        Assert.False(Visibility.IsVisible(pawn));
        pawn.Map.Fog.InSight[0] = true;
        Assert.True(Visibility.IsVisible(pawn));
        pawn.Map.Fog.InSight[0] = false;
        Assert.False(Visibility.IsVisible(pawn));
    }

    [Fact]
    public void RememberedObjectsCannotDiscloseLiveEffects()
    {
        var thing = new ThingWithComps();
        var comp = new CompFog();
        comp.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = comp };
        comp.Hiddenable = new CompPresentationState { parent = thing, mainComponent = comp };
        thing.Component = comp;
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        comp.Hiddenable.PostSpawnSetup(false);
        comp.HideFromPlayer.PostSpawnSetup(false);
        thing.Map.Fog.InSight[0] = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        thing.Map.Fog.InSight[0] = false;
        Assert.True(ThingVisibility.IsVisible(thing));
        Assert.False(Visibility.IsVisible(thing));
        Assert.True(comp.HideFromPlayer.SeenByPlayer);
    }

    [Fact]
    public void VanillaFogStillPrecedesInitializationAndColonyBypass()
    {
        var thing = new Thing();
        thing.Map.IsPlayerHome = true;
        bool old = FogSettings.OnlyOutsideColony;
        try
        {
            FogSettings.OnlyOutsideColony = true;
            thing.Map.fogGrid.Fogged = true;
            Assert.False(Visibility.IsVisible(thing));
            thing.Map.fogGrid.Fogged = false;
            Assert.True(Visibility.IsVisible(thing));
            thing.Map.Fog.Initialized = true;
            FogSettings.OnlyOutsideColony = false;
            Assert.False(Visibility.IsVisible(thing));
        }
        finally
        {
            FogSettings.OnlyOutsideColony = old;
        }
    }

    [Fact]
    public void IntegrationQueryDoesNotInspectComponentsOrMutateSight()
    {
        var pawn = new Pawn();
        pawn.Map.Fog.Initialized = true;
        Assert.False(Visibility.IsVisible(pawn));
        Assert.Equal(0, pawn.ComponentQueries);
        Assert.False(pawn.Map.Fog.InSight[0]);
        Assert.False(pawn.Map.Fog.knownCells[0]);
        Assert.Empty(pawn.Map.dynamicDrawManager.things);
        Assert.True(Visibility.IsVisible(null));
        pawn.Map = null;
        Assert.True(Visibility.IsVisible(pawn));
    }
}
