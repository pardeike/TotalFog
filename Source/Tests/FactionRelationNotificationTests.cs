using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using TotalFog.Core;
using TotalFog.Notifications;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class FactionRelationNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Diplomacy_reports_are_immediate_without_revealing_their_hidden_target(bool discard)
    {
        var map = new Map();
        map.Fog.Initialized = true;
        var targets = new LookTargets
        {
            targets = new List<TargetInfo>
            {
                new()
                {
                    IsValid = true,
                    Map = map,
                    Cell = new IntVec3(0, 0),
                },
            },
        };
        Assert.False(NotificationVisibility.HasVisibleTarget(targets));
        var faction = new Faction();
        FactionRelationNotifications.Prefix(
            faction,
            Faction.OfPlayer,
            true,
            new GlobalTargetInfo { Map = map, Cell = new IntVec3(0, 0) },
            out var previous
        );
        try
        {
            Assert.Equal(
                NotificationDecision.Show,
                NotificationPolicy.Decide(
                    !FactionRelationNotifications.IsReport(targets, faction)
                        && !NotificationVisibility.HasVisibleTarget(targets),
                    discard,
                    true,
                    false
                )
            );
            Assert.False(map.Fog.InSight[0]);
            Assert.False(map.Fog.knownCells[0]);
        }
        finally
        {
            FactionRelationNotifications.Finalizer(previous);
        }
        Assert.False(NotificationVisibility.HasVisibleTarget(targets));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_letters_and_other_factions_do_not_open_a_report_scope(bool player)
    {
        FactionRelationNotifications.Prefix(
            new Faction(),
            player ? Faction.OfPlayer : new Faction(),
            !player,
            default,
            out var previous
        );
        try
        {
            Assert.False(FactionRelationNotifications.IsPlayerReport);
        }
        finally
        {
            FactionRelationNotifications.Finalizer(previous);
        }
    }

    [Fact]
    public void Nested_and_skipped_prefixes_preserve_the_outer_report()
    {
        FactionRelationNotifications.Prefix(
            new Faction(),
            Faction.OfPlayer,
            true,
            default,
            out var initial
        );
        try
        {
            FactionRelationNotifications.Prefix(
                new Faction(),
                new Faction(),
                false,
                default,
                out var nested
            );
            FactionRelationNotifications.Finalizer(nested);
            FactionRelationNotifications.Finalizer(null);
            Assert.True(FactionRelationNotifications.IsPlayerReport);
        }
        finally
        {
            FactionRelationNotifications.Finalizer(initial);
        }
        Assert.False(FactionRelationNotifications.IsPlayerReport);
    }

    [Fact]
    public void Quest_callback_events_keep_their_own_faction_and_target_checks()
    {
        var map = new Map();
        map.Fog.Initialized = true;
        var faction = new Faction();
        var pawn = new Pawn { Faction = faction, Map = map };
        var targets = new LookTargets
        {
            targets = new List<TargetInfo>
            {
                new()
                {
                    IsValid = true,
                    Thing = pawn,
                    Map = map,
                },
            },
        };
        FactionRelationNotifications.Prefix(
            faction,
            Faction.OfPlayer,
            true,
            new GlobalTargetInfo { Thing = pawn, Map = map },
            out var previous
        );
        try
        {
            Assert.True(FactionRelationNotifications.IsReport(targets, faction));
            Assert.False(FactionRelationNotifications.IsReport(targets, new Faction()));
            Assert.False(NotificationVisibility.HasVisibleTarget(targets));
            var otherPawn = new Pawn { Faction = faction, Map = map };
            targets.targets[0] = new TargetInfo
            {
                IsValid = true,
                Thing = otherPawn,
                Map = map,
            };
            Assert.False(FactionRelationNotifications.IsReport(targets, faction));
            targets.targets.Add(
                new TargetInfo
                {
                    IsValid = true,
                    Thing = pawn,
                    Map = map,
                }
            );
            Assert.False(FactionRelationNotifications.IsReport(targets, faction));
        }
        finally
        {
            FactionRelationNotifications.Finalizer(previous);
        }
    }

    [Fact]
    public void Failed_report_restores_the_scope_and_preserves_the_error()
    {
        var error = new InvalidOperationException("diplomacy fixture");
        Assert.Same(
            error,
            Assert.Throws<InvalidOperationException>(
                (Action)(
                    () =>
                    {
                        FactionRelationNotifications.Prefix(
                            new Faction(),
                            Faction.OfPlayer,
                            true,
                            default,
                            out var previous
                        );
                        try
                        {
                            throw error;
                        }
                        finally
                        {
                            FactionRelationNotifications.Finalizer(previous);
                        }
                    }
                )
            )
        );
        Assert.False(FactionRelationNotifications.IsPlayerReport);
    }
}
