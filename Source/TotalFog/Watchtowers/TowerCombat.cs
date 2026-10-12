using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using TotalFog.Core;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.Watchtowers;

/// <summary>Only shots involving a tower take this path. Native shields, accuracy and damage remain native.</summary>
internal static class TowerCombat
{
    private sealed class ShotPath
    {
        internal float SourceHeight = TowerGeometry.GroundAimHeight;
        internal float TargetHeight = TowerGeometry.GroundAimHeight;
        internal Vector3 SourceProjection;
        internal Vector3 TargetProjection;
        internal Building_Watchtower SourceTower;
        internal Building_Watchtower TargetTower;
        internal bool ProjectRotation;
    }

    private static readonly ConditionalWeakTable<Projectile, ShotPath> paths = new();
    private static readonly Dictionary<Type, bool> rotationPolicies = new();
    private static readonly AccessTools.StructFieldRef<ShotReport, float> targetSize =
        AccessTools.StructFieldRefAccess<ShotReport, float>("factorFromTargetSize");
    private static readonly AccessTools.StructFieldRef<ShotReport, float> coverChance =
        AccessTools.StructFieldRefAccess<ShotReport, float>("coversOverallBlockChance");
    private static readonly Func<Projectile, float> distanceCovered = AccessTools.MethodDelegate<
        Func<Projectile, float>
    >(AccessTools.PropertyGetter(typeof(Projectile), "DistanceCoveredFraction"));
    private static readonly Action<Projectile, Thing, bool> impact = AccessTools.MethodDelegate<
        Action<Projectile, Thing, bool>
    >(AccessTools.Method(typeof(Projectile), "Impact"));
    private static readonly AccessTools.FieldRef<Projectile, Vector3> origin =
        AccessTools.FieldRefAccess<Projectile, Vector3>("origin");
    private static readonly AccessTools.FieldRef<Projectile, Vector3> destination =
        AccessTools.FieldRefAccess<Projectile, Vector3>("destination");

    internal static float Height(Thing thing) =>
        TowerGeometry.GroundAimHeight
        + TowerGeometry.PlatformLift(TowerPresentation.TowerFor(thing)?.ElevationFor(thing) ?? 0);

    // A foreign ballistic solver remains authoritative. Only reject its successful
    // direct shot when our tower/room/mountain policy says the line is blocked.
    internal static bool AllowsNativeShotLine(
        Verb verb,
        IntVec3 root,
        LocalTargetInfo target,
        IntVec3 destination
    )
    {
        var sourceTower = TowerPresentation.TowerFor(verb.caster);
        var targetTower = TowerPresentation.TowerFor(target.Thing);
        return sourceTower == null && targetTower == null
            || ClearLine(
                verb.caster.Map,
                root,
                destination,
                PlatformFor(verb.caster, sourceTower),
                PlatformFor(target.Thing, targetTower) ?? target.Thing as Building_Watchtower,
                Height(verb.caster),
                Height(target.Thing)
            );
    }

    private static Building_Watchtower PlatformFor(Thing actor, Building_Watchtower tower) =>
        tower?.ElevationFor(actor) > .999f ? tower : null;

    private static bool UsesNativeFlightRotation(Projectile projectile)
    {
        var type = projectile.GetType();
        if (!rotationPolicies.TryGetValue(type, out bool result))
        {
            result =
                AccessTools.PropertyGetter(type, nameof(Projectile.ExactRotation))?.DeclaringType
                == typeof(Projectile);
            rotationPolicies.Add(type, result);
        }
        return result;
    }

    internal static void Install(Harmony harmony)
    {
        void Patch(
            Type type,
            string method,
            string prefix = null,
            string postfix = null,
            Type[] arguments = null
        ) =>
            harmony.Patch(
                AccessTools.Method(type, method, arguments)
                    ?? throw new MissingMethodException(type.FullName, method),
                prefix == null ? null : new HarmonyMethod(typeof(TowerCombat), prefix),
                postfix == null ? null : new HarmonyMethod(typeof(TowerCombat), postfix)
            );
        Patch(typeof(Verb), nameof(Verb.TryFindShootLineFromTo), nameof(ShootLinePrefix));
        Patch(typeof(AttackTargetFinder), "CanSee", nameof(AcquisitionPrefix));
        Patch(
            typeof(Verb),
            nameof(Verb.TryStartCastOn),
            nameof(CastPrefix),
            arguments: new[]
            {
                typeof(LocalTargetInfo),
                typeof(LocalTargetInfo),
                typeof(bool),
                typeof(bool),
                typeof(bool),
                typeof(bool),
            }
        );
        Patch(typeof(Verb_MeleeAttack), "TryCastShot", nameof(MeleePrefix));
        Patch(typeof(CoverUtility), nameof(CoverUtility.ThingCovered), nameof(ThingCoveredPrefix));
        Patch(typeof(ShotReport), nameof(ShotReport.HitReportFor), postfix: nameof(ReportPostfix));
        Patch(
            typeof(Projectile),
            nameof(Projectile.Launch),
            postfix: nameof(LaunchPostfix),
            arguments: new[]
            {
                typeof(Thing),
                typeof(Vector3),
                typeof(LocalTargetInfo),
                typeof(LocalTargetInfo),
                typeof(ProjectileHitFlags),
                typeof(bool),
                typeof(Thing),
                typeof(ThingDef),
            }
        );
        Patch(typeof(Projectile), "CanHit", nameof(CanHitPrefix));
        Patch(typeof(Projectile), "CheckForFreeIntercept", nameof(InterceptPrefix));
        Patch(typeof(Projectile), nameof(Projectile.ExposeData), postfix: nameof(ExposePostfix));
        // DrawAt receives native DrawPos. Changing only that argument leaves
        // ExactPosition, interception, explosions and shields in map space.
        Patch(typeof(Projectile), "DrawAt", nameof(DrawPrefix));
        Patch(typeof(Projectile), "DrawShadow", nameof(ShadowPrefix));
        harmony.Patch(
            AccessTools.Method(typeof(Projectile), "DrawAt"),
            transpiler: new HarmonyMethod(typeof(TowerCombat), nameof(DrawTranspiler))
        );
    }

    internal static bool CastPrefix(Verb __instance, ref bool __result)
    {
        var tower = TowerPresentation.TowerFor(__instance.caster);
        if (tower == null || __instance.verbProps.IsMeleeAttack)
            return true;
        if (
            tower.Watching
            && tower.Occupant == __instance.caster
            && FogSettings.TowerShootingAllowed
        )
            return true;
        __result = false;
        return false;
    }

    internal static bool AcquisitionPrefix(
        Thing seer,
        Thing target,
        Func<IntVec3, bool> validator,
        ref bool __result
    )
    {
        var source = TowerPresentation.TowerFor(seer);
        var destination = TowerPresentation.TowerFor(target);
        if (source == null && destination == null)
            return true;
        __result =
            seer?.Map != null
            && target?.Map == seer.Map
            && Visibility.AllowsTarget(seer, target.Position)
            && ClearLine(
                seer.Map,
                seer.Position,
                target.Position,
                PlatformFor(seer, source),
                PlatformFor(target, destination) ?? target as Building_Watchtower,
                Height(seer),
                Height(target),
                validator
            );
        return false;
    }

    internal static bool MeleePrefix(Verb_MeleeAttack __instance, ref bool __result)
    {
        if (Mathf.Abs(Height(__instance.caster) - Height(__instance.CurrentTarget.Thing)) < .5f)
            return true;
        __result = false;
        return false;
    }

    internal static bool ShootLinePrefix(
        Verb __instance,
        IntVec3 root,
        LocalTargetInfo targ,
        out ShootLine resultingLine,
        bool ignoreRange,
        ref bool __result
    )
    {
        resultingLine = default;
        var caster = __instance.caster;
        var source = TowerPresentation.TowerFor(caster);
        var target = TowerPresentation.TowerFor(targ.Thing);
        if (source == null && target == null || !__instance.verbProps.requireLineOfSight)
            return true;
        if (__instance.verbProps.IsMeleeAttack || __instance.EffectiveRange <= 1.42f)
        {
            if (Mathf.Abs(Height(caster) - Height(targ.Thing)) < .5f)
                return true;
            __result = false;
            return false;
        }
        resultingLine = new ShootLine(root, targ.Cell);
        __result =
            caster?.Spawned == true
            && root.InBounds(caster.Map)
            && targ.Cell.InBounds(caster.Map)
            && (targ.Thing == null || targ.Thing.Map == caster.Map)
            && (
                ignoreRange
                || !__instance.OutOfRange(
                    root,
                    targ,
                    targ.Thing?.OccupiedRect() ?? CellRect.SingleCell(targ.Cell)
                )
            )
            && (
                !__instance.verbProps.mustCastOnOpenGround
                || targ.Cell.Standable(caster.Map) && targ.Cell.GetFirstPawn(caster.Map) == null
            )
            && Visibility.AllowsTarget(caster, targ.Cell)
            && ClearLine(
                caster.Map,
                root,
                targ.Cell,
                PlatformFor(caster, source),
                PlatformFor(targ.Thing, target) ?? targ.Thing as Building_Watchtower,
                Height(caster),
                Height(targ.Thing)
            );
        return false;
    }

    internal static bool ClearLine(
        Map map,
        IntVec3 from,
        IntVec3 to,
        Building_Watchtower source,
        Building_Watchtower target,
        float sourceHeight,
        float targetHeight,
        Func<IntVec3, bool> validator = null,
        bool forSight = false
    )
    {
        foreach (var cell in GenSight.PointsOnLineOfSight(from, to))
        {
            if (cell != from && validator != null && !validator(cell))
                return false;
            if (
                cell == from
                || cell == to
                || source?.OccupiedRect().Contains(cell) == true
                || target?.OccupiedRect().Contains(cell) == true
            )
                continue;
            if (cell.Roofed(map))
                return false;
            if (forSight && map.GetVisibility().treeBlockerCells[map.cellIndices.CellToIndex(cell)])
                return false;
            var building = cell.GetEdifice(map);
            if (building == null || building.CanBeSeenOver())
                continue;
            if (
                !MapComponent_Watchtowers.LowWall(building)
                || !TowerGeometry.ClearsLowWall(
                    sourceHeight,
                    targetHeight,
                    ProgressAt(from.ToVector3Shifted(), to.ToVector3Shifted(), cell)
                )
            )
                return false;
        }
        return true;
    }

    private static float ProgressAt(Vector3 from, Vector3 to, IntVec3 cell)
    {
        var direction = (to - from).Yto0();
        return direction.sqrMagnitude < .001f
            ? 1
            : Mathf.Clamp01(
                Vector3.Dot((cell.ToVector3Shifted() - from).Yto0(), direction)
                    / direction.sqrMagnitude
            );
    }

    internal static bool ThingCoveredPrefix(Thing thing, ref bool __result)
    {
        if (
            TowerPresentation.TowerFor(thing)?.ElevationFor(thing) < .999f
            || TowerPresentation.TowerFor(thing) == null
        )
            return true;
        __result = false;
        return false;
    }

    internal static void ReportPostfix(
        Thing caster,
        LocalTargetInfo target,
        ref ShotReport __result
    )
    {
        var sourceTower = TowerPresentation.TowerFor(caster);
        var targetTower = TowerPresentation.TowerFor(target.Thing);
        if (sourceTower == null && targetTower == null)
            return;
        if (
            PlatformFor(caster, sourceTower) != null
            || PlatformFor(target.Thing, targetTower) != null
        )
            coverChance(ref __result) = 0;
        if (targetTower?.ElevationFor(target.Thing) > .999f)
            targetSize(ref __result) = Mathf.Min(2f, targetSize(ref __result) * 1.25f);
    }

    internal static void LaunchPostfix(
        Projectile __instance,
        Thing launcher,
        LocalTargetInfo usedTarget,
        LocalTargetInfo intendedTarget
    )
    {
        var source = TowerPresentation.TowerFor(launcher);
        // A native accuracy miss uses a cell as usedTarget. It still travels at
        // the intended lookout's height, instead of instantly aiming at ground.
        var aim = usedTarget.Thing ?? intendedTarget.Thing;
        var target = TowerPresentation.TowerFor(aim);
        if (source == null && target == null)
            return;
        paths.Remove(__instance);
        paths.Add(
            __instance,
            new ShotPath
            {
                SourceHeight = Height(launcher),
                TargetHeight = Height(aim),
                SourceProjection = TowerPresentation.Offset(launcher),
                TargetProjection = TowerPresentation.Offset(aim),
                // The solid base shields an actor on the exterior ladder.
                // Only a completed platform position can shoot across its footprint.
                SourceTower = PlatformFor(launcher, source),
                TargetTower = PlatformFor(aim, target),
                ProjectRotation = UsesNativeFlightRotation(__instance),
            }
        );
    }

    internal static bool CanHitPrefix(
        Projectile __instance,
        Thing thing,
        Vector3 ___origin,
        Vector3 ___destination,
        ref bool __result
    )
    {
        if (!paths.TryGetValue(__instance, out var path))
            return true;
        float progress = ProgressAt(___origin, ___destination, thing.Position);
        bool ignored =
            thing == path.SourceTower
            || thing == path.TargetTower && thing != __instance.intendedTarget.Thing;
        if (
            !ignored
            && thing is Building building
            && MapComponent_Watchtowers.LowWall(building)
            && !building.Position.Roofed(building.Map)
        )
            ignored = TowerGeometry.ClearsLowWall(path.SourceHeight, path.TargetHeight, progress);
        if (!ignored && thing is Pawn)
            ignored =
                Mathf.Abs(
                    TowerGeometry.Interpolate(path.SourceHeight, path.TargetHeight, progress)
                        - Height(thing)
                ) > 1f;
        if (!ignored)
            return true;
        __result = false;
        return false;
    }

    internal static bool InterceptPrefix(Projectile __instance, IntVec3 c, ref bool __result)
    {
        if (
            !paths.TryGetValue(__instance, out var path)
            || __instance.def.projectile.flyOverhead
            || !c.Roofed(__instance.Map)
            || path.SourceTower?.OccupiedRect().Contains(c) == true
            || path.TargetTower?.OccupiedRect().Contains(c) == true
        )
            return true;
        impact(__instance, c.GetEdifice(__instance.Map), false);
        __result = true;
        return false;
    }

    internal static void DrawPrefix(Projectile __instance, ref Vector3 drawLoc)
    {
        drawLoc += DrawingOffset(__instance);
    }

    internal static Vector3 DrawingOffset(Projectile projectile) =>
        paths.TryGetValue(projectile, out var path)
            ? Vector3.Lerp(
                path.SourceProjection,
                path.TargetProjection,
                distanceCovered(projectile)
            )
            : Vector3.zero;

    internal static void ShadowPrefix(Projectile __instance, ref Vector3 drawLoc) =>
        drawLoc -= DrawingOffset(__instance);

    internal static Quaternion DrawingRotation(Projectile projectile)
    {
        var native = projectile.ExactRotation;
        if (!paths.TryGetValue(projectile, out var path) || !path.ProjectRotation)
            return native;
        var direction =
            native
            * Vector3.forward
            * (destination(projectile) - origin(projectile)).MagnitudeHorizontal();
        direction += (path.TargetProjection - path.SourceProjection).Yto0();
        return direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : native;
    }

    // The mesh points along its projected flight. ExactRotation remains native
    // for simulation/mod consumers; spinning projectiles keep the native override.
    internal static IEnumerable<CodeInstruction> DrawTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var native = AccessTools.PropertyGetter(
            typeof(Projectile),
            nameof(Projectile.ExactRotation)
        );
        if (body.Count(c => c.Calls(native)) != 1)
            throw new InvalidOperationException(
                "Total Fog: projectile drawing must read one native rotation."
            );
        foreach (var code in body)
            if (code.Calls(native))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(TowerCombat), nameof(DrawingRotation));
            }
        return body;
    }

    internal static void ExposePostfix(Projectile __instance)
    {
        bool known = paths.TryGetValue(__instance, out var path);
        if (!known && Scribe.mode == LoadSaveMode.Saving)
            return;
        path ??= new ShotPath();
        Scribe_Values.Look(
            ref path.SourceHeight,
            "totalFogShotSourceHeight",
            TowerGeometry.GroundAimHeight
        );
        Scribe_Values.Look(
            ref path.TargetHeight,
            "totalFogShotTargetHeight",
            TowerGeometry.GroundAimHeight
        );
        Scribe_Values.Look(ref path.SourceProjection, "totalFogShotSourceProjection");
        Scribe_Values.Look(ref path.TargetProjection, "totalFogShotTargetProjection");
        Scribe_References.Look(ref path.SourceTower, "totalFogShotSourceTower");
        Scribe_References.Look(ref path.TargetTower, "totalFogShotTargetTower");
        if (!known && (path.SourceHeight > 1 || path.TargetHeight > 1))
        {
            path.ProjectRotation = UsesNativeFlightRotation(__instance);
            paths.Add(__instance, path);
        }
    }
}
