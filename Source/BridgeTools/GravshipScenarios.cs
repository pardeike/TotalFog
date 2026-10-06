using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class GravshipScenarios
{
    private static int controllerLookups;

    private static void CountLookup() => controllerLookups++;

    [Tool(
        "totalfog/gravship_visibility",
        Description = "Verify native landing-confirmation visibility and compare warm query cost with direct engine lookup, without DPA instrumentation. Restores the marker and setting before returning."
    )]
    public static async Task<object> GravshipVisibilityScenario(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        object result = null;
        await ctx.MainThread.InvokeAsync(
            () =>
            {
                if (
                    !ModsConfig.OdysseyActive
                    || Find.World == null
                    || Find.TickManager?.Paused != true
                )
                    throw new InvalidOperationException(
                        "Load and pause an Odyssey test map first."
                    );
                if (WorldComponent_GravshipController.CutsceneInProgress)
                    throw new InvalidOperationException(
                        "Finish the active gravship cutscene first."
                    );
                var controller = Find.GravshipController;
                var visibilityType = typeof(FogSettings).Assembly.GetType(
                    "TotalFog.Compatibility.GravshipVisibility",
                    true
                );
                var revealed =
                    (Func<bool>)
                        Delegate.CreateDelegate(
                            typeof(Func<bool>),
                            AccessTools.PropertyGetter(visibilityType, "Revealed")
                        );
                var markerField = AccessTools.Field(
                    typeof(WorldComponent_GravshipController),
                    "landingMarker"
                );
                var oldMarker = markerField.GetValue(controller);
                bool oldSetting = FogSettings.ClearFogDuringTargeting;
                var getter = AccessTools.PropertyGetter(
                    typeof(Find),
                    nameof(Find.GravshipController)
                );
                var harmony = new Harmony("brrainz.totalfog.gravship-scenario");
                try
                {
                    FogSettings.ClearFogDuringTargeting = true;
                    controller.Notify_LandingAreaConfirmationStarted(null);
                    bool before = revealed();
                    controllerLookups = 0;
                    harmony.Patch(
                        getter,
                        prefix: new HarmonyMethod(typeof(GravshipScenarios), nameof(CountLookup))
                    );
                    bool warm = false;
                    for (int i = 0; i < 1000; i++)
                        warm |= revealed();
                    int lookups = controllerLookups;
                    controller.Notify_LandingAreaConfirmationStarted(new GravshipLandingMarker());
                    bool during = revealed();
                    FogSettings.ClearFogDuringTargeting = false;
                    bool disabled = revealed();
                    FogSettings.ClearFogDuringTargeting = true;
                    controller.Notify_LandingAreaConfirmationStarted(null);
                    bool after = revealed();
                    harmony.Unpatch(getter, HarmonyPatchType.All, harmony.Id);
                    var queryNanoseconds = new double[3];
                    var directLookupNanoseconds = new double[3];
                    for (int i = 0; i < 3; i++)
                    {
                        if (i % 2 == 0)
                        {
                            directLookupNanoseconds[i] = Measure(DirectLookupRevealed);
                            queryNanoseconds[i] = Measure(revealed);
                        }
                        else
                        {
                            queryNanoseconds[i] = Measure(revealed);
                            directLookupNanoseconds[i] = Measure(DirectLookupRevealed);
                        }
                    }
                    result = new
                    {
                        before,
                        warm,
                        during,
                        disabled,
                        after,
                        warmEngineLookups = lookups,
                        timingIterations = 500000,
                        queryNanoseconds,
                        directLookupNanoseconds,
                        queries = 1000,
                        success = !before && !warm && during && !disabled && !after,
                    };
                }
                finally
                {
                    harmony.Unpatch(getter, HarmonyPatchType.All, harmony.Id);
                    markerField.SetValue(controller, oldMarker);
                    FogSettings.ClearFogDuringTargeting = oldSetting;
                }
            },
            cancellationToken
        );
        return result;
    }

    private static bool DirectLookupRevealed() =>
        ModsConfig.OdysseyActive
        && FogSettings.ClearFogDuringTargeting
        && (
            WorldComponent_GravshipController.CutsceneInProgress
            || Find.GravshipController?.LandingAreaConfirmationInProgress == true
        );

    private static double Measure(Func<bool> query)
    {
        for (int i = 0; i < 1000; i++)
            _ = query();
        var timer = Stopwatch.StartNew();
        bool revealed = false;
        for (int i = 0; i < 500000; i++)
            revealed |= query();
        timer.Stop();
        if (revealed)
            throw new InvalidOperationException("Landing state changed during the query timing.");
        return timer.ElapsedTicks * (1000000000d / Stopwatch.Frequency) / 500000;
    }
}
