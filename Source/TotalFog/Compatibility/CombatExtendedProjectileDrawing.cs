using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TotalFog.Core;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>Project CE shots from the raised sprite while retaining native physics and ground shadows.</summary>
internal static class CombatExtendedProjectileDrawing
{
    private sealed class Shot
    {
        public Shot() { }

        internal Vector3 SourceProjection;
        internal Vector3 TargetProjection;
        internal float SourceLift;
        internal float TargetLift;
        internal Vector2 Destination;
        internal bool Rotate;
    }

    private readonly struct Flight
    {
        internal readonly Vector3 Exact;
        internal readonly Vector3 Last;
        internal readonly Vector2 Origin;
        internal readonly Vector2 Destination;
        internal readonly int Tick;
        internal readonly int TruePositionTicks;
        internal readonly object Worker;

        public Flight(
            Vector3 exact,
            Vector3 last,
            Vector2 origin,
            Vector2 destination,
            int tick,
            int truePositionTicks,
            object worker
        )
        {
            Exact = exact;
            Last = last;
            Origin = origin;
            Destination = destination;
            Tick = tick;
            TruePositionTicks = truePositionTicks;
            Worker = worker;
        }
    }

    private static readonly ConditionalWeakTable<Thing, Shot> shots = new();
    private static Func<Thing, Flight> flight;
    private static Func<Thing, Thing> target;
    private static Action<Thing, Vector2> restoreDestination;
    private static Func<object, Vector3, int, int, float, Vector3> nativeProjection;
    private static Type projectileType;
    private static readonly System.Collections.Generic.Dictionary<Type, bool> rotationPolicies =
        new();

    internal static void Install(Harmony harmony)
    {
        projectileType = AccessTools.TypeByName("CombatExtended.ProjectileCE");
        var input = Expression.Parameter(typeof(Thing), "projectile");
        var cast = Expression.Convert(input, projectileType);
        Expression Value(string name) => Expression.PropertyOrField(cast, name);
        flight = Expression
            .Lambda<Func<Thing, Flight>>(
                Expression.New(
                    typeof(Flight).GetConstructors()[0],
                    Value("ExactPosition"),
                    Value("LastPos"),
                    Value("origin"),
                    Value("Destination"),
                    Value("FlightTicks"),
                    Value("ticksToTruePosition"),
                    Expression.Convert(Value("TrajectoryWorker"), typeof(object))
                ),
                input
            )
            .Compile();
        target = Expression
            .Lambda<Func<Thing, Thing>>(Value("intendedTargetThing"), input)
            .Compile();
        var destination = Expression.Parameter(typeof(Vector2), "destination");
        restoreDestination = Expression
            .Lambda<Action<Thing, Vector2>>(
                Expression.Assign(Value("Destination"), destination),
                input,
                destination
            )
            .Compile();
        var worker = Expression.Parameter(typeof(object), "worker");
        var position = Expression.Parameter(typeof(Vector3), "position");
        var tick = Expression.Parameter(typeof(int), "tick");
        var trueTicks = Expression.Parameter(typeof(int), "trueTicks");
        var altitude = Expression.Parameter(typeof(float), "altitude");
        var workerType = Value("TrajectoryWorker").Type;
        nativeProjection = Expression
            .Lambda<Func<object, Vector3, int, int, float, Vector3>>(
                Expression.Call(
                    Expression.Convert(worker, workerType),
                    AccessTools.Method(
                        workerType,
                        "ExactPosToDrawPos",
                        new[] { typeof(Vector3), typeof(int), typeof(int), typeof(float) }
                    ),
                    position,
                    tick,
                    trueTicks,
                    altitude
                ),
                worker,
                position,
                tick,
                trueTicks,
                altitude
            )
            .Compile();
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(
                projectileType,
                "Launch",
                new[]
                {
                    typeof(Thing),
                    typeof(Vector2),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(Thing),
                    typeof(float),
                }
            ),
            postfix: new HarmonyMethod(
                typeof(CombatExtendedProjectileDrawing),
                nameof(LaunchPostfix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(
                projectileType,
                "DrawAt",
                new[] { typeof(Vector3), typeof(bool) }
            ),
            prefix: new HarmonyMethod(typeof(CombatExtendedProjectileDrawing), nameof(DrawPrefix))
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.PropertyGetter(projectileType, "DrawRotation"),
            postfix: new HarmonyMethod(
                typeof(CombatExtendedProjectileDrawing),
                nameof(RotationPostfix)
            )
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.DeclaredMethod(projectileType, "ExposeData"),
            postfix: new HarmonyMethod(
                typeof(CombatExtendedProjectileDrawing),
                nameof(ExposePostfix)
            )
        );
    }

    private static bool Rotate(Thing projectile)
    {
        var type = projectile.GetType();
        if (!rotationPolicies.TryGetValue(type, out bool rotate))
            rotationPolicies[type] = rotate =
                AccessTools.PropertyGetter(type, "DrawRotation").DeclaringType == projectileType;
        return rotate;
    }

    private static void LaunchPostfix(Thing __instance, Thing __0)
    {
        var aim = target(__instance);
        var sourceTower = TowerPresentation.TowerFor(__0);
        var targetTower = TowerPresentation.TowerFor(aim);
        if (sourceTower == null && targetTower == null)
            return;
        var shot = shots.GetOrCreateValue(__instance);
        shot.SourceProjection = TowerPresentation.Offset(__0);
        shot.TargetProjection = TowerPresentation.Offset(aim);
        shot.SourceLift = TowerGeometry.PlatformLift(sourceTower?.ElevationFor(__0) ?? 0f);
        shot.TargetLift = TowerGeometry.PlatformLift(targetTower?.ElevationFor(aim) ?? 0f);
        shot.Rotate = Rotate(__instance);
    }

    private static float Progress(Flight state, Vector3 position)
    {
        var direction = state.Destination - state.Origin;
        return direction.sqrMagnitude < .001f
            ? 1f
            : Mathf.Clamp01(
                Vector2.Dot(new Vector2(position.x, position.z) - state.Origin, direction)
                    / direction.sqrMagnitude
            );
    }

    private static Vector3 Offset(
        Thing projectile,
        Shot shot,
        Flight state,
        Vector3 position,
        int tick
    )
    {
        float progress = Progress(state, position);
        var lowered = position;
        lowered.y -= Mathf.Lerp(shot.SourceLift, shot.TargetLift, progress);
        // CE workers differ: some project height and ease their muzzle start,
        // others stay flat. Ask the same worker how much of our floor it drew.
        var native = nativeProjection(
            state.Worker,
            position,
            tick,
            state.TruePositionTicks,
            projectile.def.Altitude
        );
        var ground = nativeProjection(
            state.Worker,
            lowered,
            tick,
            state.TruePositionTicks,
            projectile.def.Altitude
        );
        return Vector3.Lerp(shot.SourceProjection, shot.TargetProjection, progress)
            - (native - ground);
    }

    internal static Vector3 DrawingOffset(Thing projectile)
    {
        if (
            flight == null
            || projectile.def.category != ThingCategory.Projectile
            || !shots.TryGetValue(projectile, out var shot)
        )
            return Vector3.zero;
        var state = flight(projectile);
        return Offset(projectile, shot, state, state.Exact, state.Tick);
    }

    private static void DrawPrefix(Thing __instance, ref Vector3 __0) =>
        __0 += DrawingOffset(__instance);

    private static void RotationPostfix(Thing __instance, ref Quaternion __result)
    {
        if (!shots.TryGetValue(__instance, out var shot) || !shot.Rotate)
            return;
        var state = flight(__instance);
        if (state.Tick <= 0)
            return;
        var current =
            nativeProjection(
                state.Worker,
                state.Exact,
                state.Tick,
                state.TruePositionTicks,
                __instance.def.Altitude
            ) + Offset(__instance, shot, state, state.Exact, state.Tick);
        var previous =
            nativeProjection(
                state.Worker,
                state.Last,
                state.Tick - 1,
                state.TruePositionTicks,
                __instance.def.Altitude
            ) + Offset(__instance, shot, state, state.Last, state.Tick - 1);
        var direction = (current - previous).Yto0();
        if (direction.sqrMagnitude > .000001f)
            __result = Quaternion.LookRotation(direction);
    }

    private static void ExposePostfix(Thing __instance)
    {
        bool known = shots.TryGetValue(__instance, out var shot);
        if (!known && Scribe.mode == LoadSaveMode.Saving)
            return;
        shot ??= new Shot();
        if (Scribe.mode == LoadSaveMode.Saving)
            shot.Destination = flight(__instance).Destination;
        Scribe_Values.Look(ref shot.SourceProjection, "totalFogCEShotSourceProjection");
        Scribe_Values.Look(ref shot.TargetProjection, "totalFogCEShotTargetProjection");
        Scribe_Values.Look(ref shot.SourceLift, "totalFogCEShotSourceLift");
        Scribe_Values.Look(ref shot.TargetLift, "totalFogCEShotTargetLift");
        // CE calculates its omitted default before restoring gravity on load.
        // For a saved tower shot that can produce infinity and discard it on
        // the first tick. Retain the exact native endpoint; do not recompute it.
        Scribe_Values.Look(ref shot.Destination, "totalFogCEShotDestination", forceSave: true);
        if (!known && (shot.SourceLift > 0 || shot.TargetLift > 0))
        {
            shot.Rotate = Rotate(__instance);
            shots.Add(__instance, shot);
        }
        if (Scribe.mode == LoadSaveMode.LoadingVars && (shot.SourceLift > 0 || shot.TargetLift > 0))
            restoreDestination(__instance, shot.Destination);
    }
}
