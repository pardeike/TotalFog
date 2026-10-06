using Verse;

namespace TotalFog;

public class CompProperties_SightModifier : CompProperties
{
    public readonly bool denyDarkness = false;

    public readonly bool denyWeather = false;

    public bool applyImmediately;

    public float fovMultiplier;

    public CompProperties_SightModifier()
    {
        compClass = typeof(CompSightModifier);
    }
}
