using System.Collections.Generic;
using TotalFog.Core;
using TotalFog.Notifications;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class NotificationVisibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColonyHealthEventsRemainObservableOutsideCurrentSight(bool prisoner)
    {
        var pawn = new Pawn
        {
            Faction = prisoner ? new Faction() : Faction.OfPlayer,
            IsPrisonerOfColony = prisoner,
        };
        pawn.Map.Fog.Initialized = true;
        var targets = For(pawn);

        Assert.False(Presentation.ThingVisibility.IsVisible(pawn, allowMemory: false));
        Assert.True(NotificationVisibility.HasVisibleTarget(targets));
        Assert.Equal(
            NotificationDecision.Show,
            NotificationPolicy.Decide(
                !NotificationVisibility.HasVisibleTarget(targets),
                suppress: true,
                delay: true,
                replay: false
            )
        );
    }

    [Fact]
    public void HiddenEnemyEventsRemainDeferredOrDiscarded()
    {
        var pawn = new Pawn { Faction = new Faction() };
        pawn.Map.Fog.Initialized = true;
        var targets = For(pawn);
        Assert.False(NotificationVisibility.HasVisibleTarget(targets));
        Assert.Equal(
            NotificationDecision.Defer,
            NotificationPolicy.Decide(true, false, true, false)
        );
        Assert.Equal(NotificationDecision.Drop, NotificationPolicy.Decide(true, true, true, false));
        pawn.Map.Fog.InSight[0] = true;
        Assert.True(NotificationVisibility.HasVisibleTarget(targets));
    }

    [Fact]
    public void RememberedEnemyBuildingsDoNotMakeTheirEventsObservable()
    {
        var thing = new Thing { Faction = new Faction() };
        thing.Map.Fog.Initialized = true;
        thing.Map.Fog.knownCells[0] = true;
        Assert.False(NotificationVisibility.HasVisibleTarget(For(thing)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GlobalEventsWithoutTargetsRemainImmediate(bool nullTargets)
    {
        LookTargets targets = nullTargets ? null : new LookTargets();
        Assert.True(NotificationVisibility.HasVisibleTarget(targets));
        Assert.Equal(
            NotificationDecision.Show,
            NotificationPolicy.Decide(
                !NotificationVisibility.HasVisibleTarget(targets),
                true,
                true,
                false
            )
        );
    }

    [Fact]
    public void DestroyedColonyTargetsAreNotWhitelistedAsObservable()
    {
        var pawn = new Pawn { Faction = Faction.OfPlayer, Destroyed = true };
        pawn.Map.Fog.Initialized = true;
        Assert.False(NotificationVisibility.HasVisibleTarget(For(pawn)));
        Assert.False(NotificationVisibility.HasLiveTarget(For(pawn)));
    }

    private static LookTargets For(Thing thing) =>
        new()
        {
            targets = new List<TargetInfo>
            {
                new() { Thing = thing, IsValid = true },
            },
        };
}
