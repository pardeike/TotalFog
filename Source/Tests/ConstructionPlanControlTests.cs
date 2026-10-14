using System.Collections.Generic;
using System.Linq;
using TotalFog;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class ConstructionPlanControlTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MouseCandidatesExcludeHiddenThingsAndKeepNativeNonThingTargets(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        var hidden = new Pawn { Map = plan.Map };
        var zone = new object();
        var source = new List<object> { hidden, zone, plan };
        IEnumerable<object> candidates = source;
        InterfaceVisibility.MouseSelectionCandidatesPostfix(ref candidates);
        Assert.Equal(new object[] { zone, plan }, candidates.ToArray());
        Assert.Equal(new object[] { hidden, zone, plan }, source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragCandidatesExcludeHiddenThingsBeforeNativeCategoryPriority(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        var hidden = new Thing { Map = plan.Map };
        var enemyPlan = Plan(frame, new Faction());
        IEnumerable<Thing> candidates = new[] { hidden, plan, enemyPlan };
        InterfaceVisibility.MultiSelectCandidatesPostfix(ref candidates);
        Assert.Equal(new[] { plan }, candidates.ToArray());
        Assert.False(plan.Map.Fog.InSight[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DragCandidatesPreserveVisibleOrderAndNativeSourceList(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        var visible = new Thing { Map = plan.Map, PositionHeld = new IntVec3(1, 0) };
        plan.Map.Fog.InSight[1] = true;
        var hidden = new Thing { Map = plan.Map };
        var source = new List<Thing> { visible, hidden, plan };
        IEnumerable<Thing> candidates = source;
        InterfaceVisibility.MultiSelectCandidatesPostfix(ref candidates);
        Assert.Equal(new Thing[] { visible, plan }, candidates.ToArray());
        Assert.Equal(new Thing[] { visible, hidden, plan }, source);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedConstructionRemainsControllableWithoutLiveSight(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        Assert.True(ThingVisibility.CanInspect(plan));
        Assert.True(ThingVisibility.IsVisible(plan));
        Assert.Equal(plan.Position, InterfaceVisibility.TooltipPosition(plan));
        InterfaceVisibility.DrawOverlay(plan);
        Assert.Equal(1, plan.OverlayCalls);

        // Inspecting the player's order grants neither event sight nor access
        // to a hidden thing underneath its footprint.
        Assert.False(Visibility.IsVisible(plan));
        var hidden = new Thing { Map = plan.Map, PositionHeld = plan.Position };
        Assert.False(ThingVisibility.CanInspect(hidden));
        Assert.False(plan.Map.Fog.InSight[0]);
        Assert.False(plan.Map.Fog.knownCells[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedOwnedConstructionSurvivesSightLoss(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        plan.Map.Fog.InSight[0] = true;
        var comp = plan.TryGetComp<CompFog>();
        comp.HideFromPlayer.UpdateVisibility(true);
        Find.Selector.Selected.Add(plan);
        try
        {
            plan.Map.Fog.InSight[0] = false;
            comp.HideFromPlayer.UpdateVisibility(true);
            Assert.True(Find.Selector.IsSelected(plan));
            Assert.True(ThingVisibility.CanInspect(plan));
            Assert.False(Visibility.IsVisible(plan));
        }
        finally
        {
            Find.Selector.Selected.Remove(plan);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForeignConstructionStillRequiresLiveSight(bool frame)
    {
        var plan = Plan(frame, new Faction());
        Assert.False(ThingVisibility.CanInspect(plan));
        plan.Map.Fog.InSight[0] = true;
        Assert.True(ThingVisibility.CanInspect(plan));
        plan.Map.Fog.InSight[0] = false;
        Assert.False(ThingVisibility.CanInspect(plan));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedConstructionStillRespectsNativeFog(bool frame)
    {
        var plan = Plan(frame, Faction.OfPlayer);
        plan.Map.Fog.InSight[0] = true;
        plan.Map.fogGrid.Fogged = true;
        Assert.False(ThingVisibility.CanInspect(plan));
        Assert.False(ThingVisibility.IsVisible(plan));
    }

    [Fact]
    public void CompletedBuildingDoesNotInheritTheConstructionException()
    {
        var building = Plan(true, Faction.OfPlayer);
        building.def.IsFrame = false;
        building.def.category = ThingCategory.Building;
        Assert.False(ThingVisibility.CanInspect(building));
    }

    private static ThingWithComps Plan(bool frame, Faction faction)
    {
        var plan = new ThingWithComps { Faction = faction };
        plan.def.IsBlueprint = !frame;
        plan.def.IsFrame = frame;
        plan.Map.Fog.Initialized = true;
        plan.Map.Fog.knownCells[0] = true;
        var comp = new CompFog();
        comp.Hiddenable = new CompPresentationState { parent = plan, mainComponent = comp };
        comp.HideFromPlayer = new CompVisibility { parent = plan, mainComponent = comp };
        plan.Component = comp;
        comp.Hiddenable.PostSpawnSetup(false);
        comp.HideFromPlayer.PostSpawnSetup(false);
        return plan;
    }
}
