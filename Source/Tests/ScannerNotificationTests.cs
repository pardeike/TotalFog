using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Core;
using TotalFog.Notifications;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class ScannerNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Colony_scanner_discoveries_are_immediate_without_revealing_the_deposit(bool discard)
    {
        var map = new Map();
        map.Fog.Initialized = true;
        var targets = HiddenCell(map);
        Assert.False(NotificationVisibility.HasVisibleTarget(targets));

        ScannerNotifications.Prefix(Scanner(Faction.OfPlayer), out var previous);
        try
        {
            Assert.True(NotificationVisibility.HasVisibleTarget(targets));
            Assert.Equal(
                NotificationDecision.Show,
                NotificationPolicy.Decide(
                    !NotificationVisibility.HasVisibleTarget(targets),
                    discard,
                    delay: true,
                    replay: false
                )
            );
            Assert.False(map.Fog.InSight[0]);
            Assert.False(map.Fog.knownCells[0]);
        }
        finally
        {
            ScannerNotifications.Finalizer(previous);
        }

        Assert.False(NotificationVisibility.HasVisibleTarget(targets));
        Assert.Equal(
            discard ? NotificationDecision.Drop : NotificationDecision.Defer,
            NotificationPolicy.Decide(
                !NotificationVisibility.HasVisibleTarget(targets),
                discard,
                true,
                false
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unowned_and_enemy_scanners_do_not_bypass_fog(bool unowned)
    {
        var map = new Map();
        map.Fog.Initialized = true;
        ScannerNotifications.Prefix(Scanner(unowned ? null : new Faction()), out var previous);
        try
        {
            Assert.False(NotificationVisibility.HasVisibleTarget(HiddenCell(map)));
        }
        finally
        {
            ScannerNotifications.Finalizer(previous);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nested_reports_restore_the_outer_scope(bool outerOwned)
    {
        ScannerNotifications.Prefix(Scanner(outerOwned ? Faction.OfPlayer : null), out var initial);
        try
        {
            ScannerNotifications.Prefix(Scanner(Faction.OfPlayer), out var outer);
            try
            {
                Assert.True(ScannerNotifications.IsPlayerReport);
            }
            finally
            {
                ScannerNotifications.Finalizer(outer);
            }
            Assert.Equal(outerOwned, ScannerNotifications.IsPlayerReport);
        }
        finally
        {
            ScannerNotifications.Finalizer(initial);
        }
        Assert.False(ScannerNotifications.IsPlayerReport);
    }

    [Fact]
    public void Failed_discovery_does_not_leak_its_notification_exception()
    {
        var failure = new InvalidOperationException("discovery fixture");
        Assert.Same(
            failure,
            Assert.Throws<InvalidOperationException>(
                (Action)(
                    () =>
                    {
                        ScannerNotifications.Prefix(Scanner(Faction.OfPlayer), out var previous);
                        try
                        {
                            throw failure;
                        }
                        finally
                        {
                            ScannerNotifications.Finalizer(previous);
                        }
                    }
                )
            )
        );
        Assert.False(ScannerNotifications.IsPlayerReport);
    }

    [Fact]
    public void Targetless_long_range_reports_remain_immediate_outside_a_scanner_scope()
    {
        Assert.False(ScannerNotifications.IsPlayerReport);
        Assert.True(NotificationVisibility.HasVisibleTarget(new LookTargets()));
    }

    [Fact]
    public void A_skipped_prefix_cannot_clear_an_outer_scanner_report()
    {
        ScannerNotifications.Prefix(Scanner(Faction.OfPlayer), out var initial);
        try
        {
            ScannerNotifications.Finalizer(null);
            Assert.True(ScannerNotifications.IsPlayerReport);
        }
        finally
        {
            ScannerNotifications.Finalizer(initial);
        }
        Assert.False(ScannerNotifications.IsPlayerReport);
    }

    private static CompScanner Scanner(Faction faction) =>
        new() { parent = new ThingWithComps { Faction = faction } };

    private static LookTargets HiddenCell(Map map) =>
        new()
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
}
