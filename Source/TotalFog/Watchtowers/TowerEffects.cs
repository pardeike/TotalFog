using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Watchtowers;

/// <summary>Native effects keep their physics and fog cells, with a separate drawing projection.</summary>
internal static class TowerEffects
{
    internal struct Context
    {
        internal Map Map;
        internal Thing Actor;
        internal Vector3 Offset;
    }

    internal struct Scope
    {
        internal Context Previous;
        internal bool Entered;
    }

    private sealed class MoteProjection
    {
        internal Vector3 Offset;
    }

    [ThreadStatic]
    private static Context current;
    private static readonly ConditionalWeakTable<Mote, MoteProjection> looseMotes = new();

    internal static void Install(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
            prefix: new HarmonyMethod(typeof(TowerEffects), nameof(DamagePrefix)),
            finalizer: new HarmonyMethod(typeof(TowerEffects), nameof(EndScope))
        );
        harmony.Patch(
            AccessTools.Method(typeof(SubEffecter_Sprayer), "MakeMote"),
            prefix: new HarmonyMethod(typeof(TowerEffects), nameof(SprayerPrefix)),
            finalizer: new HarmonyMethod(typeof(TowerEffects), nameof(EndScope))
        );
        harmony.Patch(
            AccessTools.Method(typeof(FleckStatic), nameof(FleckStatic.Setup)),
            postfix: new HarmonyMethod(typeof(TowerEffects), nameof(FleckSetupPostfix))
        );
        harmony.Patch(
            AccessTools.Method(typeof(FleckThrown), "NextPosition"),
            postfix: new HarmonyMethod(typeof(TowerEffects), nameof(NextPositionPostfix))
        );
        harmony.Patch(
            AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing)),
            postfix: new HarmonyMethod(typeof(TowerEffects), nameof(MoteCreationPostfix))
        );
    }

    internal static void DamagePrefix(Thing __instance, out Scope __state)
    {
        Enter(
            new Context
            {
                Map = __instance.MapHeld,
                Actor = __instance,
                Offset = __instance is Pawn or Corpse
                    ? TowerPresentation.Offset(__instance)
                    : Vector3.zero,
            },
            out __state
        );
    }

    internal static void SprayerPrefix(
        SubEffecter_Sprayer __instance,
        TargetInfo A,
        TargetInfo B,
        out Scope __state
    )
    {
        var a = OffsetFor(A);
        var b = OffsetFor(B);
        var offset = Vector3.zero;
        switch (__instance.EffectiveSpawnLocType)
        {
            case MoteSpawnLocType.OnSource:
                offset = a;
                break;
            case MoteSpawnLocType.OnTarget:
            case MoteSpawnLocType.RandomCellOnTarget:
            case MoteSpawnLocType.RandomDrawPosOnTarget:
                offset = b;
                break;
            case MoteSpawnLocType.BetweenPositions:
                offset =
                    A.HasThing && !A.Thing.Spawned ? b
                    : B.HasThing && !B.Thing.Spawned ? a
                    : Vector3.LerpUnclamped(b, a, __instance.def.positionLerpFactor);
                break;
            case MoteSpawnLocType.BetweenTouchingCells:
                float distance = (B.Cell - A.Cell).ToVector3().magnitude;
                offset = Vector3.Lerp(a, b, distance < .001f ? 0 : .5f / distance);
                break;
        }
        Enter(new Context { Map = A.Map ?? B.Map, Offset = offset }, out __state);
    }

    private static Vector3 OffsetFor(TargetInfo target) =>
        target.HasThing ? TowerPresentation.Offset(target.Thing)
        : current.Actor != null
        && target.Map == current.Map
        && target.Cell == current.Actor.PositionHeld
            ? current.Offset
        : Vector3.zero;

    private static void Enter(Context context, out Scope state)
    {
        state = new Scope { Previous = current, Entered = true };
        current = context;
    }

    internal static Exception EndScope(Scope __state, Exception __exception)
    {
        if (__state.Entered)
            current = __state.Previous;
        return __exception;
    }

    internal static void FleckSetupPostfix(ref FleckStatic __instance)
    {
        if (current.Offset == Vector3.zero)
            return;
        // The engine's otherwise unassigned owner field identifies these
        // particles. Store the bonus in native drawing storage, not worldPosition.
        __instance.map = current.Map;
        __instance.position.unattachedDrawOffset += current.Offset;
    }

    internal static void NextPositionPostfix(
        ref FleckThrown __instance,
        ref FleckDrawPosition __result
    )
    {
        var data = __instance.baseData;
        if (data.map == null)
            return;
        var offset = data.position.unattachedDrawOffset - data.def.unattachedDrawOffset;
        var link = __instance.link;
        if (
            link.Target.HasThing
            && !link.Target.ThingDestroyed
            && (link.detachAfterTicks == -1 || data.ageTicks < link.detachAfterTicks)
        )
            offset = TowerPresentation.Offset(link.Target.Thing);
        // Native NextPosition reconstructs the struct each tick. Carry only
        // the bonus drawing offset forward; velocity, collision and arc stay native.
        __result.unattachedDrawOffset += offset;
    }

    internal static void MoteCreationPostfix(Thing __result)
    {
        if (
            current.Offset != Vector3.zero
            && __result is Mote mote
            && mote is not (MoteAttached or MoteProgressBar or MoteBubble)
        )
            looseMotes.Add(mote, new MoteProjection { Offset = current.Offset });
    }

    internal static Vector3 MoteOffset(Mote mote) =>
        looseMotes.TryGetValue(mote, out var projection) ? projection.Offset : Vector3.zero;
}
