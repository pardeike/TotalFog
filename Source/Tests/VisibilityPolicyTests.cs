using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class VisibilityPolicyTests
{
    [Theory]
    [InlineData(false, -1, false)]
    [InlineData(false, 0, false)]
    [InlineData(false, 1, true)]
    [InlineData(true, -1, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, 30, true)]
    public void ZeroRangeBuildingsDoNotObserveTheirOwnCells(bool pawn, int radius, bool expected) =>
        Assert.Equal(expected, VisibilityPolicy.IsSightSource(pawn, radius));

    [Theory]
    [InlineData(true, false, false, false, true, false, true, false)]
    [InlineData(true, false, false, false, false, false, true, true)]
    [InlineData(true, false, false, false, false, false, false, false)]
    [InlineData(true, false, false, false, true, true, false, true)]
    [InlineData(true, false, false, true, true, false, false, true)]
    [InlineData(true, true, false, true, true, true, true, false)]
    [InlineData(true, false, true, false, true, false, false, true)]
    [InlineData(false, false, false, false, true, false, false, true)]
    public void DistinguishesCurrentSightFromRememberedObjects(
        bool init,
        bool vanilla,
        bool bypass,
        bool ownObserver,
        bool mobile,
        bool sight,
        bool remembered,
        bool expected
    ) =>
        Assert.Equal(
            expected,
            VisibilityPolicy.Entity(init, vanilla, bypass, ownObserver, mobile, sight, remembered)
        );

    [Theory]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 30, 1, 1)]
    [InlineData(15, 30, 1, .5f)]
    [InlineData(30, 30, 1, 0)]
    [InlineData(15, 30, 0, 1)]
    public void HearingIsBoundedAndHandlesZeroRange(
        float distance,
        float range,
        float muffling,
        float expected
    ) => Assert.Equal(expected, VisibilityPolicy.Hearing(distance, range, muffling));
}
