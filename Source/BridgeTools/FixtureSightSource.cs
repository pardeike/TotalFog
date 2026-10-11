using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Fixture-only sight component on a remaining, saveable native building.</summary>
internal static class FixtureSightSource
{
    internal const string DefName = "WatchTelescope";

    internal static ThingWithComps Create()
    {
        var thing = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed(DefName));
        Configure(thing);
        return thing;
    }

    // Per-instance instrumentation never changes the public definition. Saved
    // fixtures call this again after loading, before checking their sight.
    internal static void Configure(ThingWithComps thing)
    {
        if (thing.TryGetComp<CompBuildingSight>() == null)
            thing.AllComps.Add(
                new CompBuildingSight
                {
                    parent = thing,
                    props = new CompProperties_BuildingSight { viewRadius = 3 },
                }
            );
        thing.TryGetComp<CompSightModifier>().props = new CompProperties_SightModifier
        {
            fovMultiplier = 1,
        };
        if (thing.Spawned)
            thing.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
    }
}
