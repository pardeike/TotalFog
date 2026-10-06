namespace TotalFog.Core;

public enum NotificationDecision
{
    Show,
    Defer,
    Drop,
}

public static class NotificationPolicy
{
    public static NotificationDecision Decide(
        bool hidden,
        bool suppress,
        bool delay,
        bool replay
    ) =>
        replay || !hidden ? NotificationDecision.Show
        : suppress ? NotificationDecision.Drop
        : delay ? NotificationDecision.Defer
        : NotificationDecision.Show;
}
