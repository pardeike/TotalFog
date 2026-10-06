using Verse;

namespace TotalFog;

public class CompProperties_BuildingSight : CompProperties
{
    public readonly bool needManned = false;

    public float viewRadius;

    public CompProperties_BuildingSight()
    {
        compClass = typeof(CompBuildingSight);
    }
}
