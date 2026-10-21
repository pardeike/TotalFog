using System;
using RimWorld;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class RememberedWorkTests : IDisposable
{
    private readonly bool previous = FogSettings.InteractWithRememberedObjects;

    public RememberedWorkTests() => FogSettings.InteractWithRememberedObjects = true;

    public void Dispose() => FogSettings.InteractWithRememberedObjects = previous;

    [Fact]
    public void ObservedAnimalCorpseRetainsWorkAccessWithoutLiveSight()
    {
        var corpse = ObservedCorpse();
        Assert.True(CanReserve(corpse));
        Assert.True(ThingVisibility.IsActionable(corpse, Faction.OfPlayer));
        Assert.False(ThingVisibility.IsVisible(corpse));
        Assert.False(Visibility.IsVisible(corpse));
    }

    [Fact]
    public void RememberedCorpseSupportsWorkMenusButNotCombatTargeting()
    {
        var corpse = ObservedCorpse();
        var context = new FloatMenuContext { ClickedCell = corpse.Position };
        context.ClickedThings.Add(corpse);
        InterfaceVisibility.ContextMenuPostfix(context);
        Assert.Contains(corpse, context.ClickedThings);
        Assert.False(InterfaceVisibility.CanReadMenuTarget(corpse, corpse.Position));
        FogSettings.InteractWithRememberedObjects = false;
        Assert.False(ThingVisibility.CanInspect(corpse));
        Assert.True(CanReserve(corpse));
    }

    [Fact]
    public void UnknownReplacementAndHiddenMovementDoNotGrantCorpseWorkAccess()
    {
        var corpse = ObservedCorpse();
        Assert.False(CanReserve(new Corpse { Map = corpse.Map, InnerPawn = new Pawn() }));
        corpse.PositionHeld = new IntVec3(1, 0);
        corpse.Map.Fog.knownCells[1] = true;
        Assert.False(CanReserve(corpse));
        Assert.False(ThingVisibility.CanInspect(corpse));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KnownValidStorageKeepsBillInputsReservable(bool corpse)
    {
        Thing item = corpse ? new Corpse { InnerPawn = new Pawn() } : new Thing();
        item.def.category = ThingCategory.Item;
        item.def.EverHaulable = true;
        item.InValidStorage = true;
        item.Map.Fog.Initialized = true;
        item.Map.Fog.knownCells[0] = true;
        Assert.True(CanReserve(item));
        Assert.False(ThingVisibility.IsVisible(item));
        Assert.False(Visibility.IsVisible(item));
        Assert.False(ThingVisibility.CanInspect(item));
        item.InValidStorage = false;
        Assert.False(CanReserve(item));
    }

    [Fact]
    public void StoredItemsDoNotBypassNativeFogFactionKnowledgeOrReservationFailure()
    {
        var item = new Thing { InValidStorage = true };
        item.def.category = ThingCategory.Item;
        item.Map.Fog.Initialized = true;
        Assert.False(CanReserve(item));
        item.Map.Fog.knownCells[0] = true;
        item.Map.fogGrid.Fogged = true;
        Assert.False(CanReserve(item));
        item.Map.fogGrid.Fogged = false;
        var other = new Faction { IsPlayer = true, loadID = 2 };
        Assert.False(ThingVisibility.IsActionable(item, other));
        bool nativeResult = false;
        TotalFog.Detours.ReservationUtility.CanReserve_Postfix(
            new Pawn { Faction = Faction.OfPlayer },
            ref nativeResult,
            new LocalTargetInfo(item)
        );
        Assert.False(nativeResult);
    }

    private static bool CanReserve(Thing thing)
    {
        bool result = true;
        TotalFog.Detours.ReservationUtility.CanReserve_Postfix(
            new Pawn { Faction = Faction.OfPlayer, Map = thing.Map },
            ref result,
            new LocalTargetInfo(thing)
        );
        return result;
    }

    private static Corpse ObservedCorpse()
    {
        var corpse = new Corpse { InnerPawn = new Pawn() };
        corpse.def.category = ThingCategory.Item;
        corpse.Map.Fog.Initialized = true;
        corpse.Map.Fog.knownCells[0] = true;
        corpse.Map.Fog.InSight[0] = true;
        var comp = new CompFog();
        comp.HideFromPlayer = new CompVisibility { parent = corpse, mainComponent = comp };
        corpse.Component = comp;
        comp.HideFromPlayer.PostSpawnSetup(false);
        corpse.Map.Fog.InSight[0] = false;
        comp.HideFromPlayer.UpdateVisibility(true);
        return corpse;
    }
}
