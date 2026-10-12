using RimWorld;
using RimWorld.Planet;
using TotalFog.Notifications;
using TotalFog.Presentation;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class OwnedWorkVisibilityTests
{
    [Fact]
    public void Known_home_fire_is_actionable_without_granting_live_effect_sight()
    {
        var fire = new Fire();
        fire.Map.Fog.Initialized = true;
        fire.Map.Fog.knownCells[0] = true;
        fire.Map.areaManager.Home.Active = true;
        var worker = new Pawn { Faction = Faction.OfPlayer, Map = fire.Map };
        Assert.True(ThingVisibility.IsActionable(fire, worker.Faction));
        Assert.False(Visibility.IsVisible(fire));
        bool result = true;
        TotalFog.Detours.ReservationUtility.CanReserve_Postfix(
            worker,
            ref result,
            new LocalTargetInfo(fire)
        );
        Assert.True(result);
        fire.Map.areaManager.Home.Active = false;
        Assert.False(ThingVisibility.IsActionable(fire, worker.Faction));
        fire.Map.areaManager.Home.Active = true;
        fire.Map.Fog.knownCells[0] = false;
        Assert.False(ThingVisibility.IsActionable(fire, worker.Faction));
    }

    [Fact]
    public void Known_own_corpse_can_be_recovered_without_revealing_foreign_bodies()
    {
        var corpse = new Corpse { InnerPawn = new Pawn { Faction = Faction.OfPlayer } };
        corpse.Map.Fog.Initialized = true;
        corpse.Map.Fog.knownCells[0] = true;
        Assert.True(ThingVisibility.IsActionable(corpse, Faction.OfPlayer));
        Assert.True(ThingVisibility.IsVisible(corpse));
        Assert.False(Visibility.IsVisible(corpse));
        corpse.InnerPawn.Faction = new Faction();
        Assert.False(ThingVisibility.IsActionable(corpse, Faction.OfPlayer));
    }

    [Fact]
    public void Threat_alert_filters_hidden_jump_targets_and_preserves_native_unlocated_alerts()
    {
        bool previous = FogSettings.DelayAlertsUntilSeen;
        try
        {
            FogSettings.DelayAlertsUntilSeen = true;
            var map = new Map();
            map.Fog.Initialized = true;
            map.Fog.InSight[0] = true;
            var shown = new GlobalTargetInfo
            {
                IsValid = true,
                Map = map,
                Cell = new IntVec3(0, 0),
            };
            var hidden = new GlobalTargetInfo
            {
                IsValid = true,
                Map = map,
                Cell = new IntVec3(1, 0),
            };
            var report = AlertReport.CulpritsAre(new() { shown, hidden });
            ThreatAlerts.ReportPostfix(ref report);
            Assert.True(report.active);
            Assert.Single(report.culpritsTargets);
            report = AlertReport.CulpritsAre(new() { hidden });
            ThreatAlerts.ReportPostfix(ref report);
            Assert.False(report.active);
            report = new AlertReport { active = true };
            ThreatAlerts.ReportPostfix(ref report);
            Assert.True(report.active);
            FogSettings.DelayAlertsUntilSeen = false;
            report = AlertReport.CulpritsAre(new() { hidden });
            ThreatAlerts.ReportPostfix(ref report);
            Assert.True(report.active);
        }
        finally
        {
            FogSettings.DelayAlertsUntilSeen = previous;
        }
    }
}
