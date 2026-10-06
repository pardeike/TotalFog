using Verse;

namespace RimWorldRealFoW;

public class CompProperties_AffectVision : CompProperties
{
    public readonly bool denyDarkness = false;

    public readonly bool denyWeather = false;

    public bool applyImmediately;

    public float fovMultiplier;

    public CompProperties_AffectVision()
    {
        compClass = typeof(CompAffectVision);
    }
}