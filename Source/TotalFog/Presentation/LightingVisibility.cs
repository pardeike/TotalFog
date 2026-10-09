using System;
using System.Collections.Generic;
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
        var shown = generator.DeclareLocal(typeof(int[]));
        var result = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.Method(typeof(LightingVisibility), nameof(ShownCells))),
            new(OpCodes.Stloc, shown),
        };
        int matches = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(native))
            {
                var load = new CodeInstruction(OpCodes.Ldloc, shown);
                load.labels.AddRange(instruction.labels);
                instruction.labels.Clear();
                result.Add(load);
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                matches++;
            }
            result.Add(instruction);
        }
        if (matches != 1)
            throw new InvalidOperationException(
                $"Total Fog: expected one visual glow sample, found {matches}."
            );
        return result;
    }
}
