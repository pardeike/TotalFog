using System;
using RimWorld;
using Verse;

namespace TotalFog.Compatibility;

internal static class AnimalVision
{
    private static readonly bool servantsEnabled =
        ModLister.GetActiveModWithIdentifier("ThatThing.Mycohazard", true) != null;

    internal static float Modifier(Pawn pawn)
    {
        if (servantsEnabled)
            foreach (var hediff in pawn.health.hediffSet.hediffs)
                if (hediff.def.defName.StartsWith("DE_Servant", StringComparison.Ordinal))
                    return 1;
        if (
            pawn.playerSettings?.Master == null
            || pawn.training?.HasLearned(TrainableDefOf.Release) != true
        )
            return 0;
        return FogSettings.AnimalVisionModifier * Math.Max(pawn.RaceProps.baseBodySize * .7f, .4f);
    }
}
