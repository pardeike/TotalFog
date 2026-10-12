using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace TotalFog.Watchtowers;

internal static class TowerBedSearch
{
    internal static IntVec3 Position(Thing thing) =>
        TowerPresentation.TowerFor(thing)?.Approach ?? thing.Position;

    internal static IntVec3 PositionHeld(Thing thing) =>
        TowerPresentation.TowerFor(thing)?.Approach ?? thing.PositionHeld;

    // Keep RimWorld's medical/owned/ideology bed preferences, reservations and
    // native validator. Only its two reachability search roots use ladder ground.
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var position = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));
        var held = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.PositionHeld));
        int positions = body.Count(c => c.Calls(position));
        int heldPositions = body.Count(c => c.Calls(held));
        if (positions != 1 || heldPositions != 1)
        {
            PatchSafety.Warn("tower bed search", "unsupported native position reads");
            return body;
        }
        foreach (var code in body)
            if (code.Calls(position) || code.Calls(held))
            {
                code.operand = AccessTools.Method(
                    typeof(TowerBedSearch),
                    code.Calls(position) ? nameof(Position) : nameof(PositionHeld)
                );
                code.opcode = OpCodes.Call;
            }
        return body;
    }
}
