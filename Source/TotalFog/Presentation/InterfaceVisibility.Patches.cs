using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Presentation;

internal static partial class InterfaceVisibility
{
    public static IEnumerable<CodeInstruction> TargetCandidatesTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var original = AccessTools.Method(typeof(GenUI), nameof(GenUI.ThingsUnderMouse));
        var replacement = AccessTools.Method(
            typeof(InterfaceVisibility),
            nameof(TargetThingsUnderMouse)
        );
        var body = instructions.ToList();
        if (
            !PatchSafety.Matches(
                "InterfaceVisibility.Patches.TargetCandidatesTranspiler",
                body,
                original,
                1
            )
        )
            return body;
        var result = new List<CodeInstruction>();
        foreach (var instruction in body)
        {
            if (instruction.Calls(original))
            {
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }

    public static List<Thing> TargetThingsUnderMouse(
        Vector3 clickPos,
        float pawnWideClickRadius,
        TargetingParameters clickParams,
        ITargetingSource source
    ) =>
        FilterTargetThings(
            GenUI.ThingsUnderMouse(clickPos, pawnWideClickRadius, clickParams, source),
            IntVec3.FromVector3(clickPos)
        );

    public static IEnumerable<CodeInstruction> MouseoverTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var original = AccessTools.Method(
            typeof(GridsUtility),
            nameof(GridsUtility.GetThingList),
            new[] { typeof(IntVec3), typeof(Map) }
        );
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(MouseoverThings));
        var body = instructions.ToList();
        if (
            !PatchSafety.Matches(
                "InterfaceVisibility.Patches.MouseoverTranspiler",
                body,
                original,
                1
            )
        )
            return body;
        var result = new List<CodeInstruction>();
        foreach (var instruction in body)
        {
            if (instruction.Calls(original))
            {
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }

    // Preserve vanilla dispatch and custom overrides, including non-pawn Things.
    // The audited 1.6 method contains exactly one virtual overlay dispatch.
    public static IEnumerable<CodeInstruction> OverlayTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var original = AccessTools.Method(typeof(Thing), nameof(Thing.DrawGUIOverlay));
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(DrawOverlay));
        var body = instructions.ToList();
        if (
            !PatchSafety.Matches("InterfaceVisibility.Patches.OverlayTranspiler", body, original, 1)
        )
            return body;
        var result = new List<CodeInstruction>();
        foreach (var instruction in body)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            result.Add(instruction);
        }
        return result;
    }

    // Keep the cheap camera-rectangle check first. Only the second position
    // read runs the fog gate, before native tooltips or shot reports are built.
    public static IEnumerable<CodeInstruction> TooltipTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var original = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(TooltipPosition));
        var nativeFog = AccessTools.Method(
            typeof(GridsUtility),
            nameof(GridsUtility.Fogged),
            new[] { typeof(IntVec3), typeof(Map) }
        );
        var fogGate = AccessTools.Method(typeof(InterfaceVisibility), nameof(TooltipFogged));
        var body = instructions.ToList();
        if (
            !PatchSafety.Matches("InterfaceVisibility.Patches.TooltipTranspiler", body, original, 2)
            || !PatchSafety.Matches(
                "InterfaceVisibility.Patches.TooltipTranspiler.Fogged",
                body,
                nativeFog,
                1
            )
        )
            return body;
        var result = new List<CodeInstruction>();
        int positionRead = 0;
        foreach (var instruction in body)
        {
            if (instruction.Calls(original) && ++positionRead == 2)
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            if (instruction.Calls(nativeFog))
                instruction.operand = fogGate;
            result.Add(instruction);
        }
        return result;
    }
}
