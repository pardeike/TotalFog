using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TotalFog.Presentation;

internal static partial class DiscoveryOverlays
{
    private static readonly FieldInfo roofMap = AccessTools.Field(typeof(RoofGrid), "map");
    private static readonly FieldInfo terrainMap = AccessTools.Field(typeof(TerrainGrid), "map");
    private static readonly FieldInfo fertilityMap = AccessTools.Field(
        typeof(FertilityGrid),
        "map"
    );

    private static bool[] Snapshot(Func<int, bool> getter)
    {
        var field = getter.Target switch
        {
            RoofGrid => roofMap,
            TerrainGrid => terrainMap,
            FertilityGrid => fertilityMap,
            _ => null,
        };
        // Other native/modded drawers retain their own policy. Reflection runs
        // once per mesh, never in the map-wide cell loop.
        return KnownCells(field?.GetValue(getter.Target) as Map);
    }

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var body = instructions.ToList();
        var invoke = AccessTools.Method(typeof(Func<int, bool>), "Invoke");
        var getter = AccessTools.Field(typeof(CellBoolDrawer), "cellBoolGetter");
        if (getter == null || !PatchSafety.Matches("discovery overlays", body, invoke, 1))
            return body;
        var known = generator.DeclareLocal(typeof(bool[]));
        var result = new List<CodeInstruction>
        {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldfld, getter),
            new(OpCodes.Call, AccessTools.Method(typeof(DiscoveryOverlays), nameof(Snapshot))),
            new(OpCodes.Stloc, known),
        };
        foreach (var instruction in body)
        {
            if (instruction.Calls(invoke))
            {
                result.Add(new CodeInstruction(OpCodes.Ldloc, known).MoveLabelsFrom(instruction));
                instruction.opcode = OpCodes.Call;
                instruction.operand = AccessTools.Method(
                    typeof(DiscoveryOverlays),
                    nameof(VisibleCell)
                );
            }
            result.Add(instruction);
        }
        return result;
    }
}
