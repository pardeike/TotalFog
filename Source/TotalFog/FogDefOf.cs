using RimWorld;
using Verse;

namespace TotalFog;

[DefOf]
public static class FogDefOf
{
    public static JobDef SurveilCameraConsole;

    public static ThingDef CameraConsole;

    public static StatDef DayVisionEffectiveness;

    public static StatDef NightVisionEffectiveness;

    public static ThingDef Mote_SoundWave;

    public static MapMeshFlagDef RealFogOfWar;

    static FogDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FogDefOf));
    }
}
