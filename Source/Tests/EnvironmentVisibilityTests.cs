using TotalFog;
using Verse;
using Xunit;
using BeautyUtility = TotalFog.Detours.BeautyUtility;
using EnvironmentStatsDrawer = TotalFog.Detours.EnvironmentStatsDrawer;

namespace TotalFog.Tests;

public sealed class EnvironmentVisibilityTests
{
    [Theory]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, false, true, true, true)]
    [InlineData(false, false, false, true, true)]
    [InlineData(true, true, false, false, false)]
    public void EnvironmentNeedsCurrentSightAndPreservesBaseRejections(
        bool initialized,
        bool inSight,
        bool bypass,
        bool original,
        bool expected
    )
    {
        var map = new Map { IsPlayerHome = bypass };
        map.Fog.Initialized = initialized;
        map.Fog.knownCells[0] = true;
        map.Fog.InSight[0] = inSight;
        Find.CurrentMap = map;
        UI.Cell = new IntVec3(0, 0);
        FogSettings.OnlyOutsideColony = bypass;
        try
        {
            bool result = original;
            EnvironmentStatsDrawer.ShouldShowWindowNow_Postfix(ref result);
            Assert.Equal(expected, result);
        }
        finally
        {
            FogSettings.OnlyOutsideColony = false;
            Find.CurrentMap = null;
        }
    }

    [Fact]
    public void EnvironmentDoesNotReadOutsideTheMap()
    {
        Find.CurrentMap = new Map();
        Find.CurrentMap.Fog.Initialized = true;
        UI.Cell = new IntVec3(-1, 0);
        try
        {
            bool result = true;
            EnvironmentStatsDrawer.ShouldShowWindowNow_Postfix(ref result);
            Assert.False(result);
        }
        finally
        {
            Find.CurrentMap = null;
        }
    }

    [Fact]
    public void BeautyOnlyReadsCurrentlyObservedCells()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.knownCells[0] = map.Fog.knownCells[1] = true;
        map.Fog.InSight[1] = true;
        var unseen = new IntVec3(0, 0);
        var seen = new IntVec3(1, 0);
        var cells = RimWorld.BeautyUtility.beautyRelevantCells;
        cells.AddRange(new[] { unseen, seen });
        try
        {
            BeautyUtility.FillBeautyRelevantCells_Postfix(map);
            Assert.Equal(new[] { seen }, cells);
        }
        finally
        {
            cells.Clear();
        }
    }

    [Fact]
    public void VanillaFogStillBlocksEnvironmentInformation()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        map.Fog.InSight[0] = true;
        map.fogGrid.Fogged = true;
        Find.CurrentMap = map;
        UI.Cell = new IntVec3(0, 0);
        try
        {
            bool result = true;
            EnvironmentStatsDrawer.ShouldShowWindowNow_Postfix(ref result);
            Assert.False(result);
        }
        finally
        {
            Find.CurrentMap = null;
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void BeautyPreservesInitializationFallbackAndColonyBypass(bool initialized, bool bypass)
    {
        var map = new Map { IsPlayerHome = bypass };
        map.Fog.Initialized = initialized;
        FogSettings.OnlyOutsideColony = bypass;
        var cells = RimWorld.BeautyUtility.beautyRelevantCells;
        cells.Add(new IntVec3(0, 0));
        try
        {
            BeautyUtility.FillBeautyRelevantCells_Postfix(map);
            Assert.Single(cells);
        }
        finally
        {
            cells.Clear();
            FogSettings.OnlyOutsideColony = false;
        }
    }
}
