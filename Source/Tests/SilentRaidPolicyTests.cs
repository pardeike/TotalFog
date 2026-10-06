using System;
using RimWorld;
using TotalFog.Notifications;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class SilentRaidPolicyTests
{
    [Theory]
    [InlineData("enemy", true, false, true)]
    [InlineData("manhunter", true, false, true)]
    [InlineData("other", true, false, false)]
    [InlineData("enemy", false, false, false)]
    [InlineData("manhunter", false, false, false)]
    [InlineData("enemy", true, true, true)]
    [InlineData("manhunter", true, true, true)]
    [InlineData("other", false, true, true)]
    public void OnlySelectedThreatArrivalsBecomeSilentAndTheirParametersAreRestored(
        string kind, bool enabled, bool wasSilent, bool expectedSilent)
    {
        bool previous = FogSettings.SilentRaids;
        var worker = Worker(kind);
        var parms = new IncidentParms { silent = wasSilent, points = 123f };
        bool state = false;
        try
        {
            FogSettings.SilentRaids = enabled;
            SilentRaidPolicy.Prefix(worker, parms, out state);
            Assert.Equal(expectedSilent, parms.silent);
            Assert.True(parms.sendLetter);
            Assert.Equal(123f, parms.points);
        }
        finally
        {
            SilentRaidPolicy.Finalizer(parms, state);
            FogSettings.SilentRaids = previous;
        }
        Assert.Equal(wasSilent, parms.silent);
    }

    [Fact]
    public void FailedIncidentExecutionDoesNotLeaveItsParametersSilent()
    {
        bool previous = FogSettings.SilentRaids;
        var parms = new IncidentParms();
        bool state = false;
        try
        {
            FogSettings.SilentRaids = true;
            SilentRaidPolicy.Prefix(Worker("enemy"), parms, out state);
            Assert.True(parms.silent);
            Assert.Throws<InvalidOperationException>((Action)(() => throw new InvalidOperationException("failed worker")));
        }
        finally
        {
            SilentRaidPolicy.Finalizer(parms, state);
            FogSettings.SilentRaids = previous;
        }
        Assert.False(parms.silent);
    }

    [Theory]
    [InlineData("manhunter", true, true, 0)]
    [InlineData("manhunter", true, false, 1)]
    [InlineData("manhunter", false, true, 1)]
    [InlineData("other", true, true, 1)]
    public void ArrivalSlowdownExceptionIsLimitedToEnabledSilentManhunterPacks(
        string kind, bool enabled, bool silent, int expectedSignals)
    {
        bool previous = FogSettings.SilentRaids;
        try
        {
            FogSettings.SilentRaids = enabled;
            var slower = new TimeSlower();
            SilentRaidPolicy.ManhunterArrivalSlowdown(slower, Worker(kind), new IncidentParms { silent = silent });
            Assert.Equal(expectedSignals, slower.Signals);
        }
        finally { FogSettings.SilentRaids = previous; }
    }

    private static IncidentWorker Worker(string kind) => kind switch
    {
        "enemy" => new IncidentWorker_RaidEnemy(),
        "manhunter" => new IncidentWorker { def = IncidentDefOf.ManhunterPack },
        _ => new IncidentWorker { def = new IncidentDef() }
    };
}
