using UnityEngine;
using Verse;

namespace TotalFog;

public partial class FogSettings : ModSettings
{
    public static string CurrentVersion;

    private static FogFadeSpeedEnum fogFadeSpeed = FogFadeSpeedEnum.Medium;

    private static FogAlpha fogAlpha = FogAlpha.Medium;

    public static int BaseViewRange = 60;
    public static float BaseHearingRange = 10;
    public static bool ShowHearingCues = true;

    public static float BuildingVisionModifier = 1;

    public static float TurretVisionModifier = 0.7f;

    public static float AnimalVisionModifier = 0.5f;

    public static int AudioSourceRange = 30; // Max tiles you can "hear"
    private static string audioSourceRangeBuffer;
    public static float VolumeMufflingModifier = 0.5f; // 0 = no dropoff, 1 = full dropoff.

    public static bool HideSpeakBubble;

    public static bool AISmart;
    public static bool NeedWatcher = true;
    public static bool HideThreatBig;
    public static bool HideThreatSmall;
    public static bool HideEventPositive;
    public static bool HideEventNegative;
    public static bool OnlyOutsideColony;
    public static bool HideEventNeutral;
    public static bool PrisonerGiveVision;
    public static bool AllyGiveVision;
    public static bool MapRevealAtStart;
    public static bool WildLifeTabVisible = true;
    private static bool needMemoryStorage = true;
    public static bool SuppressCombatMusic = true;
    public static bool MuteHiddenSounds;
    public static bool DoAudioCheck; // Whether the audio check should be performed for fogged sounds.
    public static bool DelayAlertsUntilSeen;
    public static bool SilentRaids;
    public static bool ClearFogDuringTargeting = true;

    private static bool treesBlockSightValue;

    public static bool TreesBlockSight
    {
        get => treesBlockSightValue;
        private set => treesBlockSightValue = value;
    }

    // public static bool doFilthReveal = true; // Whether filth should be automatically revealed when its created

    private static void applySettings()
    {
        SectionLayerFog.PrefFadeSpeedMult = (int)fogFadeSpeed;
        SectionLayerFog.PrefEnableFade = fogFadeSpeed != FogFadeSpeedEnum.Disabled;
        SectionLayerFog.PrefFogAlpha = (byte)fogAlpha;
        // Appearance and configuration serialization never publish gameplay
        // sight. Source ticks apply synchronized vision changes within 30 ticks.
        if (Current.ProgramState == ProgramState.Playing)
            foreach (var map in Find.Maps)
                map.mapDrawer?.RegenerateEverythingNow();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref fogFadeSpeed, "fogFadeSpeed", FogFadeSpeedEnum.Medium);
        Scribe_Values.Look(ref fogAlpha, "fogAlpha", FogAlpha.Medium);
        Scribe_Values.Look(ref BaseViewRange, "baseViewRange", 60);
        Scribe_Values.Look(ref BuildingVisionModifier, "buildingVisionMod", 1);
        Scribe_Values.Look(ref AnimalVisionModifier, "animalVisionMod", 0.5f);
        Scribe_Values.Look(ref TurretVisionModifier, "turretVisionMod", 0.7f);
        Scribe_Values.Look(ref BaseHearingRange, "baseHearingRange", 10);
        Scribe_Values.Look(ref ShowHearingCues, "totalFogShowHearingCues", true);
        Scribe_Values.Look(ref WildLifeTabVisible, "wildLifeTabVisible", true);
        Scribe_Values.Look(ref PrisonerGiveVision, "prisonerGiveVision");
        Scribe_Values.Look(ref MapRevealAtStart, "mapRevealAtStart");
        Scribe_Values.Look(ref AllyGiveVision, "allyGiveVision");
        Scribe_Values.Look(ref NeedWatcher, "needWatcher", true);
        Scribe_Values.Look(ref needMemoryStorage, "needMemoryStorage", true);
        Scribe_Values.Look(ref HideEventNegative, "hideEventNegative");
        Scribe_Values.Look(ref OnlyOutsideColony, "onlyOutsideColony");
        Scribe_Values.Look(ref HideEventNeutral, "hideEventNeutral");
        Scribe_Values.Look(ref HideEventPositive, "hideEventPositive");
        Scribe_Values.Look(ref HideThreatBig, "hideThreatBig");
        Scribe_Values.Look(ref HideThreatSmall, "hideThreatSmall");
        Scribe_Values.Look(ref HideSpeakBubble, "hideSpeakBubble");
        Scribe_Values.Look(ref AISmart, "aiSmart");
        Scribe_Values.Look(ref SuppressCombatMusic, "totalFogSuppressCombatMusic", true);
        Scribe_Values.Look(ref MuteHiddenSounds, "totalFogMuteHiddenSounds");
        Scribe_Values.Look(ref DoAudioCheck, "doAudioCheck");
        Scribe_Values.Look(ref AudioSourceRange, "audioSourceRange", 30);
        Scribe_Values.Look(ref VolumeMufflingModifier, "volumeMufflingModifier", 0.5f);
        Scribe_Values.Look(ref DelayAlertsUntilSeen, "delayAlertsUntilSeen");
        Scribe_Values.Look(ref SilentRaids, "totalFogSilentRaids");
        Scribe_Values.Look(ref ClearFogDuringTargeting, "clearFogDuringTargeting", true);
        Scribe_Values.Look(ref treesBlockSightValue, "treesBlockSight");

        if (Scribe.mode != LoadSaveMode.Saving)
            applySettings();
    }

    private enum FogAlpha
    {
        Black = 255,
        NearlyBlack = 210,
        VeryVeryVeryDark = 180,
        VeryVeryDark = 150,
        VeryDark = 120,
        Dark = 100,
        Medium = 80,
        Light = 60,
    }

    private enum FogFadeSpeedEnum
    {
        Slow = 5,
        Medium = 20,
        Fast = 40,
        Disabled = 100,
    }
}
