using System;
using RimWorld.Planet;
using TotalFog.Compatibility;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class GravshipVisibilityTests : IDisposable
{
    private readonly World oldWorld = Find.World;
    private readonly bool oldOdyssey = ModsConfig.OdysseyActive,
        oldSetting = FogSettings.ClearFogDuringTargeting;
    private readonly bool oldCutscene = WorldComponent_GravshipController.CutsceneInProgress;
    private readonly ProgramState oldState = Current.ProgramState;

    public GravshipVisibilityTests()
    {
        ModsConfig.OdysseyActive = FogSettings.ClearFogDuringTargeting = true;
        WorldComponent_GravshipController.CutsceneInProgress = false;
        Current.ProgramState = ProgramState.Playing;
        Find.World = new World();
    }

    public void Dispose()
    {
        Find.World = oldWorld;
        ModsConfig.OdysseyActive = oldOdyssey;
        FogSettings.ClearFogDuringTargeting = oldSetting;
        WorldComponent_GravshipController.CutsceneInProgress = oldCutscene;
        Current.ProgramState = oldState;
    }

    [Fact]
    public void RepeatedChecksReadLandingStateLive()
    {
        var controller = AddController();
        for (int i = 0; i < 100; i++)
            Assert.False(GravshipVisibility.Revealed);
        controller.LandingAreaConfirmationInProgress = true;
        Assert.True(GravshipVisibility.Revealed);
        controller.LandingAreaConfirmationInProgress = false;
        Assert.False(GravshipVisibility.Revealed);
    }

    [Fact]
    public void WarmQueriesAllocateNoManagedMemory()
    {
        AddController();
        for (int i = 0; i < 100; i++)
            _ = GravshipVisibility.Revealed;
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool revealed = false;
        for (int i = 0; i < 1000; i++)
            revealed |= GravshipVisibility.Revealed;
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.False(revealed);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void EachWorldHasItsOwnController()
    {
        var first = Find.World;
        AddController().LandingAreaConfirmationInProgress = true;
        Assert.True(GravshipVisibility.Revealed);
        Find.World = new World();
        var second = Find.World;
        AddController();
        Assert.False(GravshipVisibility.Revealed);
        Find.World = first;
        Assert.True(GravshipVisibility.Revealed);
    }

    [Fact]
    public void ControllerAppearingAfterInitializationIsVisible()
    {
        Assert.False(GravshipVisibility.Revealed);
        AddController().LandingAreaConfirmationInProgress = true;
        Assert.True(GravshipVisibility.Revealed);
        Assert.Equal(2, Find.World.ComponentLookups);
    }

    [Fact]
    public void WorldInitializationDoesNotKeepATransientController()
    {
        AddController();
        Assert.False(GravshipVisibility.Revealed);
        Current.ProgramState = ProgramState.MapInitializing;
        Find.World.components.Clear();
        AddController().LandingAreaConfirmationInProgress = true;
        Assert.True(GravshipVisibility.Revealed);
        Find.World.components.Clear();
        AddController();
        Current.ProgramState = ProgramState.Playing;
        Assert.False(GravshipVisibility.Revealed);
        Assert.False(GravshipVisibility.Revealed);
    }

    [Fact]
    public void DisabledSettingDisabledDlcAndCutsceneAvoidTheLookup()
    {
        FogSettings.ClearFogDuringTargeting = false;
        Assert.False(GravshipVisibility.Revealed);
        FogSettings.ClearFogDuringTargeting = true;
        ModsConfig.OdysseyActive = false;
        Assert.False(GravshipVisibility.Revealed);
        ModsConfig.OdysseyActive = true;
        WorldComponent_GravshipController.CutsceneInProgress = true;
        Assert.True(GravshipVisibility.Revealed);
        Assert.Equal(0, Find.World.ComponentLookups);
    }

    private static WorldComponent_GravshipController AddController()
    {
        var controller = new WorldComponent_GravshipController(Find.World);
        Find.World.components.Add(controller);
        return controller;
    }
}
