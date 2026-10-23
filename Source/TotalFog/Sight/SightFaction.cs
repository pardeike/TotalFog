using RimWorld;
using Verse;

namespace TotalFog;

/// <summary>Which faction's coverage grid an observer's sight feeds.</summary>
internal static class SightFaction
{
    /// <summary>
    /// Faction whose coverage a living pawn's sight feeds. Shared prisoner and
    /// ally sight is published to the host/player grid, so targeting must read
    /// that grid rather than the pawn's own, otherwise empty, faction grid.
    /// </summary>
    internal static Faction For(MapVisibility fog, Pawn pawn, out float modifier)
    {
        modifier = 1;
        if (pawn.IsPrisonerOfColony && FogSettings.PrisonerGiveVision)
        {
            modifier = .2f;
            return pawn.HostFaction;
        }
        var faction = pawn.Faction;
        if (
            faction != null
            && !faction.IsPlayer
            && FogSettings.AllyGiveVision
            && faction.AllyOrNeutralTo(fog.PrimaryPlayerFaction)
        )
        {
            modifier = .5f;
            return fog.PrimaryPlayerFaction;
        }
        return faction;
    }

    internal static Faction For(MapVisibility fog, Thing thing) =>
        thing is Pawn pawn ? For(fog, pawn, out _) : thing?.Faction;
}
