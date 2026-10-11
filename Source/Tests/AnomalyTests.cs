using System;
using System.Collections.Generic;
using RimWorld;
using TotalFog.Audio;
using TotalFog.Notifications;
using Verse;
using Xunit;

namespace TotalFog.Tests
{
    public sealed class AnomalyTests : IDisposable
    {
        private readonly bool anomaly = ModsConfig.AnomalyActive;
        private readonly bool suppression = FogSettings.SuppressCombatMusic;
        private readonly EntityCodex codex = Find.EntityCodex;
        private readonly Faction player = Faction.OfPlayer;

        public AnomalyTests()
        {
            ModsConfig.AnomalyActive = true;
            Find.EntityCodex = new();
            AnomalyUtility.NativeDiscoveryAllowed = true;
        }

        public void Dispose()
        {
            ModsConfig.AnomalyActive = anomaly;
            FogSettings.SuppressCombatMusic = suppression;
            Find.EntityCodex = codex;
            Faction.OfPlayer = player;
        }

        [Fact]
        public void ExploredHiddenEntityCannotDiscoverItsCodexEntry()
        {
            var thing = Entity();
            thing.Map.Fog.knownCells[0] = true;
            Assert.False(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
            thing.Map.Fog.InSight[0] = true;
            Assert.True(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
        }

        [Fact]
        public void InitializationDoesNotRevealEntitiesButStartDiscoveredEntriesRemainNative()
        {
            var thing = Entity();
            thing.Map.Fog.Initialized = false;
            Assert.False(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
            thing.def.entityCodexEntry.startDiscovered = true;
            Assert.True(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
            Assert.True(EntityDiscovery.DiscoverPrefix(new(), null));
        }

        [Fact]
        public void FirstObservationRetriesNativeDiscoveryOnceWithoutAPollingQueue()
        {
            var thing = Entity();
            var comp = new CompFog();
            comp.Hiddenable = new() { parent = thing, mainComponent = comp };
            comp.HideFromPlayer = new() { parent = thing, mainComponent = comp };
            thing.Component = comp;
            comp.HideFromPlayer.PostSpawnSetup(false);
            Assert.Equal(0, Find.EntityCodex.Calls);
            thing.Map.Fog.InSight[0] = true;
            comp.HideFromPlayer.UpdateVisibility(true);
            comp.HideFromPlayer.UpdateVisibility(true);
            Assert.Equal(1, Find.EntityCodex.Calls);
            Assert.Same(thing, Find.EntityCodex.LastThing);
            thing.Map.Fog.InSight[0] = false;
            comp.HideFromPlayer.UpdateVisibility(true);
            Assert.Equal(1, Find.EntityCodex.Calls);
        }

        [Fact]
        public void NativeInvisibilityRulesAndDisabledDlcStillControlDiscovery()
        {
            var thing = Entity();
            AnomalyUtility.NativeDiscoveryAllowed = false;
            EntityDiscovery.Observed(thing);
            Assert.Equal(0, Find.EntityCodex.Calls);
            AnomalyUtility.NativeDiscoveryAllowed = true;
            ModsConfig.AnomalyActive = false;
            EntityDiscovery.Observed(thing);
            Assert.Equal(0, Find.EntityCodex.Calls);
        }

        [Fact]
        public void SharedDiscoveryDoesNotDependOnTheLocalPlayerFaction()
        {
            var thing = Entity();
            var other = new Faction { loadID = 2, IsPlayer = true };
            thing.Map.Fog.OtherPlayerFactions.Add(other);
            thing.Map.Fog.FactionSight[other] = new[] { true, false, false, false };
            Assert.True(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
            Faction.OfPlayer = other;
            Assert.True(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
            thing.Map.Fog.FactionSight[other][0] = false;
            Assert.False(EntityDiscovery.DiscoverPrefix(thing.def.entityCodexEntry, thing));
        }

        [Theory]
        [InlineData(typeof(HorrorRelaxTransition))]
        [InlineData(typeof(HorrorTensionTransition))]
        [InlineData(typeof(HorrorMonolithAdvancedTransition))]
        [InlineData(typeof(HorrorCombatTransition))]
        [InlineData(typeof(CorpseAttackTransition))]
        [InlineData(typeof(MetalHellTransition))]
        public void SuppressionBlocksAllNativeAnomalyMusicTransitions(Type type)
        {
            var transition = (MusicTransition)Activator.CreateInstance(type);
            bool result = true;
            FogSettings.SuppressCombatMusic = true;
            Assert.False(MusicVisibility.TransitionPrefix(transition, ref result));
            Assert.False(result);
            Assert.False(MusicVisibility.ForceTransitionPrefix(new() { workerType = type }));
            FogSettings.SuppressCombatMusic = false;
            Assert.True(MusicVisibility.TransitionPrefix(transition, ref result));
            Assert.True(MusicVisibility.ForceTransitionPrefix(new() { workerType = type }));
        }

        [Fact]
        public void SuppressionLeavesOtherMusicTransitionsAloneAndClearsExistingAnomalySequence()
        {
            FogSettings.SuppressCombatMusic = true;
            bool result = true;
            Assert.True(MusicVisibility.TransitionPrefix(new MusicTransition(), ref result));
            Assert.True(result);
            Assert.True(
                MusicVisibility.ForceTransitionPrefix(
                    new() { workerType = typeof(MusicTransition) }
                )
            );
            var manager = new MusicManagerPlay
            {
                TriggeredTransition = new HorrorRelaxTransition(),
            };
            MusicVisibility.CheckTransitionsPrefix(manager);
            Assert.Equal(1, manager.Fades);
            Assert.Null(manager.TriggeredTransition);
            MusicVisibility.CheckTransitionsPrefix(manager);
            Assert.Equal(1, manager.Fades);
        }

        private static ThingWithComps Entity()
        {
            var thing = new ThingWithComps();
            thing.def.entityCodexEntry = new();
            thing.Map.Fog.Initialized = true;
            return thing;
        }
    }
}

namespace RimWorld
{
    public class EntityCodexEntryDef
    {
        public bool startDiscovered;
    }

    public enum EntityDiscoveryType
    {
        Unfog,
    }

    public static class AnomalyUtility
    {
        public static bool NativeDiscoveryAllowed = true;

        public static bool ShouldNotifyCodex(
            Thing thing,
            EntityDiscoveryType type,
            out List<EntityCodexEntryDef> entries
        )
        {
            entries = new() { thing.def.entityCodexEntry };
            return NativeDiscoveryAllowed;
        }
    }

    public class EntityCodex
    {
        public int Calls;
        public Thing LastThing;

        public void SetDiscovered(List<EntityCodexEntryDef> entries, ThingDef def, Thing thing)
        {
            Calls++;
            LastThing = thing;
        }
    }

    public class UnnaturalCorpse : ThingWithComps { }

    public class MusicTransition { }

    public class HorrorRelaxTransition : MusicTransition { }

    public class HorrorTensionTransition : MusicTransition { }

    public class HorrorMonolithAdvancedTransition : MusicTransition { }

    public class HorrorCombatTransition : MusicTransition { }

    public class CorpseAttackTransition : MusicTransition { }

    public class MetalHellTransition : MusicTransition { }

    public class MusicTransitionDef
    {
        public Type workerType;
    }

    public class MusicManagerPlay
    {
        public MusicTransition TriggeredTransition;
        public int Fades;

        public void ForceFadeoutAndSilenceFor(float silence, float fade)
        {
            Fades++;
            TriggeredTransition = null;
        }
    }
}
