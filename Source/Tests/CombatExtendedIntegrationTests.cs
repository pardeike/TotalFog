using System;
using System.Linq;
using HarmonyLib;
using TotalFog.Compatibility;
using UnityEngine;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public sealed class CombatExtendedIntegrationTests : IDisposable
{
    private const string Package = "ceteam.combatextended";
    private const string TypeName = "CombatExtended.Verb_LaunchProjectileCE";

    public CombatExtendedIntegrationTests()
    {
        FogSettings.AISmart = false;
        ModsConfig.Active.Clear(); AccessTools.Types.Clear(); Harmony.Patched.Clear();
        GenTypes.AllTypes = new[] { typeof(CeVerb), typeof(InheritedVerb), typeof(OverrideVerb), typeof(UnrelatedVerb) };
        AccessTools.Types[TypeName] = typeof(CeVerb);
    }

    public void Dispose()
    {
        FogSettings.AISmart = false;
        ModsConfig.Active.Clear(); AccessTools.Types.Clear(); Harmony.Patched.Clear();
        GenTypes.AllTypes = Array.Empty<Type>();
    }

    [Fact]
    public void InactiveIntegrationDoesNotPatchAnything()
    {
        CombatExtendedIntegration.Install(new Harmony());
        Assert.Empty(Harmony.Patched);
    }

    [Fact]
    public void InstallPatchesTheVerifiedOverloadAndDeclaredOverridesOnly()
    {
        ModsConfig.Active.Add(Package);
        CombatExtendedIntegration.Install(new Harmony());
        Assert.Equal(new[] { typeof(CeVerb), typeof(OverrideVerb) }, Harmony.Patched.Select(m => m.DeclaringType));
        Assert.All(Harmony.Patched, m => Assert.Equal(new[] { typeof(Vector3), typeof(IntVec3), typeof(Thing) },
            m.GetParameters().Select(p => p.ParameterType)));
    }

    [Fact]
    public void MissingOptionalTypeDoesNotBreakStartup()
    {
        ModsConfig.Active.Add(Package); AccessTools.Types.Clear();
        CombatExtendedIntegration.Install(new Harmony());
        Assert.Empty(Harmony.Patched);
    }

    private static (CeVerb verb, CompSightSource sight) Shooter(bool player = true)
    {
        var sight = new CompSightSource { Range = 0 };
        var pawn = new Pawn { Faction = player ? Faction.OfPlayer : new Faction() };
        pawn.Component = new CompFog { FieldOfViewWatcher = sight };
        pawn.Map.Fog.Initialized = true;
        return (new CeVerb { caster = pawn }, sight);
    }

    [Fact]
    public void AFailedCeHitCannotBePromotedByVisibleCoverage()
    {
        var (verb, sight) = Shooter(); verb.caster.Map.Fog.InSight[1] = true;
        bool result = false;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result); Assert.Equal(0, sight.Queries);
    }

    [Fact]
    public void CoverageChangesWithinOneTickAreReadLive()
    {
        var (verb, _) = Shooter();
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result);
        verb.caster.Map.Fog.InSight[1] = true; result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.True(result);
        verb.caster.Map.Fog.InSight[1] = false; result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result);
    }

    [Fact]
    public void DisabledEnemyFogPreservesCeAndEnabledEnemyFogRestrictsIt()
    {
        var (verb, _) = Shooter(player: false);
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.True(result);
        FogSettings.AISmart = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result);
    }

    [Fact]
    public void VerbsWithoutLineOfSightKeepTheirNativeResult()
    {
        var (verb, sight) = Shooter(); verb.verbProps.requireLineOfSight = false;
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.True(result); Assert.Equal(0, sight.Queries);
    }

    [Fact]
    public void CeSourceHeightDoesNotChangeTheHorizontalFogOrigin()
    {
        var (verb, sight) = Shooter(); bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(1.2f, 15, .2f), new(1, 1));
        Assert.Equal(new IntVec3(1, 0), sight.LastSource);
        Assert.False(result);
    }

    private class CeVerb : Verse.Verb
    {
        protected virtual bool CanHitCellFromCellIgnoringRange(Vector3 source, IntVec3 target, Thing thing) => true;
        protected bool CanHitCellFromCellIgnoringRange(IntVec3 source, IntVec3 target, bool corners) => true;
    }
    private sealed class InheritedVerb : CeVerb { }
    private sealed class OverrideVerb : CeVerb
    {
        protected override bool CanHitCellFromCellIgnoringRange(Vector3 source, IntVec3 target, Thing thing) => false;
    }
    private sealed class UnrelatedVerb : Verse.Verb
    {
        public bool CanHitCellFromCellIgnoringRange(Vector3 source, IntVec3 target, Thing thing) => true;
    }
}
