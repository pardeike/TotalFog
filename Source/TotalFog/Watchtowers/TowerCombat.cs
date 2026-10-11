using System;
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
    }

    private static readonly ConditionalWeakTable<Projectile, ShotPath> paths = new();
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

    internal static float Height(Thing thing) =>
        TowerGeometry.GroundAimHeight
        + (TowerPresentation.TowerFor(thing)?.Elevation ?? 0)
            * (TowerGeometry.PlatformAimHeight - TowerGeometry.GroundAimHeight);

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
    }

    internal static bool CastPrefix(Verb __instance, ref bool __result)
    {
        var tower = TowerPresentation.TowerFor(__instance.caster);
        if (tower == null || __instance.verbProps.IsMeleeAttack)
            return true;
        if (tower.Watching && FogSettings.TowerShootingAllowed)
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
                source,
                destination,
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
                source,
                target,
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
        Func<IntVec3, bool> validator = null
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
            TowerPresentation.TowerFor(thing)?.Elevation < .999f
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
        coverChance(ref __result) = 0;
        if (targetTower?.Elevation > .999f)
            targetSize(ref __result) = Mathf.Min(2f, targetSize(ref __result) * 1.25f);
    }

    internal static void LaunchPostfix(
        Projectile __instance,
        Thing launcher,
        LocalTargetInfo usedTarget
    )
    {
        var source = TowerPresentation.TowerFor(launcher);
        var target = TowerPresentation.TowerFor(usedTarget.Thing);
        if (source == null && target == null)
            return;
        paths.Remove(__instance);
        paths.Add(
            __instance,
            new ShotPath
            {
                SourceHeight = Height(launcher),
                TargetHeight = Height(usedTarget.Thing),
                SourceProjection = TowerPresentation.Offset(launcher),
                TargetProjection = TowerPresentation.Offset(usedTarget.Thing),
                SourceTower = source,
                TargetTower = target,
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
        if (paths.TryGetValue(__instance, out var path))
            drawLoc += Vector3.Lerp(
                path.SourceProjection,
                path.TargetProjection,
                distanceCovered(__instance)
            );
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
            paths.Add(__instance, path);
    }
}
