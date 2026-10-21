using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace TotalFog.Compatibility;

internal static class GravshipVisibility
{
    private sealed class ControllerCache
    {
        internal WorldComponent_GravshipController Controller;
        internal int ComponentCount;
    }

    private static readonly ConditionalWeakTable<World, ControllerCache> controllers = new();

    private static WorldComponent_GravshipController Controller()
    {
        var world = Find.World;
        if (world == null)
            return null;
        if (Current.ProgramState != ProgramState.Playing)
        {
            controllers.Remove(world);
            return Find.GravshipController;
        }
        var cached = controllers.GetOrCreateValue(world);
        if (cached.Controller == null || cached.ComponentCount != world.components.Count)
        {
            cached.Controller = Find.GravshipController;
            cached.ComponentCount = world.components.Count;
        }
        return cached.Controller;
    }

    internal static bool Revealed =>
        ModsConfig.OdysseyActive
        && FogSettings.ClearFogDuringTargeting
        && (
            WorldComponent_GravshipController.CutsceneInProgress
            || Controller()?.LandingAreaConfirmationInProgress == true
        );

    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.OdysseyActive)
            return;
        harmony.Patch(
            AccessTools.Method(
                typeof(GravshipPlacementUtility),
                nameof(GravshipPlacementUtility.PlaceGravshipInMap)
            ),
            postfix: new HarmonyMethod(typeof(GravshipVisibility), nameof(Placed))
        );
        var method = AccessTools.Method(typeof(WorldComponent_GravshipController), "LandingEnded");
        if (method == null)
        {
            Log.Warning("[Total Fog] Gravship integration: LandingEnded is unavailable.");
            return;
        }
        harmony.Patch(
            method,
            postfix: new HarmonyMethod(typeof(GravshipVisibility), nameof(LandingEnded))
        );
    }

    public static void LandingEnded()
    {
        if (FogSettings.ClearFogDuringTargeting)
            Find.CurrentMap?.mapDrawer?.RegenerateEverythingNow();
    }

    internal static void Placed(Gravship __0, IntVec3 __1, Map __2)
    {
        var owner = __0?.Engine?.Faction;
        if (__0 == null || owner?.IsPlayer != true || __2 == null)
            return;
        var fog = __2.GetVisibility();
        // The carried floor is already the player's ship. Reveal only that
        // footprint, without granting sight into its rooms or the landing map.
        foreach (var rect in __0.OccupiedRects)
        foreach (var local in rect)
        {
            var cell = __1 + local;
            if (!cell.InBounds(__2))
                continue;
            __2.fogGrid.Unfog(cell);
            fog.RevealCell(owner, __2.cellIndices.CellToIndex(cell));
        }
        foreach (var thing in __0.Things)
            if (thing.Spawned && thing.Map == __2)
                thing.TryGetComp<CompFog>()?.HideFromPlayer?.RememberArrival(owner);
    }
}
