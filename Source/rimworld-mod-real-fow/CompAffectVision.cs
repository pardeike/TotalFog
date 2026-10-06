using System;
using Verse;

namespace RimWorldRealFoW;

public class CompAffectVision : ThingComp
{
    public static readonly Type CompClass = typeof(CompAffectVision);

    public CompProperties_AffectVision Props => (CompProperties_AffectVision)props;
}