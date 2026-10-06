using System;
using System.Collections.Generic;
using TotalFog.Presentation;
using Unity.Collections;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class CustomRenderVisibilityTests
{
    private sealed class BodyPawn : Pawn { }

    private sealed class OtherPawn : Pawn { }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void BodyGateCanDisagreeWithRootWithoutChangingLiveInformation(
        bool rootVisible,
        bool bodyVisible
    )
    {
        var pawn = new BodyPawn();
        pawn.Map.Fog.Initialized = true;
        pawn.Map.Fog.InSight[0] = rootVisible;
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
        Visibility.RegisterRenderer(typeof(BodyPawn), _ => bodyVisible);
        try
        {
            DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });
            Assert.Equal(bodyVisible, details[0].shouldDraw);
            Assert.Equal(bodyVisible, details[0].shouldDrawShadow);
            Assert.Equal(rootVisible, Visibility.IsVisible(pawn));
            Assert.Equal(rootVisible, InterfaceVisibility.PawnLabelPrefix(pawn));
        }
        finally
        {
            Visibility.RegisterRenderer(typeof(BodyPawn), null);
        }
    }

    [Fact]
    public void NativeCullRejectionDoesNotInvokeTheCustomGate()
    {
        var pawn = new BodyPawn();
        int calls = 0;
        Visibility.RegisterRenderer(
            typeof(BodyPawn),
            _ =>
            {
                calls++;
                return true;
            }
        );
        try
        {
            var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
                new[] { new DynamicDrawManager.ThingCullDetails() }
            );
            DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });
            Assert.False(details[0].shouldDraw);
            Assert.False(details[0].shouldDrawShadow);
            Assert.Equal(0, calls);
        }
        finally
        {
            Visibility.RegisterRenderer(typeof(BodyPawn), null);
        }
    }

    [Fact]
    public void UnrelatedTypesKeepTheCommonVisibilityRule()
    {
        var pawn = new OtherPawn();
        pawn.Map.Fog.Initialized = true;
        Visibility.RegisterRenderer(typeof(BodyPawn), _ => true);
        try
        {
            var details = new NativeArray<DynamicDrawManager.ThingCullDetails>(
                new[] { new DynamicDrawManager.ThingCullDetails { shouldDraw = true } }
            );
            DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });
            Assert.False(details[0].shouldDraw);
        }
        finally
        {
            Visibility.RegisterRenderer(typeof(BodyPawn), null);
        }
    }

    [Fact]
    public void FaultingProviderFailsClosedUntilExplicitlyReplaced()
    {
        var pawn = new BodyPawn();
        pawn.Map.Fog.Initialized = pawn.Map.Fog.InSight[0] = true;
        int calls = 0;
        Visibility.RegisterRenderer(
            typeof(BodyPawn),
            _ =>
            {
                calls++;
                throw new InvalidOperationException("fixture fault");
            }
        );
        try
        {
            for (int frame = 0; frame < 2; frame++)
            {
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
                DynamicVisibility.ComputeCulledThings_Postfix(details, new List<Thing> { pawn });
                Assert.False(details[0].shouldDraw);
                Assert.False(details[0].shouldDrawShadow);
            }
            Assert.Equal(1, calls);
            Visibility.RegisterRenderer(typeof(BodyPawn), _ => true);
            Assert.True(CustomRenderVisibility.TryQuery(pawn, out bool visible));
            Assert.True(visible);
        }
        finally
        {
            Visibility.RegisterRenderer(typeof(BodyPawn), null);
        }
    }

    [Fact]
    public void CellQueryRejectsVanillaFogAndOutsideCellsBeforeBypasses()
    {
        var map = new Map { IsPlayerHome = true };
        map.Fog.Initialized = true;
        map.Fog.knownCells[0] = true;
        Assert.False(Visibility.IsVisible(map, new IntVec3(0, 0)));
        map.Fog.InSight[0] = true;
        Assert.True(Visibility.IsVisible(map, new IntVec3(0, 0)));
        FogSettings.OnlyOutsideColony = true;
        try
        {
            map.fogGrid.Fogged = true;
            Assert.False(Visibility.IsVisible(map, new IntVec3(0, 0)));
            Assert.False(Visibility.IsVisible(map, new IntVec3(-1, 0)));
            Assert.False(Visibility.IsVisible(null, new IntVec3(0, 0)));
        }
        finally
        {
            FogSettings.OnlyOutsideColony = false;
        }
    }
}
