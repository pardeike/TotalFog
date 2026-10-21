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

// Harmony registration for the base fog hooks, installed as one PatchSafety feature
// from the static constructor in TotalFogMod.cs. Threat alerts and gravship visibility
// install from here as their own features. Other features with an Install method are
// listed in that static constructor; integrations with other mods install from the
// instance constructor once loading has finished.
public partial class TotalFogMod
{
    private static void InjectDetours()
    {
        PatchSafety.Install("threat alerts", () => Notifications.ThreatAlerts.Install(harmony));
        PatchSafety.Patch(
            harmony,
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
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(typeof(SampleSustainer), "Volume"),
            postfix: new HarmonyMethod(
                typeof(Audio.SustainerVisibility),
                nameof(Audio.SustainerVisibility.VolumePostfix)
            )
        );

        PatchSafety.Patch(
            harmony,
            typeof(SoundStarter).GetMethod(
                nameof(SoundStarter.PlayOneShot),
                [typeof(SoundDef), typeof(SoundInfo)]
            ),
            new HarmonyMethod(typeof(Patch_PlayOneShot).GetMethod(nameof(Patch_PlayOneShot.Prefix)))
        );

        LogMessage("Prefixed method SoundStarter_PlayOneShot.");
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(
                typeof(MusicManagerPlay),
                nameof(MusicManagerPlay.DangerMusicMode)
            ),
            prefix: new HarmonyMethod(
                typeof(Audio.MusicVisibility),
                nameof(Audio.MusicVisibility.DangerModePrefix)
            )
        );

        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(MusicTransition),
                nameof(MusicTransition.IsTransitionSatisfied)
            ),
            prefix: new HarmonyMethod(
                typeof(Audio.MusicVisibility),
                nameof(Audio.MusicVisibility.TransitionPrefix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(MusicManagerPlay), nameof(MusicManagerPlay.CheckTransitions)),
            prefix: new HarmonyMethod(
                typeof(Audio.MusicVisibility),
                nameof(Audio.MusicVisibility.CheckTransitionsPrefix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(MusicManagerPlay),
                nameof(MusicManagerPlay.ForceTriggerTransition)
            ),
            prefix: new HarmonyMethod(
                typeof(Audio.MusicVisibility),
                nameof(Audio.MusicVisibility.ForceTransitionPrefix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(EntityCodex),
                nameof(EntityCodex.SetDiscovered),
                [typeof(EntityCodexEntryDef), typeof(ThingDef), typeof(Thing)]
            ),
            prefix: new HarmonyMethod(
                typeof(Notifications.EntityDiscovery),
                nameof(Notifications.EntityDiscovery.DiscoverPrefix)
            )
        );

        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(SectionLayer_LightingOverlay), "GenerateLightingOverlay"),
            transpiler: new HarmonyMethod(
                typeof(Presentation.LightingVisibility),
                nameof(Presentation.LightingVisibility.Transpiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.InitializeComps)),
            prefix: new HarmonyMethod(typeof(TotalFogMod), nameof(InitializeCompsPrefix))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(SectionLayer_Gas),
                nameof(SectionLayer_Gas.Regenerate),
                Type.EmptyTypes
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.GasVisibility),
                nameof(Presentation.GasVisibility.Transpiler)
            )
        );
        PatchMethod(typeof(Verse.Verb), typeof(Verb), "CanHitCellFromCellIgnoringRange");
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(Verse.Verb), nameof(Verse.Verb.TryFindShootLineFromTo)),
            prefix: new HarmonyMethod(
                typeof(Watchtowers.TowerCombat),
                nameof(Watchtowers.TowerCombat.ShootLinePrefix)
            ),
            postfix: new HarmonyMethod(typeof(Verb), nameof(Verb.TryFindShootLineFromTo_Postfix))
            {
                priority = Priority.Last,
            }
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(Building_TurretGun), "IsValidTarget"),
            postfix: new HarmonyMethod(typeof(Verb), nameof(Verb.TurretTargetPostfix))
        );
        // Selection proxies recurse directly into SelectInternal. Guard the
        // actual target as well as the initial public Select request.
        PatchMethod(typeof(RimWorld.Selector), typeof(Selector), "SelectInternal");
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(RimWorld.Selector), "SelectableObjectsUnderMouse"),
            postfix: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.MouseSelectionCandidatesPostfix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(ThingSelectionUtility),
                nameof(ThingSelectionUtility.MultiSelectableThingsInScreenRectDistinct)
            ),
            postfix: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.MultiSelectCandidatesPostfix)
            )
        );
        PatchMethod(
            typeof(RimWorld.BeautyUtility),
            typeof(BeautyUtility),
            nameof(RimWorld.BeautyUtility.FillBeautyRelevantCells)
        );
        PatchMethod(typeof(MainTabWindow_Wildlife), typeof(MainTabWindowWildlife), "get_Pawns");

        PatchMethod(typeof(Verse.Pawn), typeof(Pawn), nameof(Verse.Pawn.DrawGUIOverlay));
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
            postfix: new HarmonyMethod(
                typeof(Presentation.DynamicVisibility),
                nameof(Presentation.DynamicVisibility.ComputeCulledThings_Postfix)
            ),
            transpiler: new HarmonyMethod(
                typeof(Watchtowers.TowerPresentation),
                nameof(Watchtowers.TowerPresentation.CullTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
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
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(
                typeof(FleckStatic),
                nameof(FleckStatic.Draw),
                new[] { typeof(float), typeof(DrawBatch) }
            ),
            prefix: new HarmonyMethod(
                typeof(Presentation.FleckVisibility),
                nameof(Presentation.FleckVisibility.DrawPrefix)
            ),
            transpiler: new HarmonyMethod(
                typeof(Watchtowers.TowerPresentation),
                nameof(Watchtowers.TowerPresentation.FleckTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(DamageWorker), nameof(DamageWorker.ExplosionStart)),
            transpiler: new HarmonyMethod(
                typeof(Presentation.ExplosionVisibility),
                nameof(Presentation.ExplosionVisibility.Transpiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(ThingOverlays), nameof(ThingOverlays.ThingOverlaysOnGUI)),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.OverlayTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(TooltipGiverList),
                nameof(TooltipGiverList.DispenseAllThingTooltips)
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.TooltipTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(Verse.MouseoverReadout),
                nameof(Verse.MouseoverReadout.MouseoverReadoutOnGUI)
            ),
            prefix: new HarmonyMethod(
                typeof(MouseoverReadout),
                nameof(MouseoverReadout.MouseoverReadoutOnGUI_Prefix)
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.MouseoverTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.EnumeratorMoveNext(
                AccessTools.Method(typeof(Verse.GenUI), nameof(Verse.GenUI.TargetsAt))
            ),
            transpiler: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.TargetCandidatesTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Constructor(
                typeof(FloatMenuContext),
                [typeof(List<Verse.Pawn>), typeof(Vector3), typeof(Map)]
            ),
            postfix: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.ContextMenuPostfix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(FloatMenuOptionProvider_DraftedAttack),
                nameof(FloatMenuOptionProvider_DraftedAttack.GetOptionsFor),
                [typeof(Thing), typeof(FloatMenuContext)]
            ),
            prefix: new HarmonyMethod(
                typeof(Presentation.InterfaceVisibility),
                nameof(Presentation.InterfaceVisibility.DraftedAttackOptionsPrefix)
            )
        );
        foreach (
            var method in typeof(Verse.GenMapUI)
                .GetMethods()
                .Where(m => m.Name == nameof(Verse.GenMapUI.DrawPawnLabel))
        )
            PatchSafety.Patch(
                harmony,
                method,
                prefix: new HarmonyMethod(
                    typeof(Presentation.InterfaceVisibility),
                    nameof(Presentation.InterfaceVisibility.PawnLabelPrefix)
                )
            );
        PatchMethod(
            typeof(Verse.GenMapUI),
            typeof(GenMapUI),
            nameof(Verse.GenMapUI.DrawThingLabel),
            typeof(Thing),
            typeof(string),
            typeof(Color)
        );

        PatchMethod(
            typeof(SectionLayer_ThingsGeneral),
            typeof(SectionLayerThingsGeneral),
            "TakePrintFrom"
        );
        PatchMethod(
            typeof(SectionLayer_ThingsPowerGrid),
            typeof(SectionLayerThingsPowerGrid),
            "TakePrintFrom"
        );
        PatchMethod(
            typeof(Verse.AI.ReservationUtility),
            typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserve)
        );
        PatchMethod(
            typeof(Verse.AI.ReservationUtility),
            typeof(ReservationUtility),
            nameof(Verse.AI.ReservationUtility.CanReserveAndReach)
        );
        PatchMethod(
            typeof(Verse.AI.HaulAIUtility),
            typeof(HaulAIUtility),
            nameof(Verse.AI.HaulAIUtility.HaulToStorageJob)
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(Dialog_MapSearch), "ThingIsVisibleToPlayer"),
            postfix: new HarmonyMethod(
                typeof(Presentation.MapSearchVisibility),
                nameof(Presentation.MapSearchVisibility.VisibilityPostfix)
            )
        );
        foreach (var method in new[] { "OnHighlightUpdate", "ClikedOnElement" })
            PatchSafety.Patch(
                harmony,
                AccessTools.Method(typeof(Dialog_MapSearch), method),
                prefix: new HarmonyMethod(
                    typeof(Presentation.MapSearchVisibility),
                    nameof(Presentation.MapSearchVisibility.LocationPrefix)
                )
            );
        PatchMethod(
            typeof(Verse.EnvironmentStatsDrawer),
            typeof(EnvironmentStatsDrawer),
            "ShouldShowWindowNow"
        );

        PatchSafety.Patch(
            harmony,
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

        PatchSafety.Patch(
            harmony,
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
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(IncidentWorker_AggressiveAnimals), "TryExecuteWorker"),
            transpiler: new HarmonyMethod(
                typeof(HarmonyPatches),
                nameof(ManhunterArrivalTranspiler)
            )
        );

        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(RimWorld.MoteBubble), "DrawAt"),
            prefix: new HarmonyMethod(
                typeof(Watchtowers.TowerPresentation),
                nameof(Watchtowers.TowerPresentation.BubbleDrawingPrefix)
            ),
            finalizer: new HarmonyMethod(
                typeof(Watchtowers.TowerPresentation),
                nameof(Watchtowers.TowerPresentation.DrawingFinalizer)
            )
        );
        PatchMethod(
            typeof(Verse.GenView),
            typeof(GenView),
            nameof(Verse.GenView.ShouldSpawnMotesAt),
            typeof(IntVec3),
            typeof(Map),
            typeof(bool)
        );

        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(CellBoolDrawer), nameof(CellBoolDrawer.RegenerateMesh)),
            transpiler: new HarmonyMethod(
                typeof(Presentation.DiscoveryOverlays),
                nameof(Presentation.DiscoveryOverlays.Transpiler)
            )
        );

        //Area only designator
        PatchMethod(
            typeof(Designator_AreaBuildRoof),
            typeof(DesignatorPrefix),
            nameof(Designator_AreaBuildRoof.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_AreaNoRoof),
            typeof(DesignatorPrefix),
            nameof(Designator_AreaNoRoof.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_ZoneAdd_Growing),
            typeof(DesignatorPrefix),
            nameof(Designator_ZoneAdd_Growing.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_ZoneAddStockpile),
            typeof(DesignatorPrefix),
            nameof(Designator_ZoneAddStockpile.CanDesignateCell)
        );

        //Area+Designator
        PatchMethod(
            typeof(Designator_Claim),
            typeof(DesignatorPrefix),
            nameof(Designator_Claim.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Claim),
            typeof(DesignatorPrefix),
            nameof(Designator_Claim.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Deconstruct),
            typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Deconstruct),
            typeof(DesignatorPrefix),
            nameof(Designator_Deconstruct.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Haul),
            typeof(DesignatorPrefix),
            nameof(Designator_Haul.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Haul),
            typeof(DesignatorPrefix),
            nameof(Designator_Haul.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Hunt),
            typeof(DesignatorPrefix),
            nameof(Designator_Hunt.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Hunt),
            typeof(DesignatorPrefix),
            nameof(Designator_Hunt.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Plants),
            typeof(DesignatorPrefix),
            nameof(Designator_Plants.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Plants),
            typeof(DesignatorPrefix),
            nameof(Designator_Plants.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_PlantsHarvest),
            typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvest.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_PlantsHarvestWood),
            typeof(DesignatorPrefix),
            nameof(Designator_PlantsHarvestWood.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_RemoveFloor),
            typeof(DesignatorPrefix),
            nameof(Designator_RemoveFloor.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_SmoothSurface),
            typeof(DesignatorPrefix),
            nameof(Designator_SmoothSurface.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Tame),
            typeof(DesignatorPrefix),
            nameof(Designator_Tame.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Tame),
            typeof(DesignatorPrefix),
            nameof(Designator_Tame.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Uninstall),
            typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateCell)
        );

        //PLacing designator
        PatchMethod(
            typeof(Designator_Uninstall),
            typeof(DesignatorPrefix),
            nameof(Designator_Uninstall.CanDesignateThing)
        );
        PatchMethod(
            typeof(Designator_Build),
            typeof(DesignatorPlace),
            nameof(Designator_Build.CanDesignateCell)
        );
        PatchMethod(
            typeof(Designator_Install),
            typeof(DesignatorPlace),
            nameof(Designator_Install.CanDesignateCell)
        );

        foreach (var type in new[] { typeof(Designator_Forbid), typeof(Designator_Unforbid) })
            PatchMethod(type, typeof(DoorOrders), nameof(Designator_Forbid.CanDesignateThing));

        //Specific designation
        PatchMethod(
            typeof(Designator_Mine),
            typeof(DesignatorMine),
            nameof(Designator_Mine.CanDesignateCell)
        );

        //Designation
        PatchMethod(
            typeof(Verse.Designation),
            typeof(Designation),
            nameof(Verse.Designation.Notify_Added)
        );
        PatchMethod(typeof(Verse.Designation), typeof(Designation), "Notify_Removing");

        /* Filth checks
        PatchMethod(typeof(Thing), typeof(Patch_Filth_Draw), nameof(Filth.DrawNowAt));
        PatchMethod(typeof(Filth), typeof(Patch_Filth_Destroy), nameof(Filth.Destroy));
        */

        PatchSafety.Patch(
            harmony,
            typeof(AttackTargetFinder).GetMethod(nameof(AttackTargetFinder.CanSee)),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(CanSeePreFix)))
        );

        LogMessage("Prefixed method AttackTargetFinder_CanSee.");

        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(RimWorld.QuestGen.QuestNode_Root_RefugeePodCrash),
                "SendLetter_NewTemp"
            ),
            prefix: new HarmonyMethod(
                typeof(Notifications.RefugeePodNotifications),
                nameof(Notifications.RefugeePodNotifications.Prefix)
            ),
            finalizer: new HarmonyMethod(
                typeof(Notifications.RefugeePodNotifications),
                nameof(Notifications.RefugeePodNotifications.Finalizer)
            )
        );

        PatchSafety.Patch(
            harmony,
            typeof(OverlayDrawer).GetMethod(
                nameof(OverlayDrawer.DrawOverlay),
                [typeof(Thing), typeof(OverlayTypes)]
            ),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(DrawOverlayPrefix)))
        );

        LogMessage("Prefixed method OverlayDrawer_DrawOverlay.");

        PatchSafety.Patch(
            harmony,
            typeof(SilhouetteUtility).GetMethod(
                nameof(SilhouetteUtility.ShouldDrawSilhouette),
                [typeof(Thing)]
            ),
            new HarmonyMethod(typeof(HarmonyPatches).GetMethod(nameof(ShouldDrawSilhouettePrefix)))
        );

        LogMessage("Prefixed method SilhouetteUtility_ShouldDrawSilhouette.");

        PatchSafety.Install(
            "gravship visibility",
            () => Compatibility.GravshipVisibility.Install(harmony)
        );
        PatchSafety.Install(
            "player transport-pod visibility",
            () => Compatibility.TransportPodVisibility.Install(harmony)
        );
    }

    private static void PatchMethod(
        Type sourceType,
        Type targetType,
        string methodName,
        params Type[] types
    )
    {
        PatchSafety.Install(
            sourceType.FullName + "." + methodName,
            () =>
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
                PatchSafety.Patch(
                    harmony,
                    original,
                    prefix == null ? null : new HarmonyMethod(prefix),
                    postfix == null ? null : new HarmonyMethod(postfix)
                );
            }
        );
    }
}
