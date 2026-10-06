using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>CE keeps its own ballistics; successful hit checks also respect the shared fog policy.</summary>
internal static class CombatExtendedIntegration
{
    private static readonly Type[] HitArguments = [typeof(Vector3), typeof(IntVec3), typeof(Thing)];

    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.IsActive("ceteam.combatextended")) return;
        var type = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
        if (type == null || !typeof(Verse.Verb).IsAssignableFrom(type))
        {
            Log.Warning("[Total Fog] Combat Extended integration: projectile verb is unavailable.");
            return;
        }
        var method = AccessTools.DeclaredMethod(type, "CanHitCellFromCellIgnoringRange", HitArguments);
        if (method == null || method.ReturnType != typeof(bool) || method.IsStatic)
        {
            Log.Warning("[Total Fog] Combat Extended integration: supported hit-check overload is unavailable.");
            return;
        }
        var patched = new HashSet<System.Reflection.MethodInfo>();
        void Patch(System.Reflection.MethodInfo target)
        {
            if (target == null || target.ReturnType != typeof(bool) || target.IsStatic || target.IsAbstract ||
                target.ContainsGenericParameters || !patched.Add(target)) return;
            try { harmony.Patch(target, postfix: new HarmonyMethod(typeof(CombatExtendedIntegration), nameof(HitCellPostfix)) { priority = Priority.Last }); }
            catch (Exception exception) { Log.Warning("[Total Fog] Combat Extended integration: " + target.DeclaringType.FullName + ": " + exception.Message); }
        }
        Patch(method);
        // Inherited methods are already covered. A declared override needs its
        // own hook even when another mod supplies the derived CE verb.
        foreach (var child in GenTypes.AllTypes)
            if (child != type && type.IsAssignableFrom(child))
                Patch(AccessTools.DeclaredMethod(child, method.Name, HitArguments));
    }

    public static void HitCellPostfix(Verse.Verb __instance, ref bool __result, Vector3 __0, IntVec3 __1)
    {
        if (!__result) return;
        Detours.Verb.CanHitCellFromCellIgnoringRange_Postfix(__instance, ref __result, __0.ToIntVec3(), __1);
    }
}
