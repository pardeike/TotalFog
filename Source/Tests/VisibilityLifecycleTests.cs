using TotalFog;
using Verse;
using Xunit;

public class VisibilityLifecycleTests
{
    private sealed class RareSource : FogSubcomponent
    {
        public int Tick,
            NextCheck,
            Refreshes;

        public override void CompTick()
        {
            if (Tick < NextCheck)
                return;
            Refreshes++;
            NextCheck = Tick + 30;
        }
    }

    [Fact]
    public void Static_thing_transition_dirties_only_thing_and_building_meshes()
    {
        var parent = new ThingWithComps();
        parent.def.drawerType = DrawerType.MapMeshOnly;
        parent.Map.Fog.Initialized = true;
        var visibility = new CompPresentationState { parent = parent };
        visibility.PostSpawnSetup(false);
        visibility.Hide();
        visibility.Show();
        Assert.Equal(2, parent.Map.mapDrawer.DirtyCalls);
        Assert.Equal(
            RimWorld.MapMeshFlagDefOf.Things
                | RimWorld.MapMeshFlagDefOf.Buildings
                | RimWorld.MapMeshFlagDefOf.BuildingsDamage,
            parent.Map.mapDrawer.DirtyFlags
        );
    }

    [Fact]
    public void RareTickUpdatesSightEvenWhenItSkipsTheNormalDeadline()
    {
        var source = new RareSource { Tick = 250, NextCheck = 30 };
        source.CompTickRare();
        Assert.Equal(1, source.Refreshes);
        source.Tick = 500;
        source.CompTickRare();
        Assert.Equal(2, source.Refreshes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HiddenPawnRespawnedIntoViewHasOneRegistration(bool loadingSave)
    {
        var parent = new ThingWithComps();
        var visibility = new CompPresentationState { parent = parent };
        parent.Map.dynamicDrawManager.RegisterDrawable(parent);
        visibility.PostSpawnSetup(false);
        visibility.Hide();
        Assert.Single(parent.Map.dynamicDrawManager.things);

        // The engine unregisters on despawn, then registers before component setup.
        parent.Map.dynamicDrawManager.DeRegisterDrawable(parent);
        parent.Map.dynamicDrawManager.RegisterDrawable(parent);
        visibility.PostSpawnSetup(loadingSave);
        visibility.Show();

        Assert.False(visibility.Hidden);
        Assert.Single(parent.Map.dynamicDrawManager.things);
    }

    [Fact]
    public void HiddenPawnRespawnPreservesEngineRegistrationOwnership()
    {
        var parent = new ThingWithComps();
        var visibility = new CompPresentationState { parent = parent };
        parent.Map.dynamicDrawManager.RegisterDrawable(parent);
        visibility.PostSpawnSetup(false);
        visibility.Hide();
        parent.Map.dynamicDrawManager.DeRegisterDrawable(parent);
        parent.Map.dynamicDrawManager.RegisterDrawable(parent);
        visibility.PostSpawnSetup(false);
        visibility.Hide();

        Assert.True(visibility.Hidden);
        Assert.Single(parent.Map.dynamicDrawManager.things);
    }

    [Fact]
    public void RepeatedVisibilityUpdatesDoNotDuplicateRegistration()
    {
        var parent = new ThingWithComps();
        var visibility = new CompPresentationState { parent = parent };
        parent.Map.dynamicDrawManager.RegisterDrawable(parent);
        visibility.PostSpawnSetup(false);
        visibility.Hide();
        visibility.Hide();
        visibility.Show();
        visibility.Show();
        Assert.Single(parent.Map.dynamicDrawManager.things);
    }
}
