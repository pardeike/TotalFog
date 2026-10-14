// Modified by Andreas Pardeike for Total Fog, 2026-10-04.
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TotalFog;

[StaticConstructorOnStartup]
public partial class TotalFogMod : Mod
{
    private static readonly Harmony harmony;

    static TotalFogMod()
    {
        harmony = new Harmony("brrainz.totalfog");
        PatchSafety.Install(
            "legacy save import",
            () => Compatibility.LegacySaveTypes.Install(harmony)
        );
        PatchSafety.Install("base fog hooks", InjectDetours);
        PatchSafety.Install("debug fog commands", () => DebugFogCommands.Install(harmony));
        PatchSafety.Install("watchtowers", () => Watchtowers.WatchtowerPatches.Install(harmony));
    }

    public TotalFogMod(ModContentPack content)
        : base(content)
    {
        FogSettings.CurrentVersion = typeof(TotalFogMod).Assembly.GetName().Version.ToString(3);

        LongEventHandler.ExecuteWhenFinished(() =>
            PatchSafety.Install("minimap", () => Compatibility.MinimapIntegration.Install(harmony))
        );
        LongEventHandler.ExecuteWhenFinished(() =>
            PatchSafety.Install(
                "interaction bubbles",
                () => Compatibility.BubbleIntegration.Install(harmony)
            )
        );
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            PatchSafety.Install(
                "Combat Extended targeting",
                () => Compatibility.CombatExtendedIntegration.Install(harmony)
            );
            PatchSafety.Install(
                "Combat Extended towers",
                () => Compatibility.CombatExtendedTowers.Install(harmony)
            );
        });
        LongEventHandler.ExecuteWhenFinished(() =>
            PatchSafety.Install("Multiplayer", Compatibility.MultiplayerIntegration.Install)
        );
        GetSettings<FogSettings>();
    }

    public static void LogMessage(string message)
    {
        if (!Prefs.DevMode)
        {
            return;
        }

        Log.Message($"[Total Fog] {message}");
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }

    public override void DoSettingsWindowContents(Rect rect)
    {
        FogSettings.DoSettingsWindowContents(rect);
    }

    public static void InitializeCompsPrefix(ThingWithComps __instance)
    {
        var def = __instance.def;
        if (
            def.category
                is not (
                    ThingCategory.Pawn
                    or ThingCategory.Building
                    or ThingCategory.Item
                    or ThingCategory.Filth
                    or ThingCategory.Gas
                    or ThingCategory.Plant
                )
            && !def.IsBlueprint
        )
            return;
        def.comps ??= new List<CompProperties>();
        if (!def.comps.Any(c => c.compClass == typeof(CompFog)))
            def.comps.Insert(0, CompFog.CompDef);
    }
}
