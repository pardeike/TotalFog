using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace TotalFog.Combat;

internal static class UnseenFirePatches
{
    private static Func<JobGiver_AIFightEnemy, Pawn, IntVec3> flagPosition;
    private static Func<JobGiver_AIFightEnemy, Pawn, float> flagRadius;

    internal static void Install(Harmony harmony)
    {
        flagPosition = AccessTools.MethodDelegate<Func<JobGiver_AIFightEnemy, Pawn, IntVec3>>(
            AccessTools.DeclaredMethod(typeof(JobGiver_AIFightEnemy), "GetFlagPosition")
        );
        flagRadius = AccessTools.MethodDelegate<Func<JobGiver_AIFightEnemy, Pawn, float>>(
            AccessTools.DeclaredMethod(typeof(JobGiver_AIFightEnemy), "GetFlagRadius")
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(typeof(Verb), "TryCastNextBurstShot"),
            transpiler: new HarmonyMethod(typeof(UnseenFirePatches), nameof(ShotTranspiler))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(typeof(JobGiver_AIFightEnemy), "TryGiveJob"),
            postfix: new HarmonyMethod(typeof(UnseenFirePatches), nameof(CombatJobPostfix))
            {
                priority = Priority.Last,
            }
        );
    }

    // Verified against native 1.7: local 0 saves currentTarget, and the sole
    // TryCastShot call branches past the successful-shot body when false.
    internal static IEnumerable<CodeInstruction> ShotTranspiler(IEnumerable<CodeInstruction> body)
    {
        var codes = body.ToList();
        var cast = AccessTools.DeclaredMethod(typeof(Verb), "TryCastShot");
        int index = codes.FindIndex(code => code.Calls(cast));
        if (
            codes.Count < 4
            || !codes[1].LoadsField(AccessTools.Field(typeof(Verb), "currentTarget"))
            || codes[2].opcode != OpCodes.Stloc_0
            || !PatchSafety.Matches("unseen fire", codes, cast, 1)
            || index + 1 >= codes.Count
            || codes[index + 1].opcode != OpCodes.Brfalse
                && codes[index + 1].opcode != OpCodes.Brfalse_S
        )
        {
            PatchSafety.Warn("unseen fire", "successful-shot hook unavailable; reaction disabled");
            return codes;
        }
        codes.InsertRange(
            index + 2,
            new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
                new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.DeclaredMethod(typeof(UnseenFire), nameof(UnseenFire.ShotFired))
                ),
            }
        );
        return codes;
    }

    internal static void CombatJobPostfix(
        JobGiver_AIFightEnemy __instance,
        Pawn pawn,
        ref Job __result
    )
    {
        if (__result != null || !UnseenFire.HasMemory(pawn))
            return;
        __result = UnseenFire.InvestigationJob(
            pawn,
            __result,
            flagPosition(__instance, pawn),
            flagRadius(__instance, pawn)
        );
    }
}
