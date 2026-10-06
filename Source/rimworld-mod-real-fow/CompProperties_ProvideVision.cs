using Verse;

namespace RimWorldRealFoW;

public class CompProperties_ProvideVision : CompProperties
{
    public readonly bool needManned = false;

    public float viewRadius;

    public CompProperties_ProvideVision()
    {
        compClass = typeof(CompProvideVision);
    }
}