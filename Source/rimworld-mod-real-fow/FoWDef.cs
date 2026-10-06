using RimWorld;
using Verse;

namespace RimWorldRealFoW;

[DefOf]
public static class FoWDef
{
    public static JobDef SurveilCameraConsole;

    public static ThingDef CameraConsole;

    public static StatDef DayVisionEffectiveness;

    public static StatDef NightVisionEffectiveness;

    public static ThingDef Mote_SoundWave;

    public static MapMeshFlagDef RealFogOfWar;

    static FoWDef()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FoWDef));
    }
}