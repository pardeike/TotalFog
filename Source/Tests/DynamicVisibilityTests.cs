using System.Collections.Generic;
using RimWorld;
using TotalFog;
using TotalFog.Presentation;
using Unity.Collections;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class DynamicVisibilityTests
{
    private static ThingWithComps Observed(ThingCategory category)
    {
        var thing = new ThingWithComps { def = new ThingDef { category = category } };
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = thing.Map.Fog.InSight[0] = true;
        var main = new CompFog();
        main.Hiddenable = new CompPresentationState { parent = thing, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = thing, mainComponent = main };
        thing.Component = main;
        thing.Map.dynamicDrawManager.RegisterDrawable(thing);
        main.Hiddenable.PostSpawnSetup(false);
        main.HideFromPlayer.PostSpawnSetup(false);
        main.HideFromPlayer.UpdateVisibility(true);
        return thing;
    }

    [Theory]
    [InlineData(ThingCategory.Item)]
    [InlineData(ThingCategory.Building)]
    public void ObservedStaticThingKeepsNativeDrawingWhenSightIsLost(ThingCategory category)
    {
        var thing = Observed(category);
        thing.Map.Fog.InSight[0] = false;
        Assert.True(ThingVisibility.IsVisible(thing));
        Assert.True(thing.TryGetComp<CompFog>().HideFromPlayer.SeenByPlayer);
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { thing });

        Assert.True(details[0].shouldDraw);
        Assert.True(details[0].shouldDrawShadow);
        Assert.Single(thing.Map.dynamicDrawManager.things);
    }

    [Fact]
    public void NewStaticThingInExploredTerrainCannotDrawUntilObserved()
    {
        var thing = Observed(ThingCategory.Item);
        var fresh = new ThingWithComps { Map = thing.Map };
        var main = new CompFog();
        main.Hiddenable = new CompPresentationState { parent = fresh, mainComponent = main };
        main.HideFromPlayer = new CompVisibility { parent = fresh, mainComponent = main };
        fresh.Component = main;
        fresh.Map.Fog.InSight[0] = false;
        main.Hiddenable.PostSpawnSetup(false);
        main.HideFromPlayer.PostSpawnSetup(false);
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { fresh });

        Assert.False(details[0].shouldDraw);
        Assert.False(details[0].shouldDrawShadow);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void VisibleThingPreservesTheEnginesCullDecision(bool draw, bool shadow)
    {
        var thing = Observed(ThingCategory.Building);
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    shouldDraw = draw,
                    shouldDrawShadow = shadow,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { thing });

        Assert.Equal(draw, details[0].shouldDraw);
        Assert.Equal(shadow, details[0].shouldDrawShadow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedPawnPreservesItsExistingObserverException(bool flying)
    {
        Thing thing = flying
            ? new PawnFlyer { FlyingPawn = new Pawn { Faction = Faction.OfPlayer } }
            : new Pawn { Faction = Faction.OfPlayer };
        thing.Map.Fog.Initialized = true;
        Assert.False(thing.Map.Fog.InSight[0]);
        Assert.True(ThingVisibility.IsVisible(thing));
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { thing });

        Assert.True(details[0].shouldDraw);
        Assert.True(details[0].shouldDrawShadow);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedPawnPresentationNeedsNoFogComponentLookup(bool flying)
    {
        Thing thing = flying
            ? new PawnFlyer { FlyingPawn = new Pawn { Faction = Faction.OfPlayer } }
            : new Pawn { Faction = Faction.OfPlayer };
        thing.Map.Fog.Initialized = true;

        Assert.True(ThingVisibility.IsVisible(thing));
        Assert.Equal(0, thing.Map.ComponentLookups);
        Assert.False(ThingVisibility.IsVisible(thing, allowMemory: false));
        Assert.Equal(1, thing.Map.ComponentLookups);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void WalkingPawnUsesTheNativeRenderedCellForAllDrawingPhases(
        bool logicalSight,
        bool renderedSight
    )
    {
        var pawn = new Pawn { PositionHeld = new IntVec3(1, 0) };
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[1] = logicalSight;
        pawn.Map.Fog.InSight[0] = renderedSight;
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    cell = new IntVec3(0, 0),
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });

        Assert.Equal(renderedSight, details[0].shouldDraw);
        Assert.Equal(renderedSight, details[0].shouldDrawShadow);
        Assert.Equal(logicalSight, ThingVisibility.IsVisible(pawn));
    }

    [Fact]
    public void OversizedPawnKeepsItsWholeFootprintAtTheRenderedPosition()
    {
        var pawn = new Pawn
        {
            PositionHeld = new IntVec3(1, 1),
            SizeX = 2,
            SizeZ = 2,
        };
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[2] = true;
        Assert.False(ThingVisibility.IsVisible(pawn));
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    cell = new IntVec3(0, 0),
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });

        Assert.True(details[0].shouldDraw);
        Assert.True(details[0].shouldDrawShadow);
    }

    [Fact]
    public void WalkingPawnOutsideTheMapCannotBorrowItsLogicalCellsSight()
    {
        var pawn = new Pawn();
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[0] = true;
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    cell = new IntVec3(-1, 0),
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });

        Assert.False(details[0].shouldDraw);
        Assert.False(details[0].shouldDrawShadow);
    }

    [Fact]
    public void VisibilityQueryNeverCallsTheFlyersMutatingDrawPositionGetter()
    {
        var flyer = new PawnFlyer { PositionHeld = new IntVec3(1, 0), ThrowOnDrawPos = true };
        flyer.Map.Fog.Initialized = true;
        flyer.Map.Fog.InSight[1] = true;

        Assert.True(ThingVisibility.IsVisible(flyer));
        Assert.False(ThingVisibility.IsVisible(flyer, renderedCell: new IntVec3(0, 0)));
        Assert.Equal(0, flyer.DrawPosReads);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void FlyerDrawingUsesItsActualRenderedCellAtTheSafeRenderBoundary(
        bool logicalSight,
        bool renderedSight
    )
    {
        var flyer = new PawnFlyer
        {
            PositionHeld = new IntVec3(1, 0),
            RenderedPosition = new DrawPosition { Cell = new IntVec3(0, 0) },
        };
        flyer.Map.Fog.Initialized = true;
        flyer.Map.Fog.InSight[1] = logicalSight;
        flyer.Map.Fog.InSight[0] = renderedSight;
        var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
            new[]
            {
                new DynamicDrawManager.ThingCullDetails
                {
                    cell = flyer.Position,
                    shouldDraw = true,
                    shouldDrawShadow = true,
                },
            }
        );

        DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { flyer });

        Assert.Equal(renderedSight, details[0].shouldDraw);
        Assert.Equal(renderedSight, details[0].shouldDrawShadow);
        Assert.Equal(logicalSight, ThingVisibility.IsVisible(flyer));
        Assert.Equal(1, flyer.DrawPosReads);
    }

    [Theory]
    [InlineData(DrawerType.None, 0)]
    [InlineData(DrawerType.RealtimeOnly, 0)]
    [InlineData(DrawerType.MapMeshOnly, 2)]
    [InlineData(DrawerType.MapMeshAndRealTime, 2)]
    public void RealtimeVisibilityDoesNotRequestSectionMeshRebuilds(
        DrawerType drawer,
        int dirtyCalls
    )
    {
        var thing = new ThingWithComps { def = new ThingDef { drawerType = drawer } };
        thing.Map.Fog.Initialized = true;
        var comp = new CompPresentationState { parent = thing };

        comp.Hide();
        comp.Hide();
        comp.Show();
        comp.Show();

        Assert.Equal(dirtyCalls, thing.Map.mapDrawer.DirtyCalls);
    }
}
