using System;
using System.Collections.Generic;
using System.Linq;
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
        var body = instructions.ToList();
        if (!PatchSafety.Matches("ExplosionVisibility.Transpiler", body, original, 1))
            return body;
        var result = new List<CodeInstruction>();
        foreach (var instruction in body)
        {
            if (instruction.Calls(original))
            {
                result.Add(new CodeInstruction(OpCodes.Ldarg_1).MoveLabelsFrom(instruction));
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }
}
