namespace TotalFog.Core;

public static class VisibilityPolicy
{
    public static bool Entity(
        bool initialized,
        bool vanillaFog,
        bool bypass,
        bool ownObserver,
        bool mobile,
        bool inSight,
        bool remembered
    )
    {
        if (vanillaFog)
            return false;
        if (!initialized || bypass || ownObserver)
            return true;
        return inSight || !mobile && remembered;
    }

    public static bool IsSightSource(bool pawn, int radius) => radius >= 0 && (pawn || radius > 0);

    public static float Hearing(float distance, float range, float muffling)
    {
        if (range <= 0 || distance >= range)
            return 0;
        if (distance <= 0)
            return 1;
        return 1 - Clamp(muffling) * distance / range;
    }

    private static float Clamp(float value) =>
        value < 0 ? 0
        : value > 1 ? 1
        : value;
}
