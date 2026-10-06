using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>CE keeps its own ballistics; successful hit checks also respect the shared fog policy.</summary>
internal static class CombatExtendedIntegration
{
    private static readonly Type[] HitArguments = [typeof(Vector3), typeof(IntVec3), typeof(Thing)];
    private static readonly Type[] ShootLineArguments = [typeof(IntVec3), typeof(LocalTargetInfo),
        typeof(ShootLine).MakeByRefType(), typeof(Vector3).MakeByRefType()];
    private static readonly Type[] BurstFallbackArguments = [typeof(bool), typeof(ShootLine).MakeByRefType()];

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
        void Patch(System.Reflection.MethodInfo target, string postfix = nameof(HitCellPostfix))
        {
            if (target == null || target.ReturnType != typeof(bool) || target.IsStatic || target.IsAbstract ||
                target.ContainsGenericParameters || !patched.Add(target)) return;
            try { harmony.Patch(target, postfix: new HarmonyMethod(typeof(CombatExtendedIntegration), postfix) { priority = Priority.Last }); }
            catch (Exception exception) { Log.Warning("[Total Fog] Combat Extended integration: " + target.DeclaringType.FullName + ": " + exception.Message); }
        }
        Patch(method);
        void PatchShotPaths(Type verbType, bool required = false)
        {
            var shot = AccessTools.DeclaredMethod(verbType, "TryFindCEShootLineFromTo", ShootLineArguments);
            var fallback = AccessTools.DeclaredMethod(verbType, "KeepBurstOnNoShootLine", BurstFallbackArguments);
            if (required && (shot == null || fallback == null))
                Log.Warning("[Total Fog] Combat Extended integration: burst shoot-line hooks are unavailable.");
            Patch(shot, nameof(ShootLinePostfix));
            Patch(fallback, nameof(BurstFallbackPostfix));
        }
        PatchShotPaths(type, required: true);
        // Inherited methods are already covered. A declared override needs its
        // own hook even when another mod supplies the derived CE verb.
        foreach (var child in GenTypes.AllTypes)
            if (child != type && type.IsAssignableFrom(child))
            {
                Patch(AccessTools.DeclaredMethod(child, method.Name, HitArguments));
                PatchShotPaths(child);
            }
        // Overhead weapons bypass hit-cell LOS. Filter automatic acquisition at
        // CE's existing validator; deliberate indirect-fire cell orders stay native.
        var turretType = AccessTools.TypeByName("CombatExtended.Building_TurretGunCE");
        var validator = turretType != null && typeof(Building_Turret).IsAssignableFrom(turretType)
            ? AccessTools.DeclaredMethod(turretType, "IsValidTarget", [typeof(Thing)]) : null;
        if (validator == null)
            Log.Warning("[Total Fog] Combat Extended integration: turret target validator is unavailable.");
        else Patch(validator, nameof(TurretTargetPostfix));
    }

    public static void HitCellPostfix(Verse.Verb __instance, ref bool __result, Vector3 __0, IntVec3 __1)
    {
        if (!__result) return;
        Detours.Verb.CanHitCellFromCellIgnoringRange_Postfix(__instance, ref __result, __0.ToIntVec3(), __1);
    }

    public static void TurretTargetPostfix(Building_Turret __instance, Thing __0, ref bool __result)
    {
        if (__result && __0 != null) __result = CanTrack(__instance, __0, __0.Position);
    }

    public static void ShootLinePostfix(Verse.Verb __instance, LocalTargetInfo __1, ref bool __result, ShootLine __2)
    {
        // Native Retarget runs before this check. Overhead weapons otherwise
        // bypass the hit-cell hook and can follow a now-hidden Thing.
        // Another mod can choose a logical body cell away from a Thing's root.
        // Validate CE's actual selected destination, without repeating targeting.
        if (__result && __1.HasThing) __result = CanTrack(__instance.caster, __1.Thing, __2.Dest);
    }

    public static void BurstFallbackPostfix(Verse.Verb __instance, ref bool __result, ShootLine __1)
    {
        // CE may convert a locked burst to its last known cell. Keep that blind
        // fire; only a fallback still tracking an unseen Thing is restricted.
        var target = __instance.CurrentTarget;
        if (__result && target.HasThing) __result = CanTrack(__instance.caster, target.Thing, __1.Dest);
    }

    private static bool CanTrack(Thing caster, Thing target, IntVec3 targetCell)
    {
        Thing observer = caster?.TryGetComp<CompMannable>()?.ManningPawn ?? caster;
        if (observer?.Map == null || observer.Faction == null || target.MapHeld != observer.Map ||
            observer.Faction != Faction.OfPlayer && !FogSettings.AISmart) return true;
        var fog = observer.Map.GetVisibility();
        return fog?.Initialized != true || fog.IsShown(observer.Faction, targetCell);
    }
}
