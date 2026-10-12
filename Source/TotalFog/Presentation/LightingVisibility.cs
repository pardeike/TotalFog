using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Presentation;

internal static class LightingVisibility
{
    private static int[] ShownCells(Map map)
    {
        if (ThingVisibility.Bypass(map))
            return null;
        var visibility = map.GetVisibility();
        return visibility.Initialized ? visibility.GetFactionShownCells(Faction.OfPlayer) : null;
    }

    private static Color32 VisualGlowAt(GlowGrid grid, int index, int[] shown) =>
        shown == null || (uint)index < shown.Length && shown[index] != 0
            ? grid.VisualGlowAt(index)
            : default;

    // Clip artificial light only in the rendering mesh. Gameplay illumination,
    // glower registration, propagation and sky/roof shading stay native.
    public static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var native = AccessTools.Method(
            typeof(GlowGrid),
            nameof(GlowGrid.VisualGlowAt),
            new[] { typeof(int) }
        );
        var replacement = AccessTools.Method(typeof(LightingVisibility), nameof(VisualGlowAt));
        var body = instructions.ToList();
        if (!PatchSafety.Matches("LightingVisibility.Transpiler", body, native, 1))
            return body;
        var shown = generator.DeclareLocal(typeof(int[]));
        var result = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(LightingVisibility), nameof(ShownCells))),
            new(OpCodes.Stloc, shown),
        };
        foreach (var instruction in body)
        {
            if (instruction.Calls(native))
            {
                var load = new CodeInstruction(OpCodes.Ldloc, shown);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                result.Add(load);
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }
}
