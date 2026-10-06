using System;
using System.Collections.Generic;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public class NotificationTests
{
    [Theory]
    [InlineData(false, false, true, false, NotificationDecision.Show)]
    [InlineData(true, false, true, false, NotificationDecision.Defer)]
    [InlineData(true, false, false, false, NotificationDecision.Show)]
    [InlineData(false, true, true, false, NotificationDecision.Show)]
    [InlineData(true, true, true, false, NotificationDecision.Drop)]
    [InlineData(true, true, true, true, NotificationDecision.Show)]
    public void ExplicitPolicyHandlesReplayAndSettings(
        bool hidden,
        bool suppress,
        bool delay,
        bool replay,
        NotificationDecision expected
    ) => Assert.Equal(expected, NotificationPolicy.Decide(hidden, suppress, delay, replay));

    [Fact]
    public void SeveralEventsForSameTargetAreNotDiscardedAndReplayOnceInOrder()
    {
        var queue = new PendingQueue<string>();
        queue.Add("letter");
        queue.Add("message");
        queue.Add("letter");
        var seen = new List<string>();
        queue.Drain(_ => true, _ => true, seen.Add);
        queue.Drain(_ => true, _ => true, seen.Add);
        Assert.Equal(new[] { "letter", "message" }, seen);
        Assert.Empty(queue.Items);
    }

    [Fact]
    public void DestroyedEventsAreRemovedButHiddenEventsRemain()
    {
        var queue = new PendingQueue<string>();
        queue.Add("dead");
        queue.Add("hidden");
        queue.Drain(s => s != "dead", _ => false, _ => throw new Exception());
        Assert.Equal(new[] { "hidden" }, queue.Items);
    }

    [Fact]
    public void FailedReplayDoesNotLoseThePayload()
    {
        var queue = new PendingQueue<string>();
        queue.Add("event");
        Assert.Throws<InvalidOperationException>(() =>
            queue.Drain(_ => true, _ => true, _ => throw new InvalidOperationException())
        );
        Assert.Single(queue.Items);
    }
}
