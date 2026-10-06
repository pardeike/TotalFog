using System;
using TotalFog.Compatibility;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class LegacySaveTypesTests
{
    [Fact]
    public void MissingOldSaveTypesImportOnlyCompatibleTotalFogTypes()
    {
        var previous = Scribe.mode;
        try
        {
            Scribe.mode = LoadSaveMode.LoadingVars;
            foreach (var (name, baseType, expected) in new[] {
                ("MapComponentSeenFog", typeof(MapComponent), typeof(MapVisibility)),
                ("PendingAlertManager", typeof(MapComponent), typeof(DeferredNotifications)),
                ("DeferredNotification", typeof(IExposable), typeof(DeferredNotification)),
                ("RfowSettings", typeof(ModSettings), typeof(FogSettings)),
                ("Building_CameraConsole", typeof(Building), typeof(Building_VisionConsole)),
                ("Building_SurveillanceCamera", typeof(Building), typeof(Building_VisionCamera)),
                ("MoteSoundWave", typeof(Thing), typeof(Mote_HearingCue)),
                ("JobDriver_SurveilCameraConsole", typeof(Verse.AI.JobDriver), typeof(JobDriver_MonitorVision)) })
            {
                Type result = null;
                LegacySaveTypes.ResolvePostfix(baseType, "RimWorldRealFoW." + name, ref result);
                Assert.Equal(expected, result);
                Assert.Equal("TotalFog", result.Namespace);
            }
        }
        finally { Scribe.mode = previous; }
    }

    [Fact]
    public void ImportDoesNotOverrideExistingTypesOrResolveUnrelatedNames()
    {
        var previous = Scribe.mode;
        try
        {
            Scribe.mode = LoadSaveMode.LoadingVars;
            Type resolved = typeof(MapComponent);
            LegacySaveTypes.ResolvePostfix(typeof(MapComponent), "RimWorldRealFoW.MapComponentSeenFog", ref resolved);
            Assert.Equal(typeof(MapComponent), resolved);
            foreach (var name in new[] { "RimWorldRealFoW.UnknownThing", "OtherMod.MapComponentSeenFog", "TotalFog.MapVisibility", null })
            {
                Type result = null;
                LegacySaveTypes.ResolvePostfix(typeof(MapComponent), name, ref result);
                Assert.Null(result);
            }
            Type wrongBase = null;
            LegacySaveTypes.ResolvePostfix(typeof(Thing), "RimWorldRealFoW.MapComponentSeenFog", ref wrongBase);
            Assert.Null(wrongBase);
        }
        finally { Scribe.mode = previous; }
    }

    [Theory]
    [InlineData(LoadSaveMode.Inactive)]
    [InlineData(LoadSaveMode.Saving)]
    [InlineData(LoadSaveMode.ResolvingCrossRefs)]
    [InlineData(LoadSaveMode.PostLoadInit)]
    public void ImportNeverExportsOldTypesOrRunsOutsideLoading(LoadSaveMode mode)
    {
        var previous = Scribe.mode;
        try
        {
            Scribe.mode = mode;
            Type result = null;
            LegacySaveTypes.ResolvePostfix(typeof(MapComponent), "RimWorldRealFoW.MapComponentSeenFog", ref result);
            Assert.Null(result);
        }
        finally { Scribe.mode = previous; }
    }
}
