using System.Linq;
using TotalFog;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class CellRegistrationTests
{
    private static (ThingWithComps thing, CompCellRegistration tracker) Spawn(
        int width = 1,
        int height = 1
    )
    {
        Find.TickManager.TicksGame = 0;
        var thing = new ThingWithComps { SizeX = width, SizeZ = height };
        var main = new CompFog();
        main.Hiddenable = new() { parent = thing, mainComponent = main };
        main.HideFromPlayer = new() { parent = thing, mainComponent = main };
        thing.Component = main;
        main.Hiddenable.PostSpawnSetup(false);
        main.HideFromPlayer.PostSpawnSetup(false);
        var tracker = new CompCellRegistration { parent = thing, mainComponent = main };
        main.ComponentsPositionTracker = tracker;
        tracker.PostSpawnSetup(false);
        return (thing, tracker);
    }

    [Fact]
    public void TurningWithinOneCellDoesNotRewriteRegistration()
    {
        var (thing, tracker) = Spawn();
        for (int tick = 1; tick <= 100; tick++)
        {
            Find.TickManager.TicksGame = tick;
            thing.Rotation = new(tick % 4);
            tracker.CompTick();
        }
        Assert.Equal(1, thing.Map.Fog.RegistrationWrites);
        Assert.Equal(0, thing.Map.Fog.DeregistrationWrites);
        Assert.Single(thing.Map.Fog.Registered);
    }

    [Fact]
    public void MovingUpdatesTheRegisteredCellBeforeThePeriodicDeadline()
    {
        var (thing, tracker) = Spawn();
        tracker.CompTick();
        Find.TickManager.TicksGame = 1;
        thing.PositionHeld = new(1, 0);
        tracker.CompTick();
        Assert.Equal(new[] { (1, 0) }, thing.Map.Fog.Registered.Keys);
        Assert.Equal(2, thing.Map.Fog.RegistrationWrites);
        Assert.Equal(1, thing.Map.Fog.DeregistrationWrites);
    }

    [Fact]
    public void RotatingALargerThingReplacesItsWholeFootprint()
    {
        var (thing, tracker) = Spawn(2, 1);
        tracker.CompTick();
        thing.Rotation = new(1);
        Find.TickManager.TicksGame = 1;
        tracker.CompTick();
        Assert.Equal(new[] { (0, 0), (0, 1) }, thing.Map.Fog.Registered.Keys.OrderBy(c => c));
        Assert.Equal(4, thing.Map.Fog.RegistrationWrites);
        Assert.Equal(2, thing.Map.Fog.DeregistrationWrites);
    }

    [Fact]
    public void ChangingDefinitionSizeRefreshesTheFootprintWithoutMovement()
    {
        var (thing, tracker) = Spawn();
        tracker.CompTick();
        thing.SizeX = 2;
        Find.TickManager.TicksGame = 1;
        tracker.CompTick();
        Assert.Equal(new[] { (0, 0), (1, 0) }, thing.Map.Fog.Registered.Keys.OrderBy(c => c));
    }

    [Fact]
    public void ChangingMapsClearsTheOldRegistrationAtAnUnchangedPosition()
    {
        var (thing, tracker) = Spawn();
        var old = thing.Map;
        tracker.CompTick();
        thing.Map = new();
        Find.TickManager.TicksGame = 12;
        tracker.CompTick();
        Assert.Empty(old.Fog.Registered);
        Assert.Single(thing.Map.Fog.Registered);
    }

    [Fact]
    public void VisibilityCannotUseOldMapCoverageBeforeRegistrationCatchesUp()
    {
        var (thing, tracker) = Spawn();
        var comp = thing.TryGetComp<CompFog>();
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.InSight[0] = true;
        thing.Map.Fog.knownCells[0] = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.False(comp.Hiddenable.Hidden);
        thing.Map = new();
        thing.Map.Fog.Initialized = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.True(comp.Hiddenable.Hidden);
        tracker.CompTick();
        thing.Map.Fog.InSight[0] = true;
        comp.HideFromPlayer.UpdateVisibility(true);
        Assert.False(comp.Hiddenable.Hidden);
    }

    [Fact]
    public void DespawningRepeatedlyDoesNotLeaveOrDoubleRemoveARegistration()
    {
        var (thing, tracker) = Spawn();
        tracker.PostDeSpawn(thing.Map);
        tracker.PostDeSpawn(thing.Map);
        Assert.Empty(thing.Map.Fog.Registered);
        Assert.Equal(1, thing.Map.Fog.DeregistrationWrites);
    }
}
