using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class SettingsLocalizationScenarios
{
    [Tool(
        "totalfog/settings_units",
        Description = "Check all shipped unit translations with native text measurements. Capture English, German and Japanese Vision/Audio settings. Use an isolated paused game. Restore language, settings page, scroll and audio options without saving language preferences."
    )]
    public static async Task<object> Run(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        var proof = RimBridgeEvidence.CreateManifest("totalfog/settings_units");
        var rows = new List<object>();
        var oldLanguage = LanguageDatabase.activeLanguage;
        var oldFont = Text.Font;
        object oldPage = null,
            oldScroll = null;
        bool oldAudio = FogSettings.DoAudioCheck,
            oldCues = FogSettings.ShowHearingCues;
        var pageField = AccessTools.Field(typeof(FogSettings), "settingsPage");
        var scrollField = AccessTools.Field(typeof(FogSettings), "scrollPosition");
        Dialog_ModSettings window = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (
                        !Find.TickManager.Paused
                        || Find.WindowStack.Windows.OfType<Dialog_ModSettings>().Any()
                    )
                        throw new InvalidOperationException(
                            "Use a paused isolated game with no mod settings dialog open."
                        );
                    oldPage = pageField.GetValue(null);
                    oldScroll = scrollField.GetValue(null);
                    Text.Font = GameFont.Small;
                    var mod = LoadedModManager.GetMod<TotalFogMod>();
                    var folders = Directory.GetDirectories(
                        Path.Combine(mod.Content.RootDir, "Languages")
                    );
                    Check("all 14 shipped languages are checked", folders.Length == 14);
                    foreach (var folder in folders.OrderBy(p => p))
                    {
                        var language = Language(Path.GetFileName(folder));
                        language.LoadData();
                        LanguageDatabase.activeLanguage = language;
                        var values = new List<object>();
                        foreach (
                            var format in new[]
                            {
                                "TotalFog_ValueMultiplier",
                                "TotalFog_ValuePercent",
                                "TotalFog_ValueSeconds",
                            }
                        )
                        {
                            Check(
                                language.LegacyFolderName + " has " + format,
                                language.HaveTextForKey(format, false)
                            );
                            foreach (
                                float number in format == "TotalFog_ValuePercent"
                                    ? new[] { 0f, 50f, 99.99f, 100f }
                                    : new[] { .2f, 1f, 1.5f, 4.98f, 5f }
                            )
                            {
                                string text = FogSettings.UnitValue(format, number);
                                float width = Text.CalcSize(text).x;
                                Check(
                                    language.LegacyFolderName + " value fits: " + text,
                                    width > 0 && width <= 72f
                                );
                                values.Add(
                                    new
                                    {
                                        format,
                                        number,
                                        text,
                                        width,
                                    }
                                );
                            }
                        }
                        foreach (
                            var key in new[]
                            {
                                "baseViewRange",
                                "TotalFog_TowerRange",
                                "TotalFog_TelescopeRange",
                                "audioSourceRange",
                                "baseHearingRange",
                                "TotalFog_TowerClimb",
                            }
                        )
                        {
                            Check(
                                language.LegacyFolderName + " has " + key,
                                language.HaveTextForKey(key, false)
                            );
                            string label = key.Translate();
                            float height = Text.CalcHeight(label, 320f);
                            Check(
                                language.LegacyFolderName + " label wraps within two lines: " + key,
                                height > 0 && height <= Text.LineHeight * 2 + 2
                            );
                        }
                        rows.Add(new { language = language.LegacyFolderName, values });
                    }
                    proof.environment.gameVersion = VersionControl.CurrentVersionStringWithRev;
                    proof.environment.details = new
                    {
                        labelWidth = 320,
                        valueWidth = 72,
                        rows,
                    };
                },
                cancellationToken
            );
            foreach (var languageName in new[] { "English", "German", "Japanese" })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        LanguageDatabase.activeLanguage = Language(languageName);
                        FogSettings.DoAudioCheck = FogSettings.ShowHearingCues = true;
                    },
                    cancellationToken
                );
                foreach (var page in new[] { "Vision", "VisionBottom", "Audio" })
                {
                    await ctx.MainThread.InvokeAsync(
                        () =>
                        {
                            window?.Close(false);
                            pageField.SetValue(
                                null,
                                Enum.ToObject(pageField.FieldType, page == "Audio" ? 3 : 1)
                            );
                            scrollField.SetValue(
                                null,
                                page == "VisionBottom" ? new Vector2(0, 400) : Vector2.zero
                            );
                            window = new Dialog_ModSettings(LoadedModManager.GetMod<TotalFogMod>());
                            Find.WindowStack.Add(window);
                        },
                        cancellationToken
                    );
                    await ctx.Game.FramesAsync(3, cancellationToken);
                    var shot = await ctx.Tools.CallAsync(
                        "rimworld/take_screenshot",
                        new
                        {
                            fileName = "settings-units-" + languageName + "-" + page,
                            includeTargets = false,
                            suppressMessage = true,
                        },
                        cancellationToken: cancellationToken
                    );
                    Check(languageName + " " + page + " screenshot", shot.Succeeded());
                    proof.captures.Add(
                        new RimBridgeEvidenceCapture
                        {
                            label = languageName + " " + page,
                            path = shot.TryReadResult<string>(out var path, "path") ? path : null,
                            success = shot.Succeeded(),
                        }
                    );
                }
            }
        }
        catch (Exception error)
        {
            proof.errors.Add(new RimBridgeEvidenceError { message = error.ToString() });
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    FogSettings.DoAudioCheck = oldAudio;
                    FogSettings.ShowHearingCues = oldCues;
                    window?.Close(false);
                    LanguageDatabase.activeLanguage = oldLanguage;
                    Text.Font = oldFont;
                    if (oldPage != null)
                        pageField.SetValue(null, oldPage);
                    if (oldScroll != null)
                        scrollField.SetValue(null, oldScroll);
                },
                CancellationToken.None
            );
        }
        RimBridgeEvidence.Complete(proof);
        return proof;
        void Check(string name, bool condition) =>
            proof.assertions.Add(RimBridgeEvidence.IsTrue(name, condition));
    }

    private static LoadedLanguage Language(string folder) =>
        LanguageDatabase.AllLoadedLanguages.Single(l =>
            l.LegacyFolderName == folder || l.folderName == folder
        );
}
