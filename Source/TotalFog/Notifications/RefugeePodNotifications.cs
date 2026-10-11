using Verse;

namespace TotalFog.Notifications;

internal static class RefugeePodNotifications
{
    // The native quest sends its letter before the passenger is held by a pod.
    // Keep the supplied destination only for that call, never as game state.
    internal static Map ArrivalMap { get; private set; }

    public static void Prefix(Map map, out Map __state)
    {
        __state = ArrivalMap;
        ArrivalMap = map;
    }

    public static void Finalizer(Map __state) => ArrivalMap = __state;
}
