using Verse;

namespace RimWorldRealFoW;

public class Verb_Look : Verb
{
    protected override bool TryCastShot()
    {
        return (!currentTarget.HasThing || currentTarget.Thing.Map == caster.Map) && currentTarget.Thing is Pawn &&
               (!verbProps.stopBurstWithoutLos || TryFindShootLineFromTo(caster.Position, currentTarget, out _));
    }
}