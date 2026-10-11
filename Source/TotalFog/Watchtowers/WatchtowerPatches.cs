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
            harmony.Patch(
                AccessTools.Method(type, method, arguments)
                    ?? throw new MissingMethodException(type.FullName, method),
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
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos)),
            postfix: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.DrawPosPostfix)
            )
        );
        harmony.Patch(
            AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.DrawPos)),
            postfix: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.DrawPosPostfix)
            )
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
        harmony.Patch(
            AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
            transpiler: new HarmonyMethod(
                typeof(TowerPresentation),
                nameof(TowerPresentation.CullTranspiler)
            )
        );
        harmony.Patch(
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

    internal static void RoofPostfix(IntVec3 c, Map ___map) =>
        ___map.GetComponent<MapComponent_Watchtowers>().CellChanged(c);

    internal static void FinalizerEligibilityPostfix(Pawn pawn, ref bool __result)
    {
        if (
            !__result
            && !pawn.Downed
            && !pawn.Dead
            && TowerPresentation.TowerFor(pawn)?.Elevation > 0
        )
            __result = true;
    }

    internal static bool DeconstructPrefix(Thing t, ref bool __result)
    {
        if (t is not Building_Watchtower { Occupant: not null })
            return true;
        __result = false;
        JobFailReason.Is("TotalFog_TowerEmptyFirst".Translate());
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
