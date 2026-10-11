using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TotalFog.Watchtowers;

/// <summary>Drawing reads elevation. Simulation always reads native ground coordinates.</summary>
internal static class TowerPresentation
{
    [ThreadStatic]
    internal static int DrawingDepth;

    internal static Building_Watchtower TowerFor(Thing thing) =>
        thing?.MapHeld?.GetComponent<MapComponent_Watchtowers>().For(thing);

    internal static Vector3 Offset(Thing thing)
    {
        if (thing is Pawn { jobs.curDriver: JobDriver_Watchtower driver } && driver.Climbing)
            return driver.VisualOffset;
        return TowerFor(thing)?.OffsetFor(thing) ?? Vector3.zero;
    }

    internal static void PawnDrawingPrefix(Pawn __instance, ref Vector3 drawLoc)
    {
        drawLoc += Offset(__instance);
        DrawingDepth++;
    }

    internal static void CorpseDrawingPrefix(Corpse __instance, ref Vector3 drawLoc)
    {
        drawLoc += Offset(__instance);
        DrawingDepth++;
    }

    internal static void DrawingPrefix() => DrawingDepth++;

    internal static Exception DrawingFinalizer(Exception __exception)
    {
        DrawingDepth--;
        return __exception;
    }

    internal static void DrawPosPostfix(Thing __instance, ref Vector3 __result)
    {
        if (DrawingDepth > 0)
            __result += Offset(__instance);
    }

    internal static CellRect CullBounds(Thing thing)
    {
        var bounds = thing.OccupiedDrawRect();
        var offset = Offset(thing);
        if (offset == Vector3.zero)
            return bounds;
        return new CellRect(
            bounds.minX + Mathf.FloorToInt(offset.x),
            bounds.minZ + Mathf.FloorToInt(offset.z),
            bounds.Width + Mathf.CeilToInt(offset.x) - Mathf.FloorToInt(offset.x),
            bounds.Height + Mathf.CeilToInt(offset.z) - Mathf.FloorToInt(offset.z)
        );
    }

    // Move coarse drawing bounds only. Native fog/depth tests keep the logical
    // cell, so a raised sprite neither disappears at the camera edge nor leaks fog.
    internal static IEnumerable<CodeInstruction> CullTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var native = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedDrawRect));
        if (body.Count(c => c.Calls(native)) != 1)
            throw new InvalidOperationException(
                "Total Fog: native culling must have one occupied draw-rect call."
            );
        foreach (var code in body)
            if (code.Calls(native))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(TowerPresentation), nameof(CullBounds));
            }
        return body;
    }
}
