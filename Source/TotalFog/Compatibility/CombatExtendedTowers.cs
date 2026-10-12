using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using TotalFog.Core;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>Optional CE height corrections; its native trajectories and damage remain authoritative.</summary>
internal static class CombatExtendedTowers
{
    private static Func<Thing, float> coverTop;
    private static Func<Thing, Bounds> nativeBounds;
    private static MethodInfo boundsMethod;
    private static Func<Verb, ThingDef> projectile;

    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.IsActive("ceteam.combatextended"))
            return;
        string hook = "height bindings";
        try
        {
            var vertical = AccessTools.TypeByName("CombatExtended.CollisionVertical");
            var calculate = AccessTools.DeclaredMethod(
                vertical,
                "CalculateHeightRange",
                new[]
                {
                    typeof(Thing),
                    typeof(FloatRange).MakeByRefType(),
                    typeof(float).MakeByRefType(),
                }
            );
            var utility = AccessTools.TypeByName("CombatExtended.CE_Utility");
            boundsMethod = AccessTools.Method(utility, "GetBoundsFor", new[] { typeof(Thing) });
            var tracker = AccessTools.TypeByName("CombatExtended.LightingTracker");
            var highest = AccessTools.Method(tracker, "HighestCoverAt", new[] { typeof(IntVec3) });
            if (
                calculate == null
                || boundsMethod?.ReturnType != typeof(Bounds)
                || highest?.ReturnType != typeof(float)
            )
                throw new MissingMethodException(
                    "Supported CE height/bounds hooks are unavailable."
                );
            var thing = Expression.Parameter(typeof(Thing), "thing");
            coverTop = Expression
                .Lambda<Func<Thing, float>>(
                    Expression.Property(
                        Expression.New(vertical.GetConstructor(new[] { typeof(Thing) }), thing),
                        "Max"
                    ),
                    thing
                )
                .Compile();
            nativeBounds = AccessTools.MethodDelegate<Func<Thing, Bounds>>(boundsMethod);
            harmony.Patch(
                calculate,
                postfix: new HarmonyMethod(typeof(CombatExtendedTowers), nameof(HeightPostfix))
            );
            harmony.Patch(
                highest,
                transpiler: new HarmonyMethod(
                    typeof(CombatExtendedTowers),
                    nameof(CoverCacheTranspiler)
                )
            );
            var verbType = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            hook = "projectile type binding";
            var verb = Expression.Parameter(typeof(Verb), "verb");
            projectile = Expression
                .Lambda<Func<Verb, ThingDef>>(
                    Expression.Property(Expression.Convert(verb, verbType), "Projectile"),
                    verb
                )
                .Compile();
            hook = "shoot-line hooks";
            var shotArguments = new[]
            {
                typeof(IntVec3),
                typeof(LocalTargetInfo),
                typeof(ShootLine).MakeByRefType(),
                typeof(Vector3).MakeByRefType(),
            };
            foreach (var type in GenTypes.AllTypes)
                if (verbType.IsAssignableFrom(type))
                {
                    var method = AccessTools.DeclaredMethod(
                        type,
                        "TryFindCEShootLineFromTo",
                        shotArguments
                    );
                    if (
                        method != null
                        && !method.IsAbstract
                        && !method.ContainsGenericParameters
                        && !method.IsStatic
                        && method.ReturnType == typeof(bool)
                    )
                        harmony.Patch(
                            method,
                            postfix: new HarmonyMethod(
                                typeof(CombatExtendedTowers),
                                nameof(ShootLinePostfix)
                            )
                            {
                                priority = Priority.Last,
                            }
                        );
                }
            hook = "projectile drawing bindings";
            CombatExtendedProjectileDrawing.Install(harmony);
        }
        catch (Exception exception)
        {
            Log.Warning(
                "[Total Fog] Combat Extended tower integration (" + hook + "): " + exception
            );
        }
    }

    internal static void ShootLinePostfix(
        Verb __instance,
        LocalTargetInfo __1,
        ShootLine __2,
        ref bool __result
    )
    {
        if (__result && !projectile(__instance).projectile.flyOverhead)
            __result = TowerCombat.AllowsNativeShotLine(__instance, __2.Source, __1, __2.Dest);
    }

    internal static void HeightPostfix(Thing __0, ref FloatRange __1, ref float __2)
    {
        var tower = TowerPresentation.TowerFor(__0);
        if (tower == null)
            return;
        // Native CE already adds the cell's cover height. Replace that floor,
        // retaining the pawn's body size, crouch and flying offsets above it.
        var cover = __0.Position.GetCover(__0.MapHeld);
        float nativeFloor =
            cover != null && cover != __0 && !cover.def.IsPlant ? coverTop(cover) : 0f;
        float delta = TowerGeometry.PlatformLift(tower.ElevationFor(__0)) - nativeFloor;
        __1.min += delta;
        __1.max += delta;
        __2 += delta;
    }

    private static float CoverHeight(Thing thing)
    {
        var bounds = nativeBounds(thing);
        // CE's early collision rejection caches body thickness. Raised actors
        // need their upper edge, otherwise arrows skip their cells above y=1.
        return TowerPresentation.TowerFor(thing) != null ? bounds.max.y : bounds.size.y;
    }

    internal static IEnumerable<CodeInstruction> CoverCacheTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        int index = body.FindIndex(c => c.Calls(boundsMethod));
        if (
            index < 0
            || body.Count(c => c.Calls(boundsMethod)) != 1
            || index + 4 >= body.Count
            || !body[index + 1].opcode.Name.StartsWith("stloc", StringComparison.Ordinal)
            || !body[index + 2].opcode.Name.StartsWith("ldloca", StringComparison.Ordinal)
            || !body[index + 3]
                .Calls(AccessTools.PropertyGetter(typeof(Bounds), nameof(Bounds.size)))
            || body[index + 4].opcode != OpCodes.Ldfld
            || !Equals(
                body[index + 4].operand,
                AccessTools.Field(typeof(Vector3), nameof(Vector3.y))
            )
            || body.Skip(index + 1).Take(4).Any(c => c.labels.Count != 0 || c.blocks.Count != 0)
        )
            throw new InvalidOperationException(
                "CE highest-cover cache must have one supported bounds-size read."
            );
        body[index].operand = AccessTools.Method(typeof(CombatExtendedTowers), nameof(CoverHeight));
        body.RemoveRange(index + 1, 4);
        return body;
    }
}
