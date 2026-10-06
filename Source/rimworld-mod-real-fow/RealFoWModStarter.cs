using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mlie;
using RimWorld;
using RimWorldRealFoW.Detours;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using static RimWorldRealFoW.HarmonyPatches;
using BeautyUtility = RimWorldRealFoW.Detours.BeautyUtility;
using Designation = RimWorldRealFoW.Detours.Designation;
using EnvironmentStatsDrawer = RimWorldRealFoW.Detours.EnvironmentStatsDrawer;
using FertilityGrid = RimWorldRealFoW.Detours.FertilityGrid;
using GenMapUI = RimWorldRealFoW.Detours.GenMapUI;
using GenView = RimWorldRealFoW.Detours.GenView;
using HaulAIUtility = RimWorldRealFoW.Detours.HaulAIUtility;
using LetterStack = RimWorldRealFoW.Detours.LetterStack;
using Messages = RimWorldRealFoW.Detours.Messages;
using MoteBubble = RimWorldRealFoW.Detours.MoteBubble;
using MouseoverReadout = RimWorldRealFoW.Detours.MouseoverReadout;
using Pawn = RimWorldRealFoW.Detours.Pawn;
using ReservationUtility = RimWorldRealFoW.Detours.ReservationUtility;
using RoofGrid = RimWorldRealFoW.Detours.RoofGrid;
using Selector = RimWorldRealFoW.Detours.Selector;
using TerrainGrid = RimWorldRealFoW.Detours.TerrainGrid;
using Verb = RimWorldRealFoW.Detours.Verb;

namespace RimWorldRealFoW;

[StaticConstructorOnStartup]
public class RealFoWModStarter : Mod
{
    private static readonly Harmony harmony;
    public static bool CanBeServant;
    public static bool DubsMintMinimapLoaded;
    public static FieldInfo DubsMintMinimap_MainTabWindow_MiniMap_dirtyGirls;

    static RealFoWModStarter()
    {
        harmony = new Harmony("com.github.lukakama.rimworldmodrealfow");
        injectDetours();
        harmony = null;
    }

    public RealFoWModStarter(ModContentPack content) : base(content)
    {
        RfowSettings.CurrentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);
        LongEventHandler.QueueLongEvent(InjectComponents, "Real Fog of War - Init.", false, null);
        LongEventHandler.ExecuteWhenFinished(InjectDubsMinimapPatch);
        GetSettings<RfowSettings>();
        CanBeServant = ModLister.GetActiveModWithIdentifier("ThatThing.Mycohazard", true) != null;
        DubsMintMinimapLoaded = ModLister.GetActiveModWithIdentifier("dubwise.dubsmintminimap", true) != null;
        if (!DubsMintMinimapLoaded)
        {
            return;
        }

        DubsMintMinimap_MainTabWindow_MiniMap_dirtyGirls =
            AccessTools.Field(AccessTools.TypeByName("DubsMintMinimap.MainTabWindow_MiniMap"), "dirtyGirls");
        if (DubsMintMinimap_MainTabWindow_MiniMap_dirtyGirls != null)
        {
            return;
        }

        LogMessage("Failed to find field 'dirtyGirls' in DubsMintMinimap.MainTabWindow_MiniMap");
        DubsMintMinimapLoaded = false;
    }

    public static void LogMessage(string message)
    {
        if (!Prefs.DevMode)
        {
            return;
        }

        Log.Message($"[Real Fog of War] {message}");
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }

    public override void DoSettingsWindowContents(Rect rect)
    {
        RfowSettings.DoSettingsWindowContents(rect);
    }

    private static void InjectComponents()
    {
        foreach (var thingDef in DefDatabase<ThingDef>.AllDefs)
        {
            var category = thingDef.category;

            if (typeof(ThingWithComps).IsAssignableFrom(thingDef.thingClass)
                && (category == ThingCategory.Pawn
                    || category == ThingCategory.Building
                    || category == ThingCategory.Item
                    || category == ThingCategory.Filth
                    || category == ThingCategory.Gas
                    || category == ThingCategory.Plant
                    //|| category == ThingCategory.Projectile
                    || thingDef.IsBlueprint
                ))
            {
                AddComponentAsFirst(thingDef, CompMainComponent.CompDef);
            }
        }
    }

    private static void AddComponentAsFirst(ThingDef def, CompProperties compProperties)
    {
        if (!def.comps.Contains(compProperties))
        {
            def.comps.Insert(0, compProperties);
        }
    }

    private static void injectDetours()
    {
        patchMethod(typeof(Verse.Verb), typeof(Verb), "CanHitCellFromCellIgnoringRange");
        patchMethod(typeof(RimWorld.Selector), typeof(Selector), nameof(RimWorld.Selector.Select));
        patchMethod(typeof(Verse.MouseoverReadout), typeof(MouseoverReadout),
            nameof(Verse.MouseoverReadout.MouseoverReadoutOnGUI));
        patchMethod(typeof(RimWorld.BeautyUtility), typeof(BeautyUtility),
            nameof(RimWorld.BeautyUtility.FillBeautyRelevantCells));
        patchMethod(typeof(MainTabWindow_Wildlife), typeof(MainTabWindowWildlife), "get_Pawns");

        patchMethod(typeof(Verse.Pawn), typeof(Pawn), nameof(Verse.Pawn.DrawGUIOverlay));
        patchMethod(typeof(Verse.GenMapUI), typeof(GenMapUI), nameof(Verse.GenMapUI.DrawThingLabel), typeof(Thing),
            typeof(string),
            typeof(Color));

        patchMethod(typeof(SectionLayer_ThingsGeneral), typeof(SectionLayerThingsGeneral), "TakePrintFrom");
        patchMethod(typeof(SectionLayer_ThingsPowerGrid), typeof(SectionLayerThingsPowerGrid), "TakePrintFrom");
        patchMethod(typeof(Verse.AI.ReservationUtility), typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserve));
        patchMethod(typeof(Verse.AI.ReservationUtility), typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserveAndReach));
        patchMethod(typeof(Verse.AI.HaulAIUtility), typeof(HaulAIUtility),
            nameof(Verse.AI.HaulAIUtility.HaulToStorageJob));
        patchMethod(typeof(Verse.EnvironmentStatsDrawer), typeof(EnvironmentStatsDrawer), "ShouldShowWindowNow");

        patchMethod(typeof(Verse.Messages), typeof(Messages), nameof(Verse.Messages.Message), typeof(string),
            typeof(LookTargets),
            typeof(MessageTypeDef), typeof(bool));
        patchMethod(typeof(Verse.LetterStack), typeof(LetterStack), nameof(Verse.LetterStack.ReceiveLetter),
            typeof(TaggedString),
            typeof(TaggedString), typeof(LetterDef), typeof(LookTargets), typeof(Faction), typeof(Quest),
            typeof(List<ThingDef>), typeof(string), typeof(int), typeof(bool));

        patchMethod(typeof(RimWorld.MoteBubble), typeof(MoteBubble), "DrawAt");
        patchMethod(typeof(Verse.GenView), typeof(GenView), nameof(Verse.GenView.ShouldSpawnMotesAt), typeof(IntVec3),
            typeof(Map),
            typeof(bool));

        patchMethod(typeof(RimWorld.FertilityGrid), typeof(FertilityGrid), "CellBoolDrawerGetBoolInt");
        patchMethod(typeof(Verse.TerrainGrid), typeof(TerrainGrid), "CellBoolDrawerGetBoolInt");
        patchMethod(typeof(Verse.RoofGrid), typeof(RoofGrid), nameof(Verse.RoofGrid.GetCellBool));

        //Area only designator
        patchMethod(typeof(Designator_AreaBuildRoof), typeof(DesignatorPrefix),
            nameof(Designator_AreaBuildRoof.CanDesignateCell));
        patchMethod(typeof(Designator_AreaNoRoof), typeof(DesignatorPrefix),
            nameof(Designator_AreaNoRoof.CanDesignateCell));
        patchMethod(typeof(Designator_ZoneAdd_Growing), typeof(DesignatorPrefix),
            nameof(Designator_ZoneAdd_Growing.CanDesignateCell));
        patchMethod(typeof(Designator_ZoneAddStockpile), typeof(DesignatorPrefix),
            nameof(Designator_ZoneAddStockpile.CanDesignateCell));

        //Area+Designator
        patchMethod(typeof(Designator_Claim), typeof(DesignatorPrefix), nameof(Designator_Claim.CanDesignateCell));
        patchMethod(typeof(Designator_Claim), typeof(DesignatorPrefix), nameof(Designator_Claim.CanDesignateThing));
        patchMethod(typeof(Designator_Deconstruct), typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateCell));
        patchMethod(typeof(Designator_Deconstruct), typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateThing));
        patchMethod(typeof(Designator_Haul), typeof(DesignatorPrefix), nameof(Designator_Haul.CanDesignateCell));
        patchMethod(typeof(Designator_Haul), typeof(DesignatorPrefix), nameof(Designator_Haul.CanDesignateThing));
        patchMethod(typeof(Designator_Hunt), typeof(DesignatorPrefix), nameof(Designator_Hunt.CanDesignateCell));
        patchMethod(typeof(Designator_Hunt), typeof(DesignatorPrefix), nameof(Designator_Hunt.CanDesignateThing));
        patchMethod(typeof(Designator_Plants), typeof(DesignatorPrefix), nameof(Designator_Plants.CanDesignateCell));
        patchMethod(typeof(Designator_Plants), typeof(DesignatorPrefix), nameof(Designator_Plants.CanDesignateThing));
        patchMethod(typeof(Designator_PlantsHarvest), typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvest.CanDesignateThing));
        patchMethod(typeof(Designator_PlantsHarvestWood), typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvestWood.CanDesignateThing));
        patchMethod(typeof(Designator_RemoveFloor), typeof(DesignatorPrefix),
            nameof(Designator_RemoveFloor.CanDesignateCell));
        patchMethod(typeof(Designator_SmoothSurface), typeof(DesignatorPrefix),
            nameof(Designator_SmoothSurface.CanDesignateCell));
        patchMethod(typeof(Designator_Tame), typeof(DesignatorPrefix), nameof(Designator_Tame.CanDesignateCell));
        patchMethod(typeof(Designator_Tame), typeof(DesignatorPrefix), nameof(Designator_Tame.CanDesignateThing));
        patchMethod(typeof(Designator_Uninstall), typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateCell));

        //PLacing designator
        patchMethod(typeof(Designator_Uninstall), typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateThing));
        patchMethod(typeof(Designator_Build), typeof(DesignatorPlace),
            nameof(Designator_Build.CanDesignateCell));
        patchMethod(typeof(Designator_Install), typeof(DesignatorPlace),
            nameof(Designator_Install.CanDesignateCell));

        //Specific designation
        patchMethod(typeof(Designator_Mine), typeof(DesignatorMine), nameof(Designator_Mine.CanDesignateCell));

        //Designation
        patchMethod(typeof(Verse.Designation), typeof(Designation), nameof(Verse.Designation.Notify_Added));
        patchMethod(typeof(Verse.Designation), typeof(Designation), "Notify_Removing");

        /* Filth checks
        patchMethod(typeof(Thing), typeof(Patch_Filth_Draw), nameof(Filth.DrawNowAt));
        patchMethod(typeof(Filth), typeof(Patch_Filth_Destroy), nameof(Filth.Destroy));
        */

        harmony.Patch(
            typeof(AttackTargetFinder).GetMethod(nameof(AttackTargetFinder.CanSee)),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(CanSeePreFix))));


        LogMessage("Prefixed method AttackTargetFinder_CanSee.");
        harmony.Patch(
            typeof(Verse.LetterStack).GetMethod(nameof(Verse.LetterStack.ReceiveLetter), [
                typeof(Letter), typeof(string), typeof(int), typeof(bool)
            ]),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(ReceiveLetterPrefix))));

        LogMessage("Prefixed method LetterStack_ReceiveLetter.");

        harmony.Patch(
            typeof(OverlayDrawer).GetMethod(nameof(OverlayDrawer.DrawOverlay), [
                typeof(Thing), typeof(OverlayTypes)
            ]),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(DrawOverlayPrefix))));


        harmony.Patch(
            typeof(SustainerManager).GetMethod(
                nameof(SustainerManager.RegisterSustainer)
            ),
            postfix: new HarmonyMethod(
                typeof(Patch_RegisterSustainer).GetMethod(
                    nameof(Patch_RegisterSustainer.Postfix)))
        );
        harmony.Patch(
            typeof(Sustainer).GetMethod(
                nameof(Sustainer.End)
            ),
            postfix: new HarmonyMethod(
                typeof(Patch_UnregisterSustainer).GetMethod(
                    nameof(Patch_UnregisterSustainer.Postfix)))
        );

        LogMessage("Prefixed method OverlayDrawer_DrawOverlay.");

        harmony.Patch(
            typeof(SilhouetteUtility).GetMethod(nameof(SilhouetteUtility.ShouldDrawSilhouette), [
                typeof(Thing)
            ]),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(ShouldDrawSilhouettePrefix))));

        LogMessage("Prefixed method SilhouetteUtility_ShouldDrawSilhouette.");

        harmony.Patch(
            typeof(SoundStarter).GetMethod(
                nameof(SoundStarter.PlayOneShot),
                [typeof(SoundDef), typeof(SoundInfo)]
            ),
            new HarmonyMethod(
                typeof(Patch_PlayOneShot).GetMethod(
                    nameof(Patch_PlayOneShot.Prefix)))
        );


        LogMessage("Prefixed method SoundStarter_PlayOneShot.");

        harmony.Patch(
            typeof(SoundStarter).GetMethod(
                nameof(SoundStarter.TrySpawnSustainer),
                [typeof(SoundDef), typeof(SoundInfo)]
            ),
            new HarmonyMethod(
                typeof(Patch_TrySpawnSustainer).GetMethod(
                    nameof(Patch_TrySpawnSustainer.Prefix))),
            new HarmonyMethod(
                typeof(Patch_TrySpawnSustainer).GetMethod(
                    nameof(Patch_TrySpawnSustainer.Postfix)
                )
            )
        );
        LogMessage("Prefixed method SoundStarter_TrySpawnSustainer.");

        if (ModLister.OdysseyInstalled)
        {
            var landingEndedMethod = AccessTools.Method(typeof(WorldComponent_GravshipController), "LandingEnded");
            if (landingEndedMethod != null)
            {
                harmony.Patch(
                    landingEndedMethod,
                    postfix: new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(LandingEndedPostfix))));
                LogMessage("Postfixed method WorldComponent_GravshipController_LandingEnded.");
            }
            else
            {
                Log.Warning("[Real FoW] Could not find WorldComponent_GravshipController.LandingEnded method.");
            }
        }

        if (!ModsConfig.IsActive("jaxe.bubbles"))
        {
            return;
        }

        var drawBubble = AccessTools.Method(
            "Bubbles.Core.Bubbler:DrawBubble"
        );
        if (drawBubble != null)
        {
            harmony.Patch(
                drawBubble,
                new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(DrawBubblePrefix)))
            );
            LogMessage("Interaction bubble is active. Patched");
        }
        else
        {
            Log.Warning("RFow is active but can't patch DrawBubble method");
        }
    }

    private static void InjectDubsMinimapPatch()
    {
        if (!ModsConfig.IsActive("dubwise.dubsmintminimap"))
        {
            return;
        }

        var h = new Harmony("com.github.lukakama.rimworldmodrealfow.dubs");
        var windowType = AccessTools.TypeByName("DubsMintMinimap.MainTabWindow_MiniMap");
        if (windowType == null)
        {
            Log.Warning("Dubs Mint Minimap is active but can't find MainTabWindow_MiniMap type");
            return;
        }

        var foggedMethods = AccessTools.GetDeclaredMethods(windowType)
            .Where(info => info.Name == "Fogged").ToList();
        if (!foggedMethods.Any())
        {
            Log.Warning("Dubs Mint Minimap is active but can't patch Fogged method");
            return;
        }

        foreach (var foggedMethod in foggedMethods)
        {
            h.Patch(foggedMethod, postfix:
                foggedMethod.GetParameters().Length == 1
                    ? new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(FoggedThingPostfix)))
                    : new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(FoggedCellPostfix)))
            );
        }

        LogMessage("Dubs Mint Minimap is active. Patched");
    }

    private static void patchMethod(Type sourceType, Type targetType, string methodName)
    {
        patchMethod(sourceType, targetType, methodName, null);
    }


    private static void patchMethod(Type sourceType, Type targetType, string methodName, params Type[] types)
    {
        MethodInfo method;
        if (types != null)
        {
            method = sourceType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, types,
                null);
        }
        else
        {
            method = sourceType.GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }

        if (sourceType != method?.DeclaringType)
        {
            LogMessage(
                $"Inconsistent method declaring type for method {methodName}: expected {sourceType} but found {method?.DeclaringType}");
        }

        if (method != null)
        {
            MethodInfo methodInfo = null;
            if (types != null)
            {
                methodInfo = targetType.GetMethod($"{methodName}_Prefix",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                    types, null);
                if (methodInfo == null)
                {
                    methodInfo = targetType.GetMethod($"{methodName}_Prefix",
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null, [
                            sourceType, .. types
                        ], null);
                }
            }

            if (methodInfo == null)
            {
                methodInfo = targetType.GetMethod($"{methodName}_Prefix",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            MethodInfo methodInfo2 = null;
            if (types != null)
            {
                methodInfo2 = targetType.GetMethod($"{methodName}_Postfix",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
                    types, null);
                if (methodInfo2 == null)
                {
                    methodInfo2 = targetType.GetMethod($"{methodName}_Postfix",
                        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                        null, [
                            sourceType, .. types
                        ], null);
                }
            }

            if (methodInfo2 == null)
            {
                methodInfo2 = targetType.GetMethod($"{methodName}_Postfix",
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            }

            if (methodInfo != null || methodInfo2 != null)
            {
                if (patchWithHarmony(method, methodInfo, methodInfo2))
                {
                    LogMessage($"Patched method {method} from source {sourceType} to {targetType}.");
                }
                else
                {
                    Log.Warning($"Unable to patch method {method} from source {sourceType} to {targetType}.");
                }
            }
            else
            {
                Log.Warning(
                    $"Target method prefix or suffix {methodName} not found for patch from source {sourceType} to {targetType}.");
            }
        }
        else
        {
            Log.Warning($"Source method {methodName} not found for patch from source {sourceType} to {targetType}.");
        }
    }

    private static bool patchWithHarmony(MethodInfo original, MethodInfo prefix, MethodInfo postfix)
    {
        bool result;
        try
        {
            var prefix2 = prefix != null ? new HarmonyMethod(prefix) : null;
            var postfix2 = postfix != null ? new HarmonyMethod(postfix) : null;
            harmony.Patch(original, prefix2, postfix2);
            result = true;
        }
        catch (Exception ex)
        {
            Log.Warning($"Error patching with Harmony: {ex.Message}");
            Log.Warning(ex.StackTrace);
            result = false;
        }

        return result;
    }
}