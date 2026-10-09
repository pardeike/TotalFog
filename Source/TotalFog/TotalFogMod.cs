// Modified by Andreas Pardeike for Total Fog, 2026-10-04.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using TotalFog.Detours;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using static TotalFog.HarmonyPatches;
using BeautyUtility = TotalFog.Detours.BeautyUtility;
using Designation = TotalFog.Detours.Designation;
using EnvironmentStatsDrawer = TotalFog.Detours.EnvironmentStatsDrawer;
using GenMapUI = TotalFog.Detours.GenMapUI;
using GenView = TotalFog.Detours.GenView;
using HaulAIUtility = TotalFog.Detours.HaulAIUtility;
using MoteBubble = TotalFog.Detours.MoteBubble;
using MouseoverReadout = TotalFog.Detours.MouseoverReadout;
using Pawn = TotalFog.Detours.Pawn;
using ReservationUtility = TotalFog.Detours.ReservationUtility;
using Selector = TotalFog.Detours.Selector;
using Verb = TotalFog.Detours.Verb;

namespace TotalFog;

[StaticConstructorOnStartup]
public class TotalFogMod : Mod
{
    private static readonly Harmony harmony;

    static TotalFogMod()
    {
        harmony = new Harmony("brrainz.totalfog");
        Compatibility.LegacySaveTypes.Install(harmony);
        injectDetours();
    }

    public TotalFogMod(ModContentPack content)
        : base(content)
    {
        FogSettings.CurrentVersion = typeof(TotalFogMod).Assembly.GetName().Version.ToString(3);

        LongEventHandler.ExecuteWhenFinished(() =>
            Compatibility.MinimapIntegration.Install(harmony)
        );
        LongEventHandler.ExecuteWhenFinished(() =>
            Compatibility.BubbleIntegration.Install(harmony)
        );
        LongEventHandler.ExecuteWhenFinished(() =>
            Compatibility.CombatExtendedIntegration.Install(harmony)
        );
        LongEventHandler.ExecuteWhenFinished(Compatibility.MultiplayerIntegration.Install);
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

    private static void injectDetours()
    {
        harmony.Patch(
            AccessTools.Method(typeof(SectionLayer_LightingOverlay), "GenerateLightingOverlay"),
            transpiler: new HarmonyMethod(
                typeof(Presentation.LightingVisibility),
                nameof(Presentation.LightingVisibility.Transpiler)
            )
        );
        harmony.Patch(
            AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.InitializeComps)),
            prefix: new HarmonyMethod(typeof(TotalFogMod), nameof(InitializeCompsPrefix))
        );
        patchMethod(typeof(Verse.Verb), typeof(Verb), "CanHitCellFromCellIgnoringRange");
        // Selection proxies recurse directly into SelectInternal. Guard the
        // actual target as well as the initial public Select request.
        patchMethod(typeof(RimWorld.Selector), typeof(Selector), "SelectInternal");
        patchMethod(
            typeof(Verse.MouseoverReadout),
            typeof(MouseoverReadout),
            nameof(Verse.MouseoverReadout.MouseoverReadoutOnGUI)
        );
        patchMethod(
            typeof(RimWorld.BeautyUtility),
            typeof(BeautyUtility),
            nameof(RimWorld.BeautyUtility.FillBeautyRelevantCells)
        );
        patchMethod(typeof(MainTabWindow_Wildlife), typeof(MainTabWindowWildlife), "get_Pawns");

        patchMethod(typeof(Verse.Pawn), typeof(Pawn), nameof(Verse.Pawn.DrawGUIOverlay));
        patchMethod(
            typeof(DynamicDrawManager),
            typeof(Presentation.DynamicVisibility),
            "ComputeCulledThings"
        );
        harmony.Patch(
            AccessTools.DeclaredMethod(typeof(FleckManager), nameof(FleckManager.FleckManagerDraw)),
            prefix: new HarmonyMethod(
                typeof(Presentation.FleckVisibility),
                nameof(Presentation.FleckVisibility.BeginDrawing)
            ),
            finalizer: new HarmonyMethod(
                typeof(Presentation.FleckVisibility),
                nameof(Presentation.FleckVisibility.EndDrawing)
            )
        );
        harmony.Patch(
            AccessTools.DeclaredMethod(
                typeof(FleckStatic),
                nameof(FleckStatic.Draw),
                new[] { typeof(float), typeof(DrawBatch) }
            ),
            prefix: new HarmonyMethod(
                typeof(Presentation.FleckVisibility),
                nameof(Presentation.FleckVisibility.DrawPrefix)
            )
        );
        harmony.Patch(
            AccessTools.Method(typeof(DamageWorker), nameof(DamageWorker.ExplosionStart)),
            transpiler: new HarmonyMethod(
                typeof(Presentation.ExplosionVisibility),
                nameof(Presentation.ExplosionVisibility.Transpiler)
            )
        );
        harmony.Patch(
            AccessTools.Method(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI)),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.OverlayTranspiler)
            )
        );
        harmony.Patch(
            AccessTools.Method(
                typeof(TooltipGiverList),
                nameof(TooltipGiverList.DispenseAllThingTooltips)
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.TooltipTranspiler)
            )
        );
        harmony.Patch(
            AccessTools.Method(
                typeof(Verse.MouseoverReadout),
                nameof(Verse.MouseoverReadout.MouseoverReadoutOnGUI)
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.MouseoverTranspiler)
            )
        );
        harmony.Patch(
            AccessTools.EnumeratorMoveNext(
                AccessTools.Method(typeof(Verse.GenUI), nameof(Verse.GenUI.TargetsAt))
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.TargetCandidatesTranspiler)
            )
        );
        harmony.Patch(
            AccessTools.Constructor(
                typeof(FloatMenuContext),
                [typeof(List<Verse.Pawn>), typeof(Vector3), typeof(Map)]
            ),
            postfix: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.ContextMenuPostfix)
            )
        );
        foreach (
            var method in typeof(Verse.GenMapUI)
                .GetMethods()
                .Where(m => m.Name == nameof(Verse.GenMapUI.DrawPawnLabel))
        )
            harmony.Patch(
                method,
                prefix: new HarmonyMethod(
                    typeof(Presentation.InterfaceVisibility),
                    nameof(Presentation.InterfaceVisibility.PawnLabelPrefix)
                )
            );
        patchMethod(
            typeof(Verse.GenMapUI),
            typeof(GenMapUI),
            nameof(Verse.GenMapUI.DrawThingLabel),
            typeof(Thing),
            typeof(string),
            typeof(Color)
        );

        patchMethod(
            typeof(SectionLayer_ThingsGeneral),
            typeof(SectionLayerThingsGeneral),
            "TakePrintFrom"
        );
        patchMethod(
            typeof(SectionLayer_ThingsPowerGrid),
            typeof(SectionLayerThingsPowerGrid),
            "TakePrintFrom"
        );
        patchMethod(
            typeof(Verse.AI.ReservationUtility),
            typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserve)
        );
        patchMethod(
            typeof(Verse.AI.ReservationUtility),
            typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserveAndReach)
        );
        patchMethod(
            typeof(Verse.AI.HaulAIUtility),
            typeof(HaulAIUtility),
            nameof(Verse.AI.HaulAIUtility.HaulToStorageJob)
        );
        patchMethod(
            typeof(Verse.EnvironmentStatsDrawer),
            typeof(EnvironmentStatsDrawer),
            "ShouldShowWindowNow"
        );

        harmony.Patch(
            AccessTools.Method(
                typeof(Verse.Messages),
                nameof(Verse.Messages.Message),
                new[] { typeof(Message), typeof(bool) }
            ),
            prefix: new HarmonyMethod(
                typeof(Notifications.NotificationHooks),
                nameof(Notifications.NotificationHooks.MessagePrefix)
            )
        );

        harmony.Patch(
            AccessTools.Method(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute)),
            prefix: new HarmonyMethod(
                typeof(Notifications.SilentRaidPolicy),
                nameof(Notifications.SilentRaidPolicy.Prefix)
            ),
            finalizer: new HarmonyMethod(
                typeof(Notifications.SilentRaidPolicy),
                nameof(Notifications.SilentRaidPolicy.Finalizer)
            )
        );
        harmony.Patch(
            AccessTools.Method(typeof(IncidentWorker_AggressiveAnimals), "TryExecuteWorker"),
            transpiler: new HarmonyMethod(
                typeof(HarmonyPatches),
                nameof(ManhunterArrivalTranspiler)
            )
        );

        patchMethod(typeof(RimWorld.MoteBubble), typeof(MoteBubble), "DrawAt");
        patchMethod(
            typeof(Verse.GenView),
            typeof(GenView),
            nameof(Verse.GenView.ShouldSpawnMotesAt),
            typeof(IntVec3),
            typeof(Map),
            typeof(bool)
        );

        foreach (
            var type in new[]
            {
                typeof(RimWorld.FertilityGrid),
                typeof(Verse.TerrainGrid),
                typeof(Verse.RoofGrid),
            }
        )
            harmony.Patch(
                AccessTools.DeclaredMethod(
                    type,
                    type == typeof(Verse.RoofGrid)
                        ? nameof(Verse.RoofGrid.GetCellBool)
                        : "CellBoolDrawerGetBoolInt",
                    new[] { typeof(int) }
                ),
                postfix: new HarmonyMethod(
                    typeof(Presentation.DiscoveryOverlays),
                    nameof(Presentation.DiscoveryOverlays.OverlayPostfix)
                )
            );

        //Area only designator
        patchMethod(
            typeof(Designator_AreaBuildRoof),
            typeof(DesignatorPrefix),
            nameof(Designator_AreaBuildRoof.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_AreaNoRoof),
            typeof(DesignatorPrefix),
            nameof(Designator_AreaNoRoof.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_ZoneAdd_Growing),
            typeof(DesignatorPrefix),
            nameof(Designator_ZoneAdd_Growing.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_ZoneAddStockpile),
            typeof(DesignatorPrefix),
            nameof(Designator_ZoneAddStockpile.CanDesignateCell)
        );

        //Area+Designator
        patchMethod(
            typeof(Designator_Claim),
            typeof(DesignatorPrefix),
            nameof(Designator_Claim.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Claim),
            typeof(DesignatorPrefix),
            nameof(Designator_Claim.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Deconstruct),
            typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Deconstruct),
            typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Haul),
            typeof(DesignatorPrefix),
            nameof(Designator_Haul.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Haul),
            typeof(DesignatorPrefix),
            nameof(Designator_Haul.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Hunt),
            typeof(DesignatorPrefix),
            nameof(Designator_Hunt.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Hunt),
            typeof(DesignatorPrefix),
            nameof(Designator_Hunt.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Plants),
            typeof(DesignatorPrefix),
            nameof(Designator_Plants.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Plants),
            typeof(DesignatorPrefix),
            nameof(Designator_Plants.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_PlantsHarvest),
            typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvest.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_PlantsHarvestWood),
            typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvestWood.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_RemoveFloor),
            typeof(DesignatorPrefix),
            nameof(Designator_RemoveFloor.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_SmoothSurface),
            typeof(DesignatorPrefix),
            nameof(Designator_SmoothSurface.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Tame),
            typeof(DesignatorPrefix),
            nameof(Designator_Tame.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Tame),
            typeof(DesignatorPrefix),
            nameof(Designator_Tame.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Uninstall),
            typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateCell)
        );

        //PLacing designator
        patchMethod(
            typeof(Designator_Uninstall),
            typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateThing)
        );
        patchMethod(
            typeof(Designator_Build),
            typeof(DesignatorPlace),
            nameof(Designator_Build.CanDesignateCell)
        );
        patchMethod(
            typeof(Designator_Install),
            typeof(DesignatorPlace),
            nameof(Designator_Install.CanDesignateCell)
        );

        foreach (var type in new[] { typeof(Designator_Forbid), typeof(Designator_Unforbid) })
            patchMethod(type, typeof(DoorOrders), nameof(Designator_Forbid.CanDesignateThing));

        //Specific designation
        patchMethod(
            typeof(Designator_Mine),
            typeof(DesignatorMine),
            nameof(Designator_Mine.CanDesignateCell)
        );

        //Designation
        patchMethod(
            typeof(Verse.Designation),
            typeof(Designation),
            nameof(Verse.Designation.Notify_Added)
        );
        patchMethod(typeof(Verse.Designation), typeof(Designation), "Notify_Removing");

        /* Filth checks
        patchMethod(typeof(Thing), typeof(Patch_Filth_Draw), nameof(Filth.DrawNowAt));
        patchMethod(typeof(Filth), typeof(Patch_Filth_Destroy), nameof(Filth.Destroy));
        */

        harmony.Patch(
            typeof(AttackTargetFinder).GetMethod(nameof(AttackTargetFinder.CanSee)),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(CanSeePreFix)))
        );

        LogMessage("Prefixed method AttackTargetFinder_CanSee.");
        harmony.Patch(
            typeof(Verse.LetterStack).GetMethod(
                nameof(Verse.LetterStack.ReceiveLetter),
                [typeof(Letter), typeof(string), typeof(int), typeof(bool)]
            ),
            new HarmonyMethod(
                typeof(Notifications.NotificationHooks),
                nameof(Notifications.NotificationHooks.LetterPrefix)
            )
        );

        LogMessage("Prefixed method LetterStack_ReceiveLetter.");

        harmony.Patch(
            typeof(OverlayDrawer).GetMethod(
                nameof(OverlayDrawer.DrawOverlay),
                [typeof(Thing), typeof(OverlayTypes)]
            ),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(DrawOverlayPrefix)))
        );

        harmony.Patch(
            AccessTools.PropertyGetter(typeof(SampleSustainer), "Volume"),
            postfix: new HarmonyMethod(
                typeof(Audio.SustainerVisibility),
                nameof(Audio.SustainerVisibility.VolumePostfix)
            )
        );

        LogMessage("Prefixed method OverlayDrawer_DrawOverlay.");

        harmony.Patch(
            typeof(SilhouetteUtility).GetMethod(
                nameof(SilhouetteUtility.ShouldDrawSilhouette),
                [typeof(Thing)]
            ),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(ShouldDrawSilhouettePrefix)))
        );

        LogMessage("Prefixed method SilhouetteUtility_ShouldDrawSilhouette.");

        harmony.Patch(
            typeof(SoundStarter).GetMethod(
                nameof(SoundStarter.PlayOneShot),
                [typeof(SoundDef), typeof(SoundInfo)]
            ),
            new HarmonyMethod(typeof(Patch_PlayOneShot).GetMethod(nameof(Patch_PlayOneShot.Prefix)))
        );

        LogMessage("Prefixed method SoundStarter_PlayOneShot.");
        harmony.Patch(
            AccessTools.PropertyGetter(
                typeof(MusicManagerPlay),
                nameof(MusicManagerPlay.DangerMusicMode)
            ),
            prefix: new HarmonyMethod(
                typeof(Audio.MusicVisibility),
                nameof(Audio.MusicVisibility.DangerModePrefix)
            )
        );

        Compatibility.GravshipVisibility.Install(harmony);
    }

    private static void patchMethod(
        Type sourceType,
        Type targetType,
        string methodName,
        params Type[] types
    )
    {
        var original =
            types.Length == 0
                ? AccessTools.DeclaredMethod(sourceType, methodName)
                : AccessTools.DeclaredMethod(sourceType, methodName, types);
        var prefix = AccessTools.DeclaredMethod(targetType, methodName + "_Prefix");
        var postfix = AccessTools.DeclaredMethod(targetType, methodName + "_Postfix");
        if (original == null || prefix == null && postfix == null)
            throw new InvalidOperationException(
                $"Total Fog: missing required patch {sourceType.FullName}.{methodName} using {targetType.FullName}."
            );
        harmony.Patch(
            original,
            prefix == null ? null : new HarmonyMethod(prefix),
            postfix == null ? null : new HarmonyMethod(postfix)
        );
    }
}
