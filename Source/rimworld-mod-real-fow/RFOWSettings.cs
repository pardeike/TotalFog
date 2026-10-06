using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimWorldRealFoW;

public class RfowSettings : ModSettings
{
    public static string CurrentVersion;

    private static Vector2 scrollPosition = Vector2.zero;

    private static FogFadeSpeedEnum fogFadeSpeed = FogFadeSpeedEnum.Medium;

    private static FogAlpha fogAlpha = FogAlpha.Medium;

    public static int BaseViewRange = 60;
    public static float BaseHearingRange = 10;

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
    public static bool DoAudioCheck; // Whether the audio check should be performed for fogged sounds.
    public static bool DelayAlertsUntilSeen;
    public static bool ClearFogDuringTargeting = true;

    private static bool treesBlockSightValue;

    public static bool TreesBlockSight
    {
        get => treesBlockSightValue;
        private set => treesBlockSightValue = value;
    }


    // public static bool doFilthReveal = true; // Whether filth should be automatically revealed when its created


    public static void DoSettingsWindowContents(Rect rect)
    {
        rect.yMin += 15f;
        rect.yMax -= 15f;

        var defaultColumnWidth = rect.width - 50;
        var row = new Listing_Standard { ColumnWidth = defaultColumnWidth };


        var outRect = new Rect(rect.x, rect.y, rect.width, rect.height);
        var scrollRect = new Rect(0f, 0f, rect.width - 16f, rect.height * 2f);
        Widgets.BeginScrollView(outRect, ref scrollPosition, scrollRect);

        row.Begin(scrollRect);
        row.ColumnWidth = defaultColumnWidth;
        if (row.ButtonTextLabeled("fogAlphaSetting_title".Translate(), ("fogAlphaSetting_" + fogAlpha).Translate()))
        {
            var list = new List<FloatMenuOption>();
            foreach (var obj in Enum.GetValues(typeof(FogAlpha)))
            {
                var localValue3 = (FogAlpha)obj;
                list.Add(new FloatMenuOption(("fogAlphaSetting_" + localValue3).Translate(), delegate
                {
                    fogAlpha = localValue3;
                    applySettings();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(list));
        }

        Text.Font = GameFont.Tiny;
        row.Label("fogAlphaSetting_desc".Translate());
        Text.Font = GameFont.Small;

        if (row.ButtonTextLabeled("fogFadeSpeedSetting_title".Translate(),
                ("fogFadeSpeedSetting_" + fogFadeSpeed).Translate()))
        {
            var list2 = new List<FloatMenuOption>();
            foreach (var obj2 in Enum.GetValues(typeof(FogFadeSpeedEnum)))
            {
                var localValue2 = (FogFadeSpeedEnum)obj2;
                list2.Add(new FloatMenuOption(("fogFadeSpeedSetting_" + localValue2).Translate(), delegate
                {
                    fogFadeSpeed = localValue2;
                    applySettings();
                }));
            }

            Find.WindowStack.Add(new FloatMenu(list2));
        }

        Text.Font = GameFont.Tiny;
        row.Label("fogFadeSpeedSetting_desc".Translate());
        Text.Font = GameFont.Small;
        addGap(row);
        row.Label("baseViewRange".Translate() + ": " + BaseViewRange.ToString(), -1f, "baseViewRange".Translate());
        BaseViewRange = (int)row.Slider(BaseViewRange, 10f, 100);
        row.Label("baseHearingRange".Translate() + ": " + Math.Round(BaseHearingRange, 1).ToString());
        BaseHearingRange = row.Slider(BaseHearingRange, 0, 30);
        addGap(row);
        row.Label("buildingVisionMod".Translate() + ": " + Math.Round(BuildingVisionModifier, 2).ToString(), -1f,
            "buildingVisionModDesc".Translate());
        BuildingVisionModifier = row.Slider(BuildingVisionModifier, 0.2f, 2);
        row.Label("animalVisionModDesc".Translate() + ": " + Math.Round(AnimalVisionModifier, 2).ToString());
        AnimalVisionModifier = row.Slider(AnimalVisionModifier, 0.2f, 2);
        row.Label("turretVisionModDesc".Translate() + ": " + Math.Round(TurretVisionModifier, 2).ToString());
        TurretVisionModifier = row.Slider(TurretVisionModifier, 0.2f, 2);
        addGap(row);
        row.CheckboxLabeled("allyGiveVision".Translate(), ref AllyGiveVision, "allyGiveVision".Translate());
        row.CheckboxLabeled("prisonerGiveVision".Translate(), ref PrisonerGiveVision, "prisonerGiveVision".Translate());
        row.CheckboxLabeled("mapRevealAtStart".Translate(), ref MapRevealAtStart, "mapRevealAtStart".Translate());

        row.CheckboxLabeled("wildLifeTabVisible".Translate(), ref WildLifeTabVisible,
            "wildLifeTabVisibleDesc".Translate());
        row.CheckboxLabeled("NeedWatcher".Translate(), ref NeedWatcher, "NeedWatcherDesc".Translate());

        addGap(row);
        row.CheckboxLabeled("hideEventNegative".Translate(), ref HideEventNegative, "hideEventNegative".Translate());
        row.CheckboxLabeled("hideEventNeutral".Translate(), ref HideEventNeutral, "hideEventNeutral".Translate());
        row.CheckboxLabeled("hideEventPositive".Translate(), ref HideEventPositive, "hideEventPositive".Translate());
        row.CheckboxLabeled("hideThreatBig".Translate(), ref HideThreatBig, "hideThreatBig".Translate());
        row.CheckboxLabeled("hideThreatSmall".Translate(), ref HideThreatSmall, "hideThreatSmall".Translate());
        addGap(row);
        row.CheckboxLabeled("hideSpeakBubble".Translate(), ref HideSpeakBubble, "hideSpeakBubbleDesc".Translate());
        row.CheckboxLabeled("aiSmart".Translate(), ref AISmart, "aiSmartDesc".Translate());
        row.CheckboxLabeled("onlyOutsideColony".Translate(), ref OnlyOutsideColony,
            "onlyOutsideColonyDesc".Translate());

        addGap(row);
        row.CheckboxLabeled("doVolumeCheck".Translate(), ref DoAudioCheck, "doVolumeCheck".Translate());

        if (DoAudioCheck)
        {
            row.Label("audioSourceRange".Translate() + ": " + AudioSourceRange.ToString(), -1f,
                "audioSourceRangeDesc".Translate());
            row.IntEntry(ref AudioSourceRange, ref audioSourceRangeBuffer);
            row.Label("volumeMufflingModifier".Translate() + ": " + Math.Round(VolumeMufflingModifier, 1).ToString(),
                -1f, "volumeMufflingModifierDesc".Translate());
            VolumeMufflingModifier = row.Slider(VolumeMufflingModifier, 0f, 1f);
        }

        addGap(row);
        row.CheckboxLabeled("delayAlertsUntilSeen".Translate(), ref DelayAlertsUntilSeen,
            "delayAlertsUntilSeenDesc".Translate());
        if (ModLister.OdysseyInstalled)
        {
            row.CheckboxLabeled("clearFogDuringTargeting".Translate(), ref ClearFogDuringTargeting,
                "clearFogDuringTargetingDesc".Translate());
        }

        // Only allow changing trees block sight setting when there's no active game
        var isGameActive = Current.ProgramState == ProgramState.Playing;

        if (isGameActive)
        {
            GUI.enabled = false;
            row.CheckboxLabeled("treesBlockSight".Translate(), ref treesBlockSightValue,
                "treesBlockSightDesc".Translate());
            GUI.enabled = true;

            Text.Font = GameFont.Tiny;
            row.Label("treesBlockSight_RequiresReload".Translate());
            Text.Font = GameFont.Small;
        }
        else
        {
            row.CheckboxLabeled("treesBlockSight".Translate(), ref treesBlockSightValue,
                "treesBlockSightDesc".Translate());
        }

        if (row.ButtonText("RFWreset".Translate(), widthPct: 0.5f))
        {
            // reset all settings
            fogFadeSpeed = FogFadeSpeedEnum.Medium;
            fogAlpha = FogAlpha.Medium;
            BaseViewRange = 60;
            BaseHearingRange = 10;
            BuildingVisionModifier = 1;
            TurretVisionModifier = 0.7f;
            AnimalVisionModifier = 0.5f;
            HideSpeakBubble = false;
            AISmart = false;
            NeedWatcher = true;
            HideThreatBig = false;
            HideThreatSmall = false;
            HideEventPositive = false;
            HideEventNegative = false;
            HideEventNeutral = false;
            PrisonerGiveVision = false;
            AllyGiveVision = false;
            MapRevealAtStart = false;
            WildLifeTabVisible = true;
            needMemoryStorage = true;
            DoAudioCheck = false;
            AudioSourceRange = 30;
            VolumeMufflingModifier = 0.5f;
            DelayAlertsUntilSeen = false;
            ClearFogDuringTargeting = true;
            TreesBlockSight = false;
            applySettings();
        }

        if (CurrentVersion != null)
        {
            row.Gap();
            GUI.contentColor = Color.gray;
            row.Label("CurrentModVersion".Translate(CurrentVersion));
            GUI.contentColor = Color.white;
        }

        row.End();
        Widgets.EndScrollView();
    }

    private static void addGap(Listing_Standard listingStandard, float value = 12f)
    {
        listingStandard.Gap(value);
        listingStandard.GapLine(value);
    }

    private static void applySettings()
    {
        SectionLayerFoVLayer.PrefFadeSpeedMult = (int)fogFadeSpeed;
        SectionLayerFoVLayer.PrefEnableFade = fogFadeSpeed != FogFadeSpeedEnum.Disabled;
        SectionLayerFoVLayer.PrefFogAlpha = (byte)fogAlpha;
        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        foreach (var map in Find.Maps)
        {
            map.mapDrawer?.RegenerateEverythingNow();
        }
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
        Scribe_Values.Look(ref DoAudioCheck, "doAudioCheck");
        Scribe_Values.Look(ref AudioSourceRange, "audioSourceRange", 30);
        Scribe_Values.Look(ref VolumeMufflingModifier, "volumeMufflingModifier", 0.5f);
        Scribe_Values.Look(ref DelayAlertsUntilSeen, "delayAlertsUntilSeen");
        Scribe_Values.Look(ref ClearFogDuringTargeting, "clearFogDuringTargeting", true);
        Scribe_Values.Look(ref treesBlockSightValue, "treesBlockSight");

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
        Light = 60
    }

    private enum FogFadeSpeedEnum
    {
        Slow = 5,
        Medium = 20,
        Fast = 40,
        Disabled = 100
    }
}