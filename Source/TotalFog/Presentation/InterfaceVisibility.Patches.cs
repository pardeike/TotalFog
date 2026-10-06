using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Presentation;

internal static partial class InterfaceVisibility
{
    public static IEnumerable<CodeInstruction> TargetCandidatesTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(GenUI), nameof(GenUI.ThingsUnderMouse));
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(TargetThingsUnderMouse));
        int matches = 0;
        var result = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original)) { instruction.operand = replacement; matches++; }
            result.Add(instruction);
        }
        if (matches != 1) throw new InvalidOperationException($"Total Fog: expected one targeting candidate list, found {matches}.");
        return result;
    }

    public static List<Thing> TargetThingsUnderMouse(Vector3 clickPos, float pawnWideClickRadius,
        TargetingParameters clickParams, ITargetingSource source) =>
        FilterTargetThings(GenUI.ThingsUnderMouse(clickPos, pawnWideClickRadius, clickParams, source),
            IntVec3.FromVector3(clickPos));

    public static IEnumerable<CodeInstruction> MouseoverTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(GridsUtility), nameof(GridsUtility.GetThingList), new[] { typeof(IntVec3), typeof(Map) });
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(MouseoverThings));
        int matches = 0;
        var result = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original)) { instruction.operand = replacement; matches++; }
            result.Add(instruction);
        }
        if (matches != 1) throw new InvalidOperationException($"Total Fog: expected one mouseover Thing list, found {matches}.");
        return result;
    }

    // Preserve vanilla dispatch and custom overrides, including non-pawn Things.
    // The audited 1.6 method contains exactly one virtual overlay dispatch.
    public static IEnumerable<CodeInstruction> OverlayTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.Method(typeof(Thing), nameof(Thing.DrawGUIOverlay));
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(DrawOverlay));
        int matches = 0;
        var result = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                matches++;
            }
            result.Add(instruction);
        }
        if (matches != 1) throw new InvalidOperationException($"Total Fog: expected one Thing overlay dispatch, found {matches}.");
        return result;
    }

    // An invalid position fails the existing camera-rectangle check before shot
    // reports, GetTooltip, or custom tooltip code can disclose a hidden Thing.
    public static IEnumerable<CodeInstruction> TooltipTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var original = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Position));
        var replacement = AccessTools.Method(typeof(InterfaceVisibility), nameof(TooltipPosition));
        int matches = 0;
        var result = new List<CodeInstruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(original))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
                matches++;
            }
            result.Add(instruction);
        }
        if (matches != 2) throw new InvalidOperationException($"Total Fog: expected two tooltip position reads, found {matches}.");
        return result;
    }
}
