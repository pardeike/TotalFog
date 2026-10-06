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
        ModsConfig.Active.Clear();
        AccessTools.Types.Clear();
        Harmony.Patched.Clear();
        GenTypes.AllTypes = new[]
        {
            typeof(CeVerb),
            typeof(InheritedVerb),
            typeof(OverrideVerb),
            typeof(UnrelatedVerb),
        };
        AccessTools.Types[TypeName] = typeof(CeVerb);
        AccessTools.Types["CombatExtended.Building_TurretGunCE"] = typeof(CeTurret);
    }

    public void Dispose()
    {
        FogSettings.AISmart = false;
        ModsConfig.Active.Clear();
        AccessTools.Types.Clear();
        Harmony.Patched.Clear();
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
        Assert.Equal(
            new[]
            {
                "CanHitCellFromCellIgnoringRange",
                "TryFindCEShootLineFromTo",
                "KeepBurstOnNoShootLine",
                "CanHitCellFromCellIgnoringRange",
                "TryFindCEShootLineFromTo",
                "KeepBurstOnNoShootLine",
                "IsValidTarget",
            },
            Harmony.Patched.Select(m => m.Name)
        );
        Assert.Equal(
            new[]
            {
                typeof(CeVerb),
                typeof(CeVerb),
                typeof(CeVerb),
                typeof(OverrideVerb),
                typeof(OverrideVerb),
                typeof(OverrideVerb),
                typeof(CeTurret),
            },
            Harmony.Patched.Select(m => m.DeclaringType)
        );
        Assert.All(
            Harmony.Patched.Where(m => m.Name == "CanHitCellFromCellIgnoringRange"),
            m =>
                Assert.Equal(
                    new[] { typeof(Vector3), typeof(IntVec3), typeof(Thing) },
                    m.GetParameters().Select(p => p.ParameterType)
                )
        );
        Assert.All(
            Harmony.Patched.Where(m => m.Name == "TryFindCEShootLineFromTo"),
            m =>
                Assert.Equal(
                    new[]
                    {
                        typeof(IntVec3),
                        typeof(LocalTargetInfo),
                        typeof(ShootLine).MakeByRefType(),
                        typeof(Vector3).MakeByRefType(),
                    },
                    m.GetParameters().Select(p => p.ParameterType)
                )
        );
        Assert.Equal(
            new[] { typeof(Thing) },
            Harmony.Patched.Last().GetParameters().Select(p => p.ParameterType)
        );
    }

    [Fact]
    public void MissingOptionalTypeDoesNotBreakStartup()
    {
        ModsConfig.Active.Add(Package);
        AccessTools.Types.Clear();
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
        var (verb, sight) = Shooter();
        verb.caster.Map.Fog.InSight[1] = true;
        bool result = false;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result);
        Assert.Equal(0, sight.Queries);
    }

    [Fact]
    public void CoverageChangesWithinOneTickAreReadLive()
    {
        var (verb, _) = Shooter();
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.False(result);
        verb.caster.Map.Fog.InSight[1] = true;
        result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.True(result);
        verb.caster.Map.Fog.InSight[1] = false;
        result = true;
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
        var (verb, sight) = Shooter();
        verb.verbProps.requireLineOfSight = false;
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 10, .5f), new(1, 0));
        Assert.True(result);
        Assert.Equal(0, sight.Queries);
    }

    [Fact]
    public void CeSourceHeightDoesNotChangeTheHorizontalFogOrigin()
    {
        var (verb, sight) = Shooter();
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(1.2f, 15, .2f), new(1, 1));
        Assert.Equal(new IntVec3(1, 0), sight.LastSource);
        Assert.False(result);
    }

    [Fact]
    public void UnmannedPlayerTurretsRequireCurrentFactionSight()
    {
        var turret = new RimWorld.Building_Turret { Faction = Faction.OfPlayer };
        turret.Map.Fog.Initialized = true;
        var verb = new CeVerb { caster = turret };
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 0, .5f), new(1, 0));
        Assert.False(result);
        turret.Map.Fog.InSight[1] = true;
        result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 0, .5f), new(1, 0));
        Assert.True(result);
        result = false;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 0, .5f), new(1, 0));
        Assert.False(result);
        turret.Map.Fog.InSight[1] = false;
        result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 0, .5f), new(1, 0));
        Assert.False(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EnemyTurretsFollowTheOptionalEnemyFogPolicy(bool enemyFog, bool expected)
    {
        var turret = new RimWorld.Building_Turret { Faction = new Faction() };
        turret.Map.Fog.Initialized = true;
        var verb = new CeVerb { caster = turret };
        FogSettings.AISmart = enemyFog;
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(verb, ref result, new(.5f, 0, .5f), new(1, 0));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void UnknownNonPawnCastersKeepTheirNativePolicy()
    {
        var caster = new Thing { Faction = Faction.OfPlayer };
        caster.Map.Fog.Initialized = true;
        bool result = true;
        CombatExtendedIntegration.HitCellPostfix(
            new CeVerb { caster = caster },
            ref result,
            new(.5f, 0, .5f),
            new(1, 0)
        );
        Assert.True(result);
    }

    [Fact]
    public void MissingOptionalTurretKeepsTheProjectileHooks()
    {
        ModsConfig.Active.Add(Package);
        AccessTools.Types.Remove("CombatExtended.Building_TurretGunCE");
        CombatExtendedIntegration.Install(new Harmony());
        Assert.Equal(
            new[]
            {
                typeof(CeVerb),
                typeof(CeVerb),
                typeof(CeVerb),
                typeof(OverrideVerb),
                typeof(OverrideVerb),
                typeof(OverrideVerb),
            },
            Harmony.Patched.Select(m => m.DeclaringType)
        );
    }

    [Fact]
    public void TurretAcquisitionUsesLiveCoverageWithoutPromotingNativeFailures()
    {
        var turret = new CeTurret { Faction = Faction.OfPlayer };
        turret.Map.Fog.Initialized = true;
        var target = new Thing { Map = turret.Map, PositionHeld = new(1, 0) };
        bool result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        turret.Map.Fog.InSight[1] = true;
        result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
        turret.Map.Fog.InSight[1] = false;
        result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        turret.Map.Fog.InSight[1] = true;
        result = false;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void EnemyTurretAcquisitionRetainsTheOptionalPolicy(bool enemyFog, bool expected)
    {
        var turret = new CeTurret { Faction = new Faction() };
        turret.Map.Fog.Initialized = true;
        var target = new Thing { Map = turret.Map, PositionHeld = new(1, 0) };
        FogSettings.AISmart = enemyFog;
        bool result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ManningPawnFactionControlsAcquisitionRatherThanBuildingOwner()
    {
        var turret = new CeTurret { Faction = Faction.OfPlayer };
        var crew = new Pawn { Faction = new Faction(), Map = turret.Map };
        turret.Component = new RimWorld.CompMannable { ManningPawn = crew };
        turret.Map.Fog.Initialized = true;
        var target = new Thing { Map = turret.Map, PositionHeld = new(1, 0) };
        bool result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
        FogSettings.AISmart = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        turret.Map.Fog.FactionSight[crew.Faction] = new[] { false, true, false, false };
        result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
    }

    [Fact]
    public void MissingMapOrUninitializedFogRetainsNativeAcquisition()
    {
        var turret = new CeTurret { Faction = Faction.OfPlayer };
        var target = new Thing { Map = turret.Map, PositionHeld = new(1, 0) };
        bool result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
        turret.Map = null;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.True(result);
    }

    [Fact]
    public void InvalidNativeTargetsDoNotQuerySight()
    {
        var turret = new CeTurret { Faction = Faction.OfPlayer };
        turret.Map.Fog.Initialized = true;
        var target = new Thing { Map = turret.Map, PositionHeld = new(1, 0) };
        bool result = false;
        CombatExtendedIntegration.TurretTargetPostfix(turret, target, ref result);
        Assert.False(result);
        Assert.Equal(0, turret.Map.Fog.VisibilityQueries);
        result = true;
        CombatExtendedIntegration.TurretTargetPostfix(turret, null, ref result);
        Assert.True(result);
        Assert.Equal(0, turret.Map.Fog.VisibilityQueries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShotLinesStopTrackingUnseenThingsIncludingOverheadWeapons(bool requiresLos)
    {
        var (verb, _) = Shooter();
        verb.verbProps.requireLineOfSight = requiresLos;
        var target = new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) };
        bool result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(target),
            ref result,
            new ShootLine(default, target.Position)
        );
        Assert.False(result);
        verb.caster.Map.Fog.InSight[1] = true;
        result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(target),
            ref result,
            new ShootLine(default, target.Position)
        );
        Assert.True(result);
        verb.caster.Map.Fog.InSight[1] = false;
        result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(target),
            ref result,
            new ShootLine(default, target.Position)
        );
        Assert.False(result);
    }

    [Fact]
    public void FailedNativeShotLinesDoNotQueryOrPromoteVisibility()
    {
        var (verb, _) = Shooter();
        verb.caster.Map.Fog.InSight[1] = true;
        var target = new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) };
        bool result = false;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(target),
            ref result,
            new ShootLine(default, target.Position)
        );
        Assert.False(result);
        Assert.Equal(0, verb.caster.Map.Fog.VisibilityQueries);
    }

    [Fact]
    public void BlindCellOrdersAndNativeLockedCellFallbackAreUnrestricted()
    {
        var (verb, _) = Shooter();
        var cell = new LocalTargetInfo(new IntVec3(1, 0));
        bool result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            cell,
            ref result,
            new ShootLine(default, cell.Cell)
        );
        Assert.True(result);
        // CE has already replaced the lost Thing with its last known cell.
        verb.CurrentTarget = cell;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
        Assert.Equal(0, verb.caster.Map.Fog.VisibilityQueries);
    }

    [Fact]
    public void SuppressiveFallbackCannotKeepTrackingAHiddenThing()
    {
        var (verb, _) = Shooter();
        verb.CurrentTarget = new LocalTargetInfo(
            new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) }
        );
        bool result = true;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.False(result);
        verb.caster.Map.Fog.InSight[1] = true;
        result = true;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
        verb.caster.Map.Fog.InSight[1] = false;
        result = true;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.False(result);
    }

    [Fact]
    public void FailedNativeBurstFallbackDoesNotQuerySight()
    {
        var (verb, _) = Shooter();
        verb.caster.Map.Fog.InSight[1] = true;
        verb.CurrentTarget = new LocalTargetInfo(
            new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) }
        );
        bool result = false;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.False(result);
        Assert.Equal(0, verb.caster.Map.Fog.VisibilityQueries);
    }

    [Fact]
    public void NativeRetargetUsesTheNewShotLineTarget()
    {
        var (verb, _) = Shooter();
        var hidden = new Thing { Map = verb.caster.Map, PositionHeld = new(0, 1) };
        var visible = new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) };
        verb.CurrentTarget = new LocalTargetInfo(hidden);
        verb.caster.Map.Fog.InSight[1] = true;
        bool result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(visible),
            ref result,
            new ShootLine(default, visible.Position)
        );
        Assert.True(result);
        verb.CurrentTarget = new LocalTargetInfo(visible);
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            new LocalTargetInfo(hidden),
            ref result,
            new ShootLine(default, hidden.Position)
        );
        Assert.False(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ShotAndFallbackUseCrewFactionAndOptionalEnemyFog(bool enemyFog, bool expected)
    {
        var turret = new CeTurret { Faction = Faction.OfPlayer };
        var crew = new Pawn { Faction = new Faction(), Map = turret.Map };
        turret.Component = new RimWorld.CompMannable { ManningPawn = crew };
        turret.Map.Fog.Initialized = true;
        FogSettings.AISmart = enemyFog;
        var verb = new CeVerb
        {
            caster = turret,
            CurrentTarget = new LocalTargetInfo(
                new Thing { Map = turret.Map, PositionHeld = new(1, 0) }
            ),
        };
        bool result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            verb.CurrentTarget,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.Equal(expected, result);
        result = true;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.Equal(expected, result);
        turret.Map.Fog.FactionSight[crew.Faction] = new[] { false, true, false, false };
        result = true;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
    }

    [Fact]
    public void IncompleteFogOrMapRetainsNativeShotAndFallback()
    {
        var verb = new CeVerb { caster = new Pawn { Faction = Faction.OfPlayer } };
        verb.CurrentTarget = new LocalTargetInfo(
            new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) }
        );
        bool result = true;
        CombatExtendedIntegration.ShootLinePostfix(
            verb,
            verb.CurrentTarget,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
        verb.caster.Map = null;
        CombatExtendedIntegration.BurstFallbackPostfix(
            verb,
            ref result,
            new ShootLine(default, verb.CurrentTarget.Cell)
        );
        Assert.True(result);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void NativeLogicalShotAndFallbackUseTheirChosenCell(bool rootVisible, bool shotVisible)
    {
        var (verb, _) = Shooter();
        var target = new Thing { Map = verb.caster.Map, PositionHeld = new(1, 0) };
        verb.CurrentTarget = new LocalTargetInfo(target);
        verb.caster.Map.Fog.InSight[1] = rootVisible;
        verb.caster.Map.Fog.InSight[2] = shotVisible;
        var line = new ShootLine(default, new IntVec3(0, 1));
        bool shot = true,
            fallback = true;
        CombatExtendedIntegration.ShootLinePostfix(verb, verb.CurrentTarget, ref shot, line);
        CombatExtendedIntegration.BurstFallbackPostfix(verb, ref fallback, line);
        Assert.Equal(shotVisible, shot);
        Assert.Equal(shotVisible, fallback);
    }

    private sealed class CeTurret : RimWorld.Building_Turret
    {
        private bool IsValidTarget(Thing target) => true;
    }

    private class CeVerb : Verse.Verb
    {
        protected virtual bool CanHitCellFromCellIgnoringRange(
            Vector3 source,
            IntVec3 target,
            Thing thing
        ) => true;

        protected bool CanHitCellFromCellIgnoringRange(
            IntVec3 source,
            IntVec3 target,
            bool corners
        ) => true;

        public virtual bool TryFindCEShootLineFromTo(
            IntVec3 source,
            LocalTargetInfo target,
            out ShootLine line,
            out Vector3 targetPosition
        )
        {
            line = default;
            targetPosition = default;
            return true;
        }

        public bool TryFindCEShootLineFromTo(
            IntVec3 source,
            LocalTargetInfo target,
            out ShootLine line
        )
        {
            line = default;
            return true;
        }

        protected virtual bool KeepBurstOnNoShootLine(bool suppressing, out ShootLine line)
        {
            line = default;
            return true;
        }
    }

    private sealed class InheritedVerb : CeVerb { }

    private sealed class OverrideVerb : CeVerb
    {
        protected override bool CanHitCellFromCellIgnoringRange(
            Vector3 source,
            IntVec3 target,
            Thing thing
        ) => false;

        public override bool TryFindCEShootLineFromTo(
            IntVec3 source,
            LocalTargetInfo target,
            out ShootLine line,
            out Vector3 targetPosition
        )
        {
            line = default;
            targetPosition = default;
            return false;
        }

        protected override bool KeepBurstOnNoShootLine(bool suppressing, out ShootLine line)
        {
            line = default;
            return false;
        }
    }

    private sealed class UnrelatedVerb : Verse.Verb
    {
        public bool CanHitCellFromCellIgnoringRange(Vector3 source, IntVec3 target, Thing thing) =>
            true;
    }
}
