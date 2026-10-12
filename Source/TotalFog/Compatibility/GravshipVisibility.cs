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
}
