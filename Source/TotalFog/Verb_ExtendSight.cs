// Modified by Andreas Pardeike for Total Fog, 2026-10-04: reference-assembly override compatibility.
using Verse;

namespace TotalFog;

public class Verb_ExtendSight : Verb
{
    public override bool TryCastShot()
    {
        return (!currentTarget.HasThing || currentTarget.Thing.Map == caster.Map) && currentTarget.Thing is Pawn &&
               (!verbProps.stopBurstWithoutLos || TryFindShootLineFromTo(caster.Position, currentTarget, out _));
    }
}