using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace TotalFog.Presentation;

internal static class ExplosionVisibility
{
    public static void Shake(CameraShaker shaker, float magnitude, Explosion explosion)
    {
        if (CellVisibility.IsCurrent(explosion.Map, explosion.Position))
            shaker.DoShake(magnitude);
    }

    // Gate only the presentation call. The native worker still applies heat,
    // creates effects and processes every damaged cell normally.
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(
            typeof(CameraShaker),
            nameof(CameraShaker.DoShake),
            new[] { typeof(float) }
        );
        var replacement = AccessTools.Method(typeof(ExplosionVisibility), nameof(Shake));
        int matches = 0;
        var result = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction));
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                matches++;
            }
            result.Add(instruction);
        }
        if (matches != 1)
            throw new InvalidOperationException(
                $"Total Fog: expected one explosion camera shake, found {matches}."
            );
        return result;
    }
}
