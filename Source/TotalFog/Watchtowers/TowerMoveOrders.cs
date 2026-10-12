using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

/// <summary>Resolve ordered platform destinations without opening the solid base to pathfinding.</summary>
internal static class TowerMoveOrders
{
    internal static void Install(Harmony harmony)
    {
        var owner = typeof(TowerMoveOrders);
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(FloatMenuOptionProvider_DraftedMove),
                "GetSingleOption",
                new[] { typeof(FloatMenuContext) }
            ),
            transpiler: new HarmonyMethod(owner, nameof(MoveAnchorTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(Selector),
                "HandleMultiselectGoto",
                new[] { typeof(FloatMenuContext) }
            ),
            transpiler: new HarmonyMethod(owner, nameof(MoveAnchorTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertySetter(
                typeof(Pawn_DraftController),
                nameof(Pawn_DraftController.Drafted)
            ),
            transpiler: new HarmonyMethod(owner, nameof(DraftInterruptionTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(MultiPawnGotoController),
                nameof(MultiPawnGotoController.Draw)
            ),
            transpiler: new HarmonyMethod(owner, nameof(FormationDrawingTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(MultiPawnGotoController),
                nameof(MultiPawnGotoController.OnGUI)
            ),
            transpiler: new HarmonyMethod(owner, nameof(FormationLabelsTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(FloatMenuOptionProvider_DraftedMove),
                "PawnCanGoto",
                new[] { typeof(Pawn), typeof(IntVec3) }
            ),
            prefix: new HarmonyMethod(owner, nameof(CanGotoPrefix))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(RCellFinder),
                nameof(RCellFinder.BestOrderedGotoDestNear),
                new[] { typeof(IntVec3), typeof(Pawn), typeof(Predicate<IntVec3>), typeof(bool) }
            ),
            prefix: new HarmonyMethod(owner, nameof(DestinationPrefix))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(Pawn_JobTracker),
                nameof(Pawn_JobTracker.TryTakeOrderedJob),
                new[] { typeof(Job), typeof(JobTag?), typeof(bool) }
            ),
            prefix: new HarmonyMethod(owner, nameof(OrderedJobPrefix))
        );
        LongEventHandler.ExecuteWhenFinished(() =>
            PatchSafety.Install("Achtung tower moves", () => InstallAchtung(harmony))
        );
    }

    private static void InstallAchtung(Harmony harmony)
    {
        // Achtung's static initialization needs loaded definitions/resources.
        // Bind after startup, just like other optional compatibility hooks.
        // Achtung uses this anchor before the common destination/job pipeline.
        // Bind only when present; Total Fog has no assembly dependency on it.
        var achtung = AccessTools.TypeByName("AchtungMod.Tools");
        var anchor =
            achtung == null
                ? null
                : AccessTools.Method(
                    achtung,
                    "TryGetStandableMoveAnchor",
                    new[] { typeof(IntVec3), typeof(Map), typeof(IntVec3).MakeByRefType() }
                );
        if (anchor != null)
            PatchSafety.Patch(
                harmony,
                anchor,
                prefix: new HarmonyMethod(typeof(TowerMoveOrders), nameof(AchtungAnchorPrefix))
            );
    }

    internal static Building_Watchtower At(IntVec3 cell, Map map)
    {
        if (map == null || !cell.InBounds(map))
            return null;
        if (
            cell.GetEdifice(map) is Building_Watchtower tower
            && (cell == tower.Position || cell == DeckCell(tower))
        )
            return tower;
        // The drawn standing point is north of the logical center for every
        // rotation. Two grid lookups suffice; no scan of maps or tower lists.
        var center = (
            cell.ToVector3Shifted()
            - new UnityEngine.Vector3(0, 0, Core.TowerGeometry.PawnProjection)
        ).ToIntVec3();
        return
            center.InBounds(map)
            && center.GetEdifice(map) is Building_Watchtower projected
            && center == projected.Position
            ? projected
            : null;
    }

    internal static IntVec3 DeckCell(Building_Watchtower tower) =>
        (
            tower.Position.ToVector3Shifted()
            + new Vector3(0, 0, WatchtowerGraphic.PlatformProjection)
        ).ToIntVec3();

    private static bool Available(Building_Watchtower tower, Pawn pawn) =>
        pawn?.Spawned == true
        && pawn.Drafted
        && pawn.RaceProps.Humanlike
        && !pawn.Downed
        && (tower.Occupant == pawn || tower.CanEnter(pawn) && pawn.CanReserve(tower));

    internal static IEnumerable<CodeInstruction> MoveAnchorTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var original = AccessTools.Method(
            typeof(CellFinder),
            nameof(CellFinder.StandableCellNear),
            new[] { typeof(IntVec3), typeof(Map), typeof(float), typeof(Predicate<IntVec3>) }
        );
        return ReplaceCalls(
            instructions,
            original,
            AccessTools.Method(typeof(TowerMoveOrders), nameof(MoveAnchor)),
            1
        );
    }

    internal static IEnumerable<CodeInstruction> DraftInterruptionTranspiler(
        IEnumerable<CodeInstruction> instructions
    ) =>
        ReplaceCalls(
            instructions,
            AccessTools.Method(
                typeof(Pawn_JobTracker),
                nameof(Pawn_JobTracker.IsCurrentJobPlayerInterruptible)
            ),
            AccessTools.Method(typeof(TowerMoveOrders), nameof(InterruptForDraftChange)),
            1
        );

    internal static bool InterruptForDraftChange(Pawn_JobTracker jobs) =>
        jobs.curDriver is JobDriver_Watchtower driver && driver.HoldWhenDrafted()
            ? false
            : jobs.IsCurrentJobPlayerInterruptible();

    internal static IEnumerable<CodeInstruction> FormationDrawingTranspiler(
        IEnumerable<CodeInstruction> instructions
    ) =>
        ReplaceCalls(
            instructions,
            AccessTools.Method(
                typeof(IntVec3),
                nameof(IntVec3.ToVector3ShiftedWithAltitude),
                new[] { typeof(float) }
            ),
            AccessTools.Method(typeof(TowerMoveOrders), nameof(FormationDrawPosition)),
            4
        );

    internal static IEnumerable<CodeInstruction> FormationLabelsTranspiler(
        IEnumerable<CodeInstruction> instructions
    ) =>
        ReplaceCalls(
            instructions,
            AccessTools.Method(typeof(IntVec3), nameof(IntVec3.ToUIRect)),
            AccessTools.Method(typeof(TowerMoveOrders), nameof(FormationLabelRect)),
            1
        );

    private static IEnumerable<CodeInstruction> ReplaceCalls(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo original,
        MethodInfo replacement,
        int expected
    )
    {
        var body = instructions.ToList();
        if (!PatchSafety.Matches("tower order " + original?.Name, body, original, expected))
            return body;
        foreach (var instruction in body)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
        }
        return body;
    }

    private static Vector3 FormationOffset(IntVec3 cell)
    {
        var tower = At(cell, Find.CurrentMap);
        return tower != null && cell == tower.Position
            ? new Vector3(0, 0, Core.TowerGeometry.PawnProjection)
            : Vector3.zero;
    }

    internal static Vector3 FormationDrawPosition(ref IntVec3 cell, float altitude) =>
        cell.ToVector3ShiftedWithAltitude(altitude) + FormationOffset(cell);

    internal static Rect FormationLabelRect(ref IntVec3 cell)
    {
        var rect = cell.ToUIRect();
        var offset = FormationOffset(cell);
        if (offset != Vector3.zero)
            rect.position +=
                (cell.ToVector3() + offset).MapToUIPosition() - cell.ToVector3().MapToUIPosition();
        return rect;
    }

    internal static IntVec3 MoveAnchor(
        IntVec3 cell,
        Map map,
        float radius,
        Predicate<IntVec3> validator = null
    ) =>
        At(cell, map) != null && (validator == null || validator(cell))
            ? cell
            : CellFinder.StandableCellNear(cell, map, radius, validator);

    internal static bool AchtungAnchorPrefix(
        IntVec3 cell,
        Map map,
        ref IntVec3 result,
        ref bool __result
    )
    {
        if (At(cell, map) == null)
            return true;
        result = cell;
        __result = true;
        return false;
    }

    internal static bool CanGotoPrefix(Pawn pawn, IntVec3 gotoLoc, ref AcceptanceReport __result)
    {
        var tower = At(gotoLoc, pawn.Map);
        if (tower == null)
            return true;
        __result = Available(tower, pawn) ? true : "CannotGoNoPath".Translate();
        return false;
    }

    internal static bool DestinationPrefix(
        IntVec3 root,
        Pawn searcher,
        Predicate<IntVec3> cellValidator,
        ref IntVec3 __result
    )
    {
        var tower = At(root, searcher.Map);
        if (
            tower == null
            || !Available(tower, searcher)
            || cellValidator != null && !cellValidator(tower.Position)
        )
            return true;
        __result = tower.Position;
        return false;
    }

    internal static bool OrderedJobPrefix(ref Job job, Pawn ___pawn, ref bool __result)
    {
        if (job?.def != JobDefOf.Goto || !___pawn.Drafted || job.targetA.HasThing)
            return true;
        var tower = At(job.targetA.Cell, ___pawn.Map);
        if (tower == null)
            return true;
        if (tower.Occupant == ___pawn)
        {
            __result = true; // Reissuing the same position must not descend and climb again.
            return false;
        }
        if (!Available(tower, ___pawn))
        {
            __result = false;
            return false;
        }
        // Clone preserves native order metadata and its synchronized job ID,
        // while discarding any cached Goto driver. Native queue handling and
        // reservations then run normally with the actual ladder job.
        var watch = job.Clone();
        watch.def = WatchtowerDefOf.TotalFog_WatchtowerWatch;
        watch.targetA = tower;
        watch.count = 0;
        job = watch;
        return true;
    }
}
