using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace TotalFog.Presentation;

internal static partial class GasVisibility
{
    // Snapshot the viewed faction's array once per regeneration. Only gas mesh
    // emission is gated; gas density, diffusion and gameplay stay native.
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var body = instructions.ToList();
        var native = AccessTools.Method(
            typeof(GasGrid),
            nameof(GasGrid.AnyGasAt),
            new[] { typeof(IntVec3) }
        );
        if (!PatchSafety.Matches("gas mesh", body, native, 1))
            return body;
        var map = generator.DeclareLocal(typeof(Map));
        var shown = generator.DeclareLocal(typeof(int[]));
        var result = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, AccessTools.PropertyGetter(typeof(MapDrawLayer), "Map")),
            new(OpCodes.Stloc, map),
            new(OpCodes.Ldloc, map),
            new(OpCodes.Call, AccessTools.Method(typeof(GasVisibility), nameof(ShownCells))),
            new(OpCodes.Stloc, shown),
        };
        foreach (var instruction in body)
        {
            if (instruction.Calls(native))
            {
                result.Add(new CodeInstruction(OpCodes.Ldloc, map).MoveLabelsFrom(instruction));
                result.Add(new CodeInstruction(OpCodes.Ldloc, shown));
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(
                    typeof(GasVisibility),
                    nameof(AnyVisibleGasAt)
                );
            }
            result.Add(instruction);
        }
        return result;
    }
}
