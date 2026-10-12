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
    public void Dead_letter_payload_can_reach_history_once_instead_of_being_invalidated()
    {
        var queue = new PendingQueue<string>();
        queue.Add("dead-target letter");
        var archive = new List<string>();
        queue.Drain(_ => true, _ => false, archive.Add);
        Assert.Single(queue.Items);
        queue.Drain(_ => true, _ => true, archive.Add);
        queue.Drain(_ => true, _ => true, archive.Add);
        Assert.Single(archive);
        Assert.Empty(queue.Items);
    }

    [Fact]
    public void FailedReplayIsDroppedAndLaterItemsStillReplayOnce()
    {
        var queue = new PendingQueue<string>();
        queue.Add("broken");
        queue.Add("hidden");
        queue.Add("later");
        var replayed = new List<string>();
        var failed = new List<string>();
        void Replay(string item)
        {
            if (item == "broken")
                throw new InvalidOperationException();
            replayed.Add(item);
        }
        queue.Drain(_ => true, item => item != "hidden", Replay, (item, _) => failed.Add(item));
        queue.Drain(_ => true, item => item != "hidden", Replay);
        Assert.Equal(new[] { "broken" }, failed);
        Assert.Equal(new[] { "later" }, replayed);
        Assert.Equal(new[] { "hidden" }, queue.Items);
    }
}
