using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

internal static class WatchtowerPatches
{
    internal static void Install(Harmony harmony)
    {
        void Patch(
            Type type,
            string method,
            string prefix = null,
            string postfix = null,
            Type owner = null,
            string finalizer = null,
            Type[] arguments = null
        )
        {
            owner ??= typeof(WatchtowerPatches);
            PatchSafety.Patch(
                harmony,
                AccessTools.Method(type, method, arguments),
                prefix == null ? null : new HarmonyMethod(owner, prefix),
                postfix == null ? null : new HarmonyMethod(owner, postfix),
                finalizer: finalizer == null ? null : new HarmonyMethod(owner, finalizer)
            );
        }
        Patch(typeof(Building), nameof(Building.SpawnSetup), postfix: nameof(SpawnPostfix));
        Patch(
            typeof(Building),
            nameof(Building.DeSpawn),
            nameof(DespawnPrefix),
            nameof(DespawnPostfix)
        );
        Patch(typeof(RoofGrid), nameof(RoofGrid.SetRoof), nameof(RoofPrefix), nameof(RoofPostfix));
        Patch(
            typeof(Pawn_JobTracker),
            "CanPawnTakeOpportunisticJob",
            postfix: nameof(FinalizerEligibilityPostfix)
        );
        Patch(
            typeof(JobDriver),
            nameof(JobDriver.GetFinalizerJob),
            postfix: nameof(FinalizerPostfix)
        );
        Patch(typeof(Thing), nameof(Thing.DeSpawn), nameof(ActorDespawnPrefix));
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.FlammableNow)),
            postfix: new HarmonyMethod(typeof(WatchtowerPatches), nameof(FlammablePostfix))
        );
        Patch(typeof(Pawn), nameof(Pawn.Kill), nameof(DeathPrefix), nameof(DeathPostfix));
        Patch(
            typeof(Pawn),
            nameof(Pawn.DropAndForbidEverything),
            nameof(IncapacitatedCarrierPrefix)
        );
        Patch(
            typeof(GenSpawn),
            nameof(GenSpawn.SpawnBuildingAsPossible),
            nameof(LoadedTowerPrefix),
            finalizer: nameof(LoadedTowerFinalizer)
        );
        Patch(
            typeof(Pawn_PathFollower),
            nameof(Pawn_PathFollower.TryRecoverFromUnwalkablePosition),
            nameof(PositionRecoveryPrefix)
        );
        Patch(
            typeof(Reachability),
            nameof(Reachability.CanReach),
            nameof(ReachabilityPrefix),
            arguments: new[]
            {
                typeof(IntVec3),
                typeof(LocalTargetInfo),
                typeof(PathEndMode),
                typeof(TraverseParms),
            }
        );
        Patch(
            typeof(Pawn_JobTracker),
            nameof(Pawn_JobTracker.TryOpportunisticJob),
            nameof(DescentPriorityPrefix)
        );
        Patch(
            typeof(WorkGiver_Deconstruct),
            nameof(WorkGiver_Deconstruct.HasJobOnThing),
            nameof(DeconstructPrefix)
        );
        Patch(
            typeof(Pawn),
            nameof(Pawn.DynamicDrawPhaseAt),
            nameof(TowerPresentation.PawnDrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(Corpse),
            nameof(Corpse.DynamicDrawPhaseAt),
            nameof(TowerPresentation.CorpseDrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(typeof(Pawn_DrawTracker), nameof(Pawn_DrawTracker.DrawPos)),
            postfix: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.TrackerDrawPosPostfix)
            )
        );
        Patch(
            typeof(GenThing),
            nameof(GenThing.TrueCenter),
            postfix: nameof(TowerPresentation.TrueCenterPostfix),
            owner: typeof(TowerPresentation),
            arguments: new[] { typeof(Thing) }
        );
        Patch(
            typeof(GenMapUI),
            nameof(GenMapUI.LabelDrawPosFor),
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer),
            arguments: new[] { typeof(Thing), typeof(float) }
        );
        Patch(
            typeof(GenDraw),
            nameof(GenDraw.DrawAimPie),
            nameof(TowerPresentation.AimPiePrefix),
            owner: typeof(TowerPresentation)
        );
        Patch(
            typeof(SelectionDrawer),
            "DrawSelectionBracketFor",
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(OverlayDrawer),
            "DrawAllOverlays",
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(GenUI),
            nameof(GenUI.ThingsUnderMouse),
            nameof(TowerPresentation.DrawingPrefix),
            nameof(TowerPresentation.MouseTargetsPostfix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(MoteProgressBar),
            "DrawAt",
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(Mote),
            "DrawAt",
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(MoteBubble),
            "DrawAt",
            nameof(TowerPresentation.DrawingPrefix),
            owner: typeof(TowerPresentation),
            finalizer: nameof(TowerPresentation.DrawingFinalizer)
        );
        Patch(
            typeof(Thing),
            nameof(Thing.DynamicDrawPhaseAt),
            nameof(TowerPresentation.AttachmentDrawingPrefix),
            owner: typeof(TowerPresentation)
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(typeof(Mote), nameof(Mote.DrawPos)),
            postfix: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.MotePositionPostfix)
            )
        );
        Patch(
            typeof(WorkGiver_RescueDowned),
            nameof(WorkGiver_RescueDowned.HasJobOnThing),
            nameof(RescueEligibilityPrefix)
        );
        Patch(
            typeof(WorkGiver_RescueDowned),
            nameof(WorkGiver_RescueDowned.JobOnThing),
            nameof(RescueJobPrefix)
        );
        Patch(
            typeof(WeatherEvent_LightningStrike),
            nameof(WeatherEvent_LightningStrike.DoStrike),
            nameof(LightningPrefix)
        );
        TowerCombat.Install(harmony);
        TowerEffects.Install(harmony);
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(FleckStatic),
                nameof(FleckStatic.Draw),
                new[] { typeof(float), typeof(DrawBatch) }
            ),
            transpiler: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.FleckTranspiler)
            )
        );
        Patch(typeof(CompColorable), nameof(CompColorable.SetColor), postfix: nameof(ColorPostfix));
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
            transpiler: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.CullTranspiler)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(RestUtility),
                nameof(RestUtility.FindBedFor),
                new[]
                {
                    typeof(Pawn),
                    typeof(Pawn),
                    typeof(bool),
                    typeof(bool),
                    typeof(GuestStatus?),
                }
            ),
            transpiler: new HarmonyMethod(typeof(TowerBedSearch), nameof(TowerBedSearch.Transpiler))
        );
    }

    internal static void SpawnPostfix(Building __instance)
    {
        if (__instance.Map == null)
            return;
        foreach (var cell in __instance.OccupiedRect())
            __instance.Map.GetComponent<MapComponent_Watchtowers>().CellChanged(cell);
    }

    internal struct DespawnState
    {
        internal Map Map;
        internal CellRect Rect;
    }

    internal static void DespawnPrefix(Building __instance, out DespawnState __state) =>
        __state = new DespawnState { Map = __instance.Map, Rect = __instance.OccupiedRect() };

    internal static void DespawnPostfix(DespawnState __state)
    {
        if (__state.Map == null)
            return;
        foreach (var cell in __state.Rect)
            __state.Map.GetComponent<MapComponent_Watchtowers>().CellChanged(cell);
    }

    internal static bool RoofPrefix(IntVec3 c, RoofDef def, Map ___map) =>
        def == null || c.GetEdifice(___map) is not Building_Watchtower;

    internal static void FlammablePostfix(Thing __instance, ref bool __result)
    {
        if (__result || !__instance.Spawned)
            return;
        var tower = TowerPresentation.TowerFor(__instance);
        if (tower?.ElevationFor(__instance) <= 0 || tower == null)
            return;
        // A solid building protects things sharing its ground cell. An actor
        // on this platform is above it, but retains native fire resistance and
        // protection from any other fire bulwark in the same cell.
        if (__instance.GetStatValue(StatDefOf.Flammability) < .01f)
            return;
        foreach (var thing in __instance.Position.GetThingList(__instance.Map))
            if (thing != tower && thing.FireBulwark)
                return;
        __result = true;
    }

    internal static void RoofPostfix(IntVec3 c, Map ___map) =>
        ___map.GetComponent<MapComponent_Watchtowers>().CellChanged(c);

    internal static void ColorPostfix(CompColorable __instance)
    {
        if (__instance.parent is Building { Spawned: true } building)
            building.Map.GetVisibility().TowerComponent?.DrawingChanged(building.Position);
    }

    internal static void FinalizerEligibilityPostfix(Pawn pawn, ref bool __result)
    {
        if (
            !__result
            && !pawn.Downed
            && !pawn.Dead
            && TowerPresentation.TowerFor(pawn)?.ElevationFor(pawn) > 0
        )
            __result = true;
    }

    internal static void FinalizerPostfix(JobDriver __instance, ref Job __result)
    {
        var pawn = __instance.GetActor();
        if (__result != null || pawn?.Spawned != true || pawn.Downed || pawn.Dead)
            return;
        var tower = TowerPresentation.TowerFor(pawn);
        if (tower?.ElevationFor(pawn) > 0)
            __result = JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerDescend, tower);
    }

    internal static void ReachabilityPrefix(
        ref IntVec3 start,
        LocalTargetInfo dest,
        TraverseParms traverseParams,
        Map ___map
    )
    {
        var pawn = traverseParams.pawn;
        if (
            pawn?.Spawned != true
            || pawn.Map != ___map
            || start != pawn.Position
            || dest.Cell == start
            || !start.InBounds(___map)
            || start.GetEdifice(___map) is not Building_Watchtower tower
            || !tower.HasActor(pawn)
            || !tower.Approach.InBounds(___map)
            || !tower.Approach.Walkable(___map)
        )
            return;
        // An ordinary order first descends through the ladder. Keep all native
        // target/traversal checks, but start that route at its actual ground exit.
        // The solid platform and queries for other pawns remain impassable.
        start = tower.Approach;
    }

    internal static bool DescentPriorityPrefix(
        Job finalizerJob,
        Job job,
        Pawn ___pawn,
        ref Job __result
    )
    {
        if (
            finalizerJob?.def != WatchtowerDefOf.TotalFog_WatchtowerDescend
            || ___pawn.Downed
            || ___pawn.Dead
        )
            return true;
        var tower = TowerPresentation.TowerFor(___pawn);
        if (tower == null)
            return true;
        bool shooting =
            FogSettings.TowerShootingAllowed
            && tower.ElevationFor(___pawn) == 1
            && (job.def == JobDefOf.AttackStatic || job.def == JobDefOf.Wait_Combat);
        if (shooting)
        {
            tower.Watching = true;
            tower.RefreshSight();
        }
        // Native non-opportunistic jobs must still descend. A direct descend
        // order must not enqueue a redundant second descent after the finalizer.
        __result =
            shooting || job.def == WatchtowerDefOf.TotalFog_WatchtowerDescend ? null : finalizerJob;
        return false;
    }

    internal static bool DeconstructPrefix(Thing t, ref bool __result)
    {
        if (t is not Building_Watchtower tower || tower.Occupant == null && tower.Rescuer == null)
            return true;
        __result = false;
        JobFailReason.Is("TotalFog_TowerEmptyFirst".Translate());
        return false;
    }

    internal static void ActorDespawnPrefix(Thing __instance)
    {
        if (__instance is not (Pawn or Corpse))
            return;
        // Death creates a corpse after despawning the pawn. Keep that top slot
        // until the corpse exists. All living external transfers relinquish it.
        if (__instance is Pawn { Dead: true })
            return;
        var tower = TowerPresentation.TowerFor(__instance);
        if (tower != null)
            tower.Release(__instance as Pawn ?? ((Corpse)__instance).InnerPawn);
    }

    internal static void DeathPrefix(Pawn __instance, out Building_Watchtower __state)
    {
        __state = TowerPresentation.TowerFor(__instance);
        // Native death cleanup can end the carrying job before SetDead and
        // DropAndForbidEverything. Preserve the ladder height before that drop.
        __state?.DropCarriedActor(__instance, true);
    }

    internal static void IncapacitatedCarrierPrefix(Pawn __instance)
    {
        if (__instance.Downed || __instance.Dead)
            TowerPresentation.TowerFor(__instance)?.DropCarriedActor(__instance, true);
    }

    internal static void DeathPostfix(Pawn __instance, Building_Watchtower __state)
    {
        if (__state?.Spawned != true || !__state.HasActor(__instance))
            return;
        var corpse = __instance.Corpse;
        if (corpse?.Spawned == true && corpse.Map == __state.Map)
        {
            // Native corpse placement can move items out of an impassable
            // edifice. This body's saved platform slot is intentionally valid.
            corpse.Position = __state.Position;
            corpse.TryGetComp<CompFog>()?.ComponentsPositionTracker?.updatePosition();
            if (__state.Occupant == __instance)
                __state.Watching = false;
            __state.RefreshSight();
        }
        else
            __state.Release(__instance); // Disintegration must not occupy the slot forever.
    }

    internal struct TowerLoadState
    {
        internal Thing Body,
            Rescuer;
        internal IntVec3 BodyCell,
            RescuerCell;
        internal Map Map;
    }

    internal static void LoadedTowerPrefix(
        Building building,
        Map map,
        bool respawningAfterLoad,
        out TowerLoadState __state
    )
    {
        __state = default;
        if (
            !respawningAfterLoad
            || building is not Building_Watchtower tower
            || !tower.OccupiedRect().InBounds(map)
        )
            return;
        __state.Map = map;
        // Native map loading spawns pawns/items before buildings. Temporarily
        // move only these saved actors outside the footprint while native
        // SpawnBuildingAsPossible validates all other collisions and refunds.
        // No despawn, job notification, simulation tick or Unity work occurs.
        __state.Body = MoveAside(tower.OccupantBody, out __state.BodyCell);
        __state.Rescuer = MoveAside(
            tower.Rescuer?.Dead == true ? tower.Rescuer.Corpse : tower.Rescuer,
            out __state.RescuerCell
        );
        Thing MoveAside(Thing body, out IntVec3 original)
        {
            original = IntVec3.Invalid;
            if (body?.Map != map || !tower.OccupiedRect().Contains(body.Position))
                return null;
            original = body.Position;
            body.Position = tower.Approach;
            return body;
        }
    }

    internal static Exception LoadedTowerFinalizer(TowerLoadState __state, Exception __exception)
    {
        Restore(__state.Body, __state.BodyCell);
        Restore(__state.Rescuer, __state.RescuerCell);
        return __exception;
        void Restore(Thing body, IntVec3 cell)
        {
            if (body == null || body.Map != __state.Map || !cell.IsValid)
                return;
            body.Position = cell;
            if (body is Pawn pawn)
                pawn.Drawer.tweener.Notify_Teleported();
        }
    }

    internal static bool PositionRecoveryPrefix(Pawn ___pawn, ref bool __result)
    {
        var tower = TowerPresentation.TowerFor(___pawn);
        if (tower == null || !tower.OccupiedRect().Contains(___pawn.Position))
            return true;
        // The solid edifice is a valid occupied position only for its own
        // ladder/platform actors. Everybody else's native recovery is unchanged.
        __result = true;
        return false;
    }

    internal static bool RescueEligibilityPrefix(Pawn pawn, Thing t, bool forced, ref bool __result)
    {
        var tower = TowerPresentation.TowerFor(t);
        if (tower == null || t is not Pawn patient)
            return true;
        __result =
            patient.Downed
            && !patient.Dead
            && (forced || patient.Faction == pawn.Faction)
            && (forced || !patient.IsForbidden(pawn) && !GenAI.EnemyIsNear(patient, 25))
            && tower.CanEnter(pawn, patient)
            && pawn.CanReserve(patient)
            && pawn.CanReserve(tower)
            && RestUtility.FindBedFor(patient, pawn, checkSocialProperness: false) != null;
        return false;
    }

    internal static bool RescueJobPrefix(Pawn pawn, Thing t, ref Job __result)
    {
        var tower = TowerPresentation.TowerFor(t);
        if (tower == null || t is not Pawn patient)
            return true;
        var bed = RestUtility.FindBedFor(patient, pawn, checkSocialProperness: false);
        __result =
            bed == null
                ? null
                : JobMaker.MakeJob(
                    WatchtowerDefOf.TotalFog_WatchtowerRetrieve,
                    tower,
                    bed,
                    patient
                );
        return false;
    }

    internal static void LightningPrefix(ref IntVec3 strikeLoc, Map map)
    {
        if (strikeLoc.IsValid)
            return; // Respect deliberately targeted native/mod strikes.
        var towers = map.GetComponent<MapComponent_Watchtowers>().Towers;
        if (towers.Count == 0)
            return;
        // Ordinary lightning never chooses impassable cells. Add tower weights
        // to that same event, without adding timers or making more storms.
        float towerWeight = towers.Count * 9f * 8f;
        if (!Rand.Chance(towerWeight / (map.cellIndices.NumGridCells + towerWeight)))
            return;
        var tower = towers[Rand.Range(0, towers.Count)];
        if (!tower.Position.Roofed(map))
            strikeLoc = tower.Position;
        // Native lightning's Flame explosion includes the occupant at this
        // logical cell. Never apply a second copy of the strike's damage.
    }
}
