using System;
using Verse;

namespace TotalFog;

public class CompSightModifier : ThingComp
{
    public static readonly Type CompClass = typeof(CompSightModifier);

    public CompProperties_SightModifier Props => (CompProperties_SightModifier)props;
}