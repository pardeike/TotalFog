// Rewritten for Total Fog by Andreas Pardeike, 2026-10-04.
using RimWorld;
using TotalFog.Core;
using Verse;

namespace TotalFog;

public static class SoundAudibility
{
    /// <summary>
    /// Read-only configured audibility for a real source in a custom sound mixer.
    /// Returns one when audio filtering is disabled. Query on the game thread;
    /// camera position does not supply hearing and no simulation state is changed.
    /// </summary>
    public static float GetAudibilityFactor(TargetInfo maker) =>
        FogSettings.DoAudioCheck || FogSettings.MuteHiddenSounds
            ? GetAudibilityFactor(maker, FogSettings.AudioSourceRange)
            : 1f;

    public static float GetAudibilityFactor(TargetInfo maker, int maxDistance)
    {
        var map = maker.Map;
        var origin = maker.Cell;
        if (map == null || !origin.InBounds(map))
            return 1;
        var fog = map.GetVisibility();
        if (
            !fog.Initialized
            || Presentation.ThingVisibility.Bypass(map)
            || maker.Thing?.Faction == Faction.OfPlayer
            || fog.IsShown(Faction.OfPlayer, origin)
        )
            return 1;
        if (FogSettings.MuteHiddenSounds || maxDistance <= 0)
            return 0;
        float nearest = float.PositiveInfinity;
        // A living listener supplies hearing. Explored cells and cameras do not.
        foreach (var listener in map.mapPawns.AllPawnsSpawned)
        {
            if (listener.Faction != Faction.OfPlayer || listener.Dead)
                continue;
            float hearing = listener.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
            if (hearing <= 0)
                continue;
            float distance = listener.Position.DistanceTo(origin) / hearing;
            if (distance < nearest)
                nearest = distance;
        }
        return VisibilityPolicy.Hearing(nearest, maxDistance, FogSettings.VolumeMufflingModifier);
    }
}
