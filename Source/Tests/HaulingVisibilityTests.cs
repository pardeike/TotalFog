using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class HaulingVisibilityTests
{
    private static Thing Chunk(bool known = true, bool ordered = true)
    {
        var chunk = new Thing { def = new ThingDef { designateHaulable = true } };
        chunk.Map.Fog.Initialized = true;
        chunk.Map.Fog.knownCells[0] = known;
        if (ordered)
            chunk.Map.designationManager.HaulOrders.Add(chunk);
        return chunk;
    }

    [Fact]
    public void OrderedKnownPlainChunkRetainsEligibilityAfterSightIsLost()
    {
        var chunk = Chunk();

        Assert.True(ThingVisibility.IsVisible(chunk));
        Assert.False(Visibility.IsVisible(chunk));
        Assert.Equal(1, chunk.Map.designationManager.Queries);

        chunk.Map.designationManager.HaulOrders.Remove(chunk);
        Assert.False(ThingVisibility.IsVisible(chunk));
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void UnknownOrUnorderedPlainChunkStaysHidden(bool known, bool ordered)
    {
        Assert.False(ThingVisibility.IsVisible(Chunk(known, ordered)));
    }

    [Fact]
    public void AHaulOrderCannotInventObservationForComponentBearingThings()
    {
        var chunk = Chunk();
        var item = new ThingWithComps { Map = chunk.Map, def = chunk.def };
        item.Map.designationManager.HaulOrders.Add(item);

        Assert.False(ThingVisibility.IsVisible(item));
        Assert.Equal(0, item.Map.designationManager.Queries);
    }

    [Fact]
    public void AHaulOrderDoesNotShareAnotherFactionsKnownTerrain()
    {
        var chunk = Chunk();
        var other = new Faction { IsPlayer = true, loadID = 2 };
        chunk.Map.Fog.FactionKnown[other] = new bool[4];

        Assert.False(ThingVisibility.IsVisible(chunk, observerFaction: other));
        chunk.Map.Fog.FactionKnown[other][0] = true;
        Assert.True(ThingVisibility.IsVisible(chunk, observerFaction: other));
    }

    [Fact]
    public void AHaulOrderDoesNotBypassVanillaFog()
    {
        var chunk = Chunk();
        chunk.Map.fogGrid.Fogged = true;

        Assert.False(ThingVisibility.IsVisible(chunk));
        Assert.Equal(0, chunk.Map.designationManager.Queries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OwnedConstructionCanClearAHiddenKnownObstructionWithoutRevealingIt(bool frame)
    {
        var chunk = Chunk(ordered: false);
        chunk.def.EverHaulable = true;
        var plan = new Thing
        {
            Map = chunk.Map,
            Faction = Faction.OfPlayer,
            def = new ThingDef
            {
                IsBlueprint = !frame,
                IsFrame = frame,
                BlocksItems = true,
            },
        };
        chunk.Map.Things.Add(plan);

        Assert.True(ThingVisibility.IsActionable(chunk, Faction.OfPlayer));
        Assert.True(
            TotalFog.Detours.HaulAIUtility.HaulToStorageJob_Prefix(
                new Pawn { Faction = Faction.OfPlayer },
                chunk
            )
        );
        Assert.False(ThingVisibility.IsVisible(chunk));
        Assert.False(Visibility.IsVisible(chunk));

        chunk.Map.Things.Remove(plan);
        Assert.False(ThingVisibility.IsActionable(chunk, Faction.OfPlayer));
        Assert.False(
            TotalFog.Detours.HaulAIUtility.HaulToStorageJob_Prefix(
                new Pawn { Faction = Faction.OfPlayer },
                chunk
            )
        );
    }

    [Theory]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, false, true, true, false)]
    [InlineData(true, true, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ConstructionDoesNotGrantUnrelatedHiddenWork(
        bool known,
        bool ownPlan,
        bool haulable,
        bool blocks,
        bool vanillaFog
    )
    {
        var chunk = Chunk(known, ordered: false);
        chunk.def.EverHaulable = haulable;
        chunk.Map.fogGrid.Fogged = vanillaFog;
        chunk.Map.Things.Add(
            new Thing
            {
                Map = chunk.Map,
                Faction = ownPlan ? Faction.OfPlayer : new Faction { loadID = 2 },
                def = new ThingDef { IsBlueprint = true, BlocksItems = blocks },
            }
        );

        Assert.False(ThingVisibility.IsActionable(chunk, Faction.OfPlayer));
    }

    [Theory]
    [InlineData(ThingCategory.Plant)]
    [InlineData(ThingCategory.Building)]
    public void ConstructionCanClearOtherNativeObstructionsWithoutDiscoveringThem(
        ThingCategory category
    )
    {
        var obstruction = new ThingWithComps { def = new ThingDef { category = category } };
        obstruction.Map.Fog.Initialized = true;
        obstruction.Map.Fog.knownCells[0] = true;
        obstruction.Map.Things.Add(
            new Thing
            {
                Faction = Faction.OfPlayer,
                def = new ThingDef { IsBlueprint = true, BlocksItems = true },
            }
        );

        Assert.True(ThingVisibility.IsActionable(obstruction, Faction.OfPlayer));
        Assert.False(ThingVisibility.IsVisible(obstruction));
        Assert.False(Visibility.IsVisible(obstruction));

        obstruction.Map.Things.Clear();
        Assert.False(ThingVisibility.IsActionable(obstruction, Faction.OfPlayer));
    }

    [Theory]
    [InlineData(ThingCategory.Pawn)]
    [InlineData(ThingCategory.Projectile)]
    [InlineData(ThingCategory.Mote)]
    public void ConstructionDoesNotAuthorizeUnrelatedEntityKinds(ThingCategory category)
    {
        var target = new ThingWithComps { def = new ThingDef { category = category } };
        target.Map.Fog.Initialized = true;
        target.Map.Fog.knownCells[0] = true;
        target.Map.Things.Add(
            new Thing
            {
                Faction = Faction.OfPlayer,
                def = new ThingDef { IsBlueprint = true, BlocksItems = true },
            }
        );

        Assert.False(ThingVisibility.IsActionable(target, Faction.OfPlayer));
    }
}
