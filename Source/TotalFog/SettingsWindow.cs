using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TotalFog;

public partial class FogSettings
{
    private enum SettingsPage
    {
        Appearance,
        Vision,
        Information,
        Audio,
    }

    private static SettingsPage settingsPage;
    private static Vector2 scrollPosition;
    private static readonly float[] contentHeights = { 1000, 1000, 1000, 1000 };

    public static void DoSettingsWindowContents(Rect rect)
    {
        var font = Text.Font;
        var anchor = Text.Anchor;
        var color = GUI.color;
        var contentColor = GUI.contentColor;
        bool enabled = GUI.enabled;
        bool watching = Compatibility.MultiplayerIntegration.BeginSettingsWatch();
        try
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            rect = rect.ContractedBy(8f);
            var panel = new Rect(rect.x, rect.y + 34f, rect.width, rect.height - 88f);
            Widgets.DrawMenuSection(panel);
            var tabs = new List<TabRecord>();
            foreach (SettingsPage page in Enum.GetValues(typeof(SettingsPage)))
            {
                var target = page;
                tabs.Add(
                    new TabRecord(
                        ("TotalFog_Page" + page).Translate(),
                        () =>
                        {
                            settingsPage = target;
                            scrollPosition = Vector2.zero;
                        },
                        settingsPage == page
                    )
                );
            }
            TabDrawer.DrawTabs(panel, tabs);
            var viewport = panel.ContractedBy(14f);
            // Reserve the scrollbar plus a clear gap between it and the controls.
            var view = new Rect(
                0,
                0,
                viewport.width - 44f,
                Math.Max(viewport.height, contentHeights[(int)settingsPage])
            );
            Widgets.BeginScrollView(viewport, ref scrollPosition, view);
            var listing = new Listing_Standard { ColumnWidth = view.width };
            listing.Begin(view);
            try
            {
                Description(listing, "TotalFog_Intro" + settingsPage);
                listing.Gap(12f);
                switch (settingsPage)
                {
                    case SettingsPage.Appearance:
                        Appearance(listing);
                        break;
                    case SettingsPage.Vision:
                        Vision(listing);
                        break;
                    case SettingsPage.Information:
                        Information(listing);
                        break;
                    case SettingsPage.Audio:
                        Audio(listing);
                        break;
                }
                contentHeights[(int)settingsPage] = listing.CurHeight + 12f;
            }
            finally
            {
                listing.End();
                Widgets.EndScrollView();
            }
            var footer = new Rect(rect.x, panel.yMax + 12f, rect.width, 32f);
            var reset = new Rect(
                footer.x,
                footer.y,
                Math.Min(220f, footer.width / 2f),
                footer.height
            );
            if (Widgets.ButtonText(reset, "RFWreset".Translate()))
                ResetDefaults();
            TooltipHandler.TipRegion(reset, "TotalFog_ResetDesc".Translate());
            if (CurrentVersion != null)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.contentColor = Color.gray;
                Widgets.Label(
                    new Rect(
                        reset.xMax + 12f,
                        footer.y,
                        footer.xMax - reset.xMax - 12f,
                        footer.height
                    ),
                    "CurrentModVersion".Translate(CurrentVersion)
                );
            }
        }
        finally
        {
            Compatibility.MultiplayerIntegration.EndSettingsWatch(watching);
            Text.Font = font;
            Text.Anchor = anchor;
            GUI.color = color;
            GUI.contentColor = contentColor;
            GUI.enabled = enabled;
        }
    }

    private static void Appearance(Listing_Standard row)
    {
        Choice(
            row,
            "fogAlphaSetting",
            fogAlpha,
            value =>
            {
                fogAlpha = value;
                applySettings();
            }
        );
        Choice(
            row,
            "fogFadeSpeedSetting",
            fogFadeSpeed,
            value =>
            {
                fogFadeSpeed = value;
                applySettings();
            }
        );
    }

    private static void Vision(Listing_Standard row)
    {
        Range(row, "baseViewRange", ref BaseViewRange, 10, 100, "baseViewRangeDesc");
        Range(
            row,
            "buildingVisionMod",
            ref BuildingVisionModifier,
            .2f,
            2f,
            "buildingVisionModDesc"
        );
        Range(
            row,
            "turretVisionModDesc",
            ref TurretVisionModifier,
            .2f,
            2f,
            "TotalFog_TurretVisionDesc"
        );
        Range(
            row,
            "animalVisionModDesc",
            ref AnimalVisionModifier,
            .2f,
            2f,
            "TotalFog_AnimalVisionDesc"
        );
        Section(row, "TotalFog_AdditionalObservers");
        Check(row, "allyGiveVision", ref AllyGiveVision, "TotalFog_AllyVisionDesc");
        Check(row, "prisonerGiveVision", ref PrisonerGiveVision, "TotalFog_PrisonerVisionDesc");
        Check(row, "NeedWatcher", ref NeedWatcher, "NeedWatcherDesc");
        Section(row, "TotalFog_TerrainAndMaps");
        Check(row, "mapRevealAtStart", ref MapRevealAtStart, "TotalFog_MapRevealDesc");
        bool enabled = GUI.enabled;
        bool playing = Current.ProgramState == ProgramState.Playing;
        GUI.enabled &= !playing;
        Check(row, "treesBlockSight", ref treesBlockSightValue, "treesBlockSightDesc");
        GUI.enabled = enabled;
        if (playing)
            Description(row, "treesBlockSight_RequiresReload");
    }

    private static void Information(Listing_Standard row)
    {
        Check(row, "TotalFog_SilentRaids", ref SilentRaids, "TotalFog_SilentRaidsDesc");
        Check(row, "delayAlertsUntilSeen", ref DelayAlertsUntilSeen, "delayAlertsUntilSeenDesc");
        Section(row, "TotalFog_DiscardEvents");
        Description(row, "TotalFog_DiscardEventsDesc");
        Check(row, "hideThreatBig", ref HideThreatBig);
        Check(row, "hideThreatSmall", ref HideThreatSmall);
        Check(row, "hideEventNegative", ref HideEventNegative);
        Check(row, "hideEventNeutral", ref HideEventNeutral);
        Check(row, "hideEventPositive", ref HideEventPositive);
        Section(row, "TotalFog_InformationOptions");
        Check(row, "wildLifeTabVisible", ref WildLifeTabVisible, "wildLifeTabVisibleDesc");
        Check(row, "hideSpeakBubble", ref HideSpeakBubble, "hideSpeakBubbleDesc");
        Check(row, "aiSmart", ref AISmart, "aiSmartDesc");
        Check(row, "onlyOutsideColony", ref OnlyOutsideColony, "onlyOutsideColonyDesc");
        if (ModsConfig.OdysseyActive)
            Check(
                row,
                "clearFogDuringTargeting",
                ref ClearFogDuringTargeting,
                "clearFogDuringTargetingDesc"
            );
    }

    private static void Audio(Listing_Standard row)
    {
        Check(
            row,
            "TotalFog_SuppressCombatMusic",
            ref SuppressCombatMusic,
            "TotalFog_SuppressCombatMusicDesc"
        );
        Check(
            row,
            "TotalFog_MuteHiddenSounds",
            ref MuteHiddenSounds,
            "TotalFog_MuteHiddenSoundsDesc"
        );
        Check(
            row,
            "TotalFog_FilterHiddenSounds",
            ref DoAudioCheck,
            "TotalFog_FilterHiddenSoundsDesc"
        );
        if (DoAudioCheck)
        {
            var label = "audioSourceRange".Translate();
            var rect = row.GetRect(Math.Max(30f, Text.CalcHeight(label, row.ColumnWidth - 92f)));
            Widgets.Label(new Rect(rect.x, rect.y, rect.width - 92f, rect.height), label);
            Widgets.TextFieldNumeric(
                new Rect(rect.xMax - 80f, rect.y, 80f, 30f),
                ref AudioSourceRange,
                ref audioSourceRangeBuffer,
                0,
                int.MaxValue
            );
            TooltipHandler.TipRegion(rect, "audioSourceRangeDesc".Translate());
            row.Gap(8f);
            Range(
                row,
                "volumeMufflingModifier",
                ref VolumeMufflingModifier,
                0,
                1,
                "volumeMufflingModifierDesc"
            );
        }
        Section(row, "TotalFog_HearingIndicators");
        Check(row, "TotalFog_ShowHearingCues", ref ShowHearingCues, "TotalFog_ShowHearingCuesDesc");
        if (ShowHearingCues)
        {
            ValueLabel(
                row,
                "baseHearingRange",
                Math.Round(BaseHearingRange, 1).ToString("0.#"),
                "TotalFog_HearingRangeDesc"
            );
            BaseHearingRange = row.Slider(BaseHearingRange, 0, 30);
        }
    }

    private static void Choice<T>(Listing_Standard row, string key, T selected, Action<T> choose)
        where T : struct, Enum
    {
        var label = (key + "_title").Translate();
        float buttonWidth = Math.Min(220f, row.ColumnWidth * .4f);
        var rect = row.GetRect(
            Math.Max(32f, Text.CalcHeight(label, row.ColumnWidth - buttonWidth - 16f))
        );
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - buttonWidth - 16f, rect.height), label);
        if (
            Widgets.ButtonText(
                new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, 32f),
                (key + "_" + selected).Translate()
            )
        )
        {
            var options = new List<FloatMenuOption>();
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                var target = value;
                options.Add(
                    new FloatMenuOption((key + "_" + value).Translate(), () => choose(target))
                );
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }
        row.Gap(6f);
        Description(row, key + "_desc");
        row.Gap(18f);
    }

    private static void Check(
        Listing_Standard row,
        string key,
        ref bool value,
        string description = null
    )
    {
        var label = key.Translate();
        var rect = row.GetRect(Math.Max(30f, Text.CalcHeight(label, row.ColumnWidth - 36f)));
        Widgets.CheckboxLabeled(rect, label, ref value, disabled: !GUI.enabled);
        if (description != null)
        {
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);
            TooltipHandler.TipRegion(rect, description.Translate());
        }
        row.Gap(8f);
    }

    private static void Range(
        Listing_Standard row,
        string key,
        ref int value,
        int min,
        int max,
        string description
    )
    {
        ValueLabel(row, key, value.ToString(), description);
        value = Mathf.RoundToInt(row.Slider(value, min, max));
        row.Gap(8f);
    }

    private static void Range(
        Listing_Standard row,
        string key,
        ref float value,
        float min,
        float max,
        string description
    )
    {
        ValueLabel(row, key, Math.Round(value, 2).ToString("0.##"), description);
        value = row.Slider(value, min, max);
        row.Gap(8f);
    }

    private static void ValueLabel(
        Listing_Standard row,
        string key,
        string value,
        string description
    )
    {
        var label = key.Translate();
        var rect = row.GetRect(Math.Max(30f, Text.CalcHeight(label, row.ColumnWidth - 80f)));
        Widgets.Label(new Rect(rect.x, rect.y, rect.width - 80f, rect.height), label);
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(new Rect(rect.xMax - 72f, rect.y, 72f, rect.height), value);
        Text.Anchor = TextAnchor.UpperLeft;
        TooltipHandler.TipRegion(rect, description.Translate());
    }

    private static void Description(Listing_Standard row, string key)
    {
        Text.Font = GameFont.Tiny;
        GUI.contentColor = new Color(.75f, .75f, .75f);
        row.Label(key.Translate());
        GUI.contentColor = Color.white;
        Text.Font = GameFont.Small;
    }

    private static void Section(Listing_Standard row, string key)
    {
        row.Gap(12f);
        row.GapLine(12f);
        row.Label(key.Translate());
        row.Gap(8f);
    }

    private static void ResetDefaults()
    {
        fogFadeSpeed = FogFadeSpeedEnum.Medium;
        fogAlpha = FogAlpha.Medium;
        BaseViewRange = 60;
        BaseHearingRange = 10;
        ShowHearingCues = true;
        BuildingVisionModifier = 1;
        TurretVisionModifier = .7f;
        AnimalVisionModifier = .5f;
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
        OnlyOutsideColony = false;
        DoAudioCheck = false;
        SuppressCombatMusic = true;
        MuteHiddenSounds = false;
        AudioSourceRange = 30;
        audioSourceRangeBuffer = null;
        VolumeMufflingModifier = .5f;
        DelayAlertsUntilSeen = false;
        SilentRaids = false;
        ClearFogDuringTargeting = true;
        if (Current.ProgramState != ProgramState.Playing)
            TreesBlockSight = false;
        applySettings();
    }
}
