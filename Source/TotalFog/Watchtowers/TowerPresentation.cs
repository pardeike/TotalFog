using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.Watchtowers;

/// <summary>Drawing reads elevation. Simulation always reads native ground coordinates.</summary>
internal static class TowerPresentation
{
    [ThreadStatic]
    internal static int DrawingDepth;

    internal static Building_Watchtower TowerFor(Thing thing) =>
        thing?.MapHeld?.GetVisibility().TowerComponent?.For(thing);

    internal static Vector3 Offset(Thing thing)
    {
        if (thing is AttachableThing attachment && attachment.parent != null)
            thing = attachment.parent;
        else if (thing is MoteAttached attached && attached.link1.Linked)
            thing = attached.link1.Target.Thing;
        return TowerFor(thing)?.OffsetFor(thing) ?? Vector3.zero;
    }

    internal static void AttachmentDrawingPrefix(Thing __instance, ref Vector3 drawLoc)
    {
        if (__instance is AttachableThing)
            drawLoc += Offset(__instance);
    }

    internal static void MotePositionPostfix(Mote __instance, ref Vector3 __result)
    {
        if (DrawingDepth > 0)
            __result +=
                __instance is MoteAttached && __instance is not MoteProgressBar
                    ? Offset(__instance)
                    : TowerEffects.MoteOffset(__instance);
    }

    internal static void PawnDrawingPrefix(Pawn __instance, ref Vector3 drawLoc, out bool __state)
    {
        __state = false;
        drawLoc += Offset(__instance);
        DrawingDepth++;
        __state = true;
    }

    internal static void CorpseDrawingPrefix(
        Corpse __instance,
        ref Vector3 drawLoc,
        out bool __state
    )
    {
        __state = false;
        drawLoc += Offset(__instance);
        DrawingDepth++;
        __state = true;
    }

    internal static void DrawingPrefix(out bool __state)
    {
        DrawingDepth++;
        __state = true;
    }

    internal static Exception DrawingFinalizer(bool __state, Exception __exception)
    {
        if (__state)
            DrawingDepth--;
        return __exception;
    }

    internal static void TrackerDrawPosPostfix(Pawn ___pawn, ref Vector3 __result)
    {
        if (DrawingDepth > 0)
            __result += Offset(___pawn);
    }

    internal static void TrueCenterPostfix(Thing t, ref Vector3 __result)
    {
        // Native pawn centers already read the scoped drawing tracker.
        if (DrawingDepth > 0 && t is not Pawn)
            __result += Offset(t);
    }

    internal static bool AimPiePrefix(
        Thing shooter,
        LocalTargetInfo target,
        int degreesWide,
        float offsetDist
    )
    {
        if (TowerFor(shooter) == null && TowerFor(target.Thing) == null)
            return true;
        var source = shooter.DrawPos;
        var destination = target.HasThing ? target.Thing.DrawPos : target.Cell.ToVector3Shifted();
        if (DrawingDepth == 0)
        {
            source += Offset(shooter);
            if (target.HasThing)
                destination += Offset(target.Thing);
        }
        GenDraw.DrawAimPieRaw(
            source + new Vector3(0, offsetDist, 0),
            (destination - source).AngleFlat(),
            degreesWide
        );
        return false;
    }

    internal static void MouseTargetsPostfix(
        Vector3 clickPos,
        float pawnWideClickRadius,
        TargetingParameters clickParams,
        ITargetingSource source,
        List<Thing> __result
    )
    {
        var component = Find.CurrentMap?.GetVisibility().TowerComponent;
        if (component == null || component.Towers.Count == 0 || __result == null)
            return;
        // Native cell-grid fallback must not select a raised body at its foot.
        __result.RemoveAll(t =>
            TowerFor(t) != null
            && (t.DrawPos - clickPos).MagnitudeHorizontal() > Mathf.Max(.5f, pawnWideClickRadius)
        );
        // Corpses are found through the native cell grid rather than the pawn
        // draw-position loop. Add only a visible, valid projected corpse.
        foreach (var tower in component.Towers)
            if (
                tower.OccupantBody is Corpse corpse
                && corpse.Spawned
                && !__result.Contains(corpse)
                && (corpse.DrawPos - clickPos).MagnitudeHorizontal() < .5f
                && clickParams.CanTarget(corpse, source)
                && Visibility.IsVisible(corpse)
            )
                __result.Insert(0, corpse);
    }

    internal static Vector3 FleckPosition(ref FleckStatic fleck)
    {
        var position = fleck.DrawPos;
        var map = fleck.map ?? Presentation.FleckVisibility.DrawingMap;
        if (
            fleck.def == FleckDefOf.ShotFlash
            && fleck.map == null
            && map != null
            && fleck.spawnPosition.ToIntVec3().GetEdifice(map) is Building_Watchtower tower
            && tower.ProvidesSight
        )
            position += tower.OffsetFor(tower.Occupant);
        return position;
    }

    // The visibility prefix still tests native map coordinates. Only the
    // particle's draw call uses its raised muzzle position.
    internal static IEnumerable<CodeInstruction> FleckTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var native = AccessTools.PropertyGetter(typeof(FleckStatic), nameof(FleckStatic.DrawPos));
        if (body.Count(c => c.Calls(native)) != 1)
            throw new InvalidOperationException(
                "Total Fog: native fleck drawing must have one draw-position call."
            );
        foreach (var code in body)
            if (code.Calls(native))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(TowerPresentation), nameof(FleckPosition));
            }
        return body;
    }

    internal static CellRect CullBounds(Thing thing)
    {
        var bounds = thing.OccupiedDrawRect();
        var offset =
            thing is Projectile projectile ? TowerCombat.DrawingOffset(projectile)
            : thing is Mote mote ? TowerEffects.MoteOffset(mote)
            : Offset(thing);
        if (offset == Vector3.zero)
            return bounds;
        return new CellRect(
            bounds.minX + Mathf.FloorToInt(offset.x),
            bounds.minZ + Mathf.FloorToInt(offset.z),
            bounds.Width + Mathf.CeilToInt(offset.x) - Mathf.FloorToInt(offset.x),
            bounds.Height + Mathf.CeilToInt(offset.z) - Mathf.FloorToInt(offset.z)
        );
    }

    // Move coarse drawing bounds only. Native fog/depth tests keep the logical
    // cell, so a raised sprite neither disappears at the camera edge nor leaks fog.
    internal static IEnumerable<CodeInstruction> CullTranspiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var body = instructions.ToList();
        var native = AccessTools.Method(typeof(GenAdj), nameof(GenAdj.OccupiedDrawRect));
        if (body.Count(c => c.Calls(native)) != 1)
            throw new InvalidOperationException(
                "Total Fog: native culling must have one occupied draw-rect call."
            );
        foreach (var code in body)
            if (code.Calls(native))
            {
                code.opcode = OpCodes.Call;
                code.operand = AccessTools.Method(typeof(TowerPresentation), nameof(CullBounds));
            }
        return body;
    }
}
