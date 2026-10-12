using RimWorld;
using Verse;
using Xunit;
using FogVerb = TotalFog.Detours.Verb;

namespace TotalFog.Tests;

public sealed class MortarVisibilityTests
{
    [Fact]
    public void Overhead_thing_targets_need_current_faction_sight_but_blind_cells_remain_native()
    {
        var caster = new Building_TurretGun { Faction = Faction.OfPlayer };
        caster.Map.Fog.Initialized = true;
        var target = new Thing { Map = caster.Map, PositionHeld = new IntVec3(1, 0) };
        var verb = new Verb { caster = caster, FliesOverhead = true };
        verb.verbProps.requireLineOfSight = false;
        caster.Map.Fog.knownCells[1] = true;
        bool result = true;
        FogVerb.TryFindShootLineFromTo_Postfix(verb, new LocalTargetInfo(target), ref result);
        Assert.False(result);
        result = true;
        FogVerb.TryFindShootLineFromTo_Postfix(
            verb,
            new LocalTargetInfo(new IntVec3(1, 0)),
            ref result
        );
        Assert.True(result);
        caster.Map.Fog.InSight[1] = true;
        result = true;
        FogVerb.TryFindShootLineFromTo_Postfix(verb, new LocalTargetInfo(target), ref result);
        Assert.True(result);
        result = false;
        FogVerb.TryFindShootLineFromTo_Postfix(verb, new LocalTargetInfo(target), ref result);
        Assert.False(result);
    }

    [Fact]
    public void Automatic_turret_candidates_require_sight_without_promoting_native_failures()
    {
        var turret = new Building_TurretGun { Faction = Faction.OfPlayer };
        var target = new Thing { Map = turret.Map, PositionHeld = new IntVec3(1, 0) };
        bool result = true;
        FogVerb.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        turret.Map.Fog.InSight[1] = true;
        result = true;
        FogVerb.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
        result = false;
        FogVerb.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        turret.Faction = new Faction();
        turret.Map.Fog.InSight[1] = false;
        result = true;
        FogVerb.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
    }
}
