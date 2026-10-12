using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace TotalFog;

/// <summary>Extend native debug commands without changing normal exploration.</summary>
internal static class DebugFogCommands
{
    internal static void Install(Harmony harmony)
    {
        var owner = typeof(DebugFogCommands);
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(DebugToolsGeneral), "ClearAllFog"),
            postfix: new HarmonyMethod(owner, nameof(ClearAllPostfix))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(typeof(DebugActionsMapManagement), "RefogMap"),
            postfix: new HarmonyMethod(owner, nameof(RefogMapPostfix))
        );
        foreach (string method in new[] { "FogRect", "UnfogRect" })
            PatchSafety.Patch(
                harmony,
                AccessTools.Method(typeof(DebugToolsGeneral), method),
                transpiler: new HarmonyMethod(owner, nameof(RectangleTranspiler))
            );
    }

    internal static void ClearAllPostfix() => Apply(Find.CurrentMap, false);

    internal static void RefogMapPostfix() => Apply(Find.CurrentMap, true, restoreSight: true);

    internal static IEnumerable<CodeInstruction> RectangleTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var original = AccessTools.Method(typeof(DebugToolsGeneral), "GenericRectTool");
        if (!PatchSafety.Matches("debug fog rectangles", body, original, 1))
            return body;
        var replacement = AccessTools.Method(typeof(DebugFogCommands), nameof(RectangleTool));
        foreach (var instruction in body)
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
        return body;
    }

    internal static void RectangleTool(string label, Action<CellRect> action, bool closeOnComplete)
    {
        DebugToolsGeneral.GenericRectTool(
            label,
            rect =>
            {
                var map = Find.CurrentMap;
                action(rect);
                Apply(map, label == "Fog", rect: rect);
            },
            closeOnComplete
        );
    }

    private static void Apply(
        Map map,
        bool fogged,
        bool restoreSight = false,
        CellRect? rect = null
    )
    {
        if (map == null)
            return;
        map.GetVisibility()
            .DebugSetFog(rect ?? new CellRect(0, 0, map.Size.x, map.Size.z), fogged, restoreSight);
        // Debug edits must appear immediately, including while paused. Keep
        // normal fade settings and ordinary sight-source updates untouched.
        bool fade = SectionLayerFog.PrefEnableFade;
        try
        {
            SectionLayerFog.PrefEnableFade = false;
            map.mapDrawer.RegenerateEverythingNow();
        }
        finally
        {
            SectionLayerFog.PrefEnableFade = fade;
        }
    }
}
