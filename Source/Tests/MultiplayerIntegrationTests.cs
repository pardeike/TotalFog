using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TotalFog.Compatibility;
using Verse;
using Xunit;

namespace TotalFog.Tests;

public class MultiplayerIntegrationTests
{
    [Fact]
    public void Bundled_api_without_multiplayer_does_not_register_settings()
    {
        AccessTools.Types.Add("Multiplayer.API.MP", typeof(FakeApi));
        FakeApi.Fields.Clear();
        FakeApi.enabled = false;
        try
        {
            MultiplayerIntegration.Install();
            Assert.Empty(FakeApi.Fields);
            Assert.False(MultiplayerIntegration.Active);
            Assert.False(MultiplayerIntegration.BeginSettingsWatch());
        }
        finally
        {
            FakeApi.enabled = true;
            FakeApi.Fields.Clear();
            AccessTools.Types.Remove("Multiplayer.API.MP");
        }
    }

    [Theory]
    [InlineData(100)]
    [InlineData(41215)]
    public void Observation_deadline_uses_its_map_clock_from_world_or_interface_context(
        int mapTicks
    )
    {
        var previousTicks = Find.TickManager.TicksGame;
        var map = new Map();
        AccessTools.Types.Add("Multiplayer.API.MP", typeof(FakeApi));
        AccessTools.Types.Add(
            "Multiplayer.Client.Factions.FactionExtensions",
            typeof(FakeFactionContext)
        );
        AccessTools.Types.Add("Multiplayer.Client.Extensions", typeof(FakeClockExtensions));
        FakeApi.Fields.Clear();
        FakeApi.Begins = FakeApi.Ends = 0;
        try
        {
            FakeClockExtensions.Clocks.Add(map, new FakeMapClock { mapTicks = mapTicks });
            MultiplayerIntegration.Install();
            FakeApi.IsInMultiplayer = true;
            Find.TickManager.TicksGame = 47445;
            var parent = new ThingWithComps { Map = map };
            var visibility = new CompVisibility { parent = parent, mainComponent = new CompFog() };
            visibility.PostSpawnSetup(false);
            Assert.Equal(
                mapTicks + 12,
                typeof(CompVisibility)
                    .GetField("nextCheck", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(visibility)
            );
        }
        finally
        {
            Find.TickManager.TicksGame = previousTicks;
            FakeApi.IsInMultiplayer = false;
            FakeApi.Fields.Clear();
            FakeClockExtensions.Clocks.Clear();
            AccessTools.Types.Remove("Multiplayer.API.MP");
            AccessTools.Types.Remove("Multiplayer.Client.Factions.FactionExtensions");
            AccessTools.Types.Remove("Multiplayer.Client.Extensions");
        }
    }

    public sealed class FakeMapClock
    {
        public int mapTicks;
    }

    [Fact]
    public void Map_clock_binding_uses_each_map_and_skips_lookup_outside_multiplayer()
    {
        var previousTicks = Find.TickManager.TicksGame;
        var first = new Map();
        var second = new Map();
        AccessTools.Types.Add("Multiplayer.API.MP", typeof(FakeApi));
        AccessTools.Types.Add(
            "Multiplayer.Client.Factions.FactionExtensions",
            typeof(FakeFactionContext)
        );
        AccessTools.Types.Add("Multiplayer.Client.Extensions", typeof(FakeClockExtensions));
        FakeApi.Fields.Clear();
        FakeApi.Begins = FakeApi.Ends = 0;
        try
        {
            FakeClockExtensions.Clocks.Add(first, new FakeMapClock { mapTicks = 36583 });
            FakeClockExtensions.Clocks.Add(second, new FakeMapClock { mapTicks = 41215 });
            FakeClockExtensions.Reads = 0;
            Find.TickManager.TicksGame = 47445;
            MultiplayerIntegration.Install();
            Assert.Equal(47445, MultiplayerIntegration.TicksFor(first));
            Assert.Equal(0, FakeClockExtensions.Reads);
            FakeApi.IsInMultiplayer = true;
            Assert.Equal(36583, MultiplayerIntegration.TicksFor(first));
            Assert.Equal(41215, MultiplayerIntegration.TicksFor(second));
            Assert.Equal(47445, MultiplayerIntegration.TicksFor(null));
            Assert.Equal(47445, MultiplayerIntegration.TicksFor(new Map()));
            FakeApi.IsInMultiplayer = false;
            int reads = FakeClockExtensions.Reads;
            Assert.Equal(47445, MultiplayerIntegration.TicksFor(first));
            Assert.Equal(reads, FakeClockExtensions.Reads);
        }
        finally
        {
            Find.TickManager.TicksGame = previousTicks;
            FakeApi.IsInMultiplayer = false;
            FakeApi.Fields.Clear();
            FakeClockExtensions.Clocks.Clear();
            AccessTools.Types.Remove("Multiplayer.API.MP");
            AccessTools.Types.Remove("Multiplayer.Client.Factions.FactionExtensions");
            AccessTools.Types.Remove("Multiplayer.Client.Extensions");
        }
    }

    public static class FakeClockExtensions
    {
        public static readonly Dictionary<Map, FakeMapClock> Clocks = new();
        public static int Reads;

        public static FakeMapClock AsyncTime(Map map)
        {
            Reads++;
            return Clocks.TryGetValue(map, out var clock) ? clock : null;
        }
    }

    [Fact]
    public void Session_settings_round_trip_exact_float_values_and_startup_tree_policy()
    {
        var original = MultiplayerIntegration.CaptureSettings();
        var originalCulture = CultureInfo.CurrentCulture;
        var trees = typeof(FogSettings).GetField(
            "treesBlockSightValue",
            BindingFlags.Static | BindingFlags.NonPublic
        );
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            FogSettings.BaseViewRange = 10;
            FogSettings.BuildingVisionModifier = 1.23456789f;
            FogSettings.SilentRaids = true;
            trees.SetValue(null, true);
            var saved = MultiplayerIntegration.CaptureSettings();
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            saved.Add("UnknownFutureSetting", "42");
            FogSettings.BaseViewRange = 60;
            FogSettings.BuildingVisionModifier = 1;
            FogSettings.SilentRaids = false;
            trees.SetValue(null, false);
            MultiplayerIntegration.RestoreSettings(saved);
            Assert.Equal(10, FogSettings.BaseViewRange);
            Assert.Equal(1.23456789f, FogSettings.BuildingVisionModifier);
            Assert.True(FogSettings.SilentRaids);
            Assert.True(FogSettings.TreesBlockSight);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            MultiplayerIntegration.RestoreSettings(original);
        }
    }

    [Fact]
    public void Optional_binding_buffers_all_settings_and_balances_watches_after_failure()
    {
        Assert.False(MultiplayerIntegration.Active);
        MultiplayerIntegration.Install();
        Assert.False(MultiplayerIntegration.BeginSettingsWatch());
        AccessTools.Types.Add("Multiplayer.API.MP", typeof(FakeApi));
        AccessTools.Types.Add(
            "Multiplayer.Client.Factions.FactionExtensions",
            typeof(FakeFactionContext)
        );
        AccessTools.Types.Add("Multiplayer.Client.Extensions", typeof(FakeClockExtensions));
        try
        {
            MultiplayerIntegration.Install();
            Assert.False(MultiplayerIntegration.BeginSettingsWatch());
            Assert.Equal(0, FakeApi.Begins);
            var expected = typeof(FogSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field =>
                    field.FieldType.IsPrimitive && !field.IsLiteral && !field.IsInitOnly
                )
                .Select(field => field.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            Assert.Equal(expected, FakeApi.Fields.Select(field => field.Name));
            Assert.All(FakeApi.Fields, field => Assert.True(field.Buffered));
            FakeApi.IsInMultiplayer = true;
            var originalFaction = Faction.OfPlayer;
            var map = new Map();
            var otherFaction = new Faction { loadID = 2, IsPlayer = true };
            bool pushed = MultiplayerIntegration.BeginFactionContext(map, otherFaction);
            try
            {
                Assert.True(pushed);
                Assert.Same(otherFaction, Faction.OfPlayer);
                Assert.False(MultiplayerIntegration.BeginFactionContext(map, otherFaction));
            }
            finally
            {
                MultiplayerIntegration.EndFactionContext(map, pushed);
            }
            Assert.Same(originalFaction, Faction.OfPlayer);
            Assert.Empty(FakeFactionContext.Stack);
            Assert.True(MultiplayerIntegration.BeginSettingsWatch());
            MultiplayerIntegration.EndSettingsWatch(true);
            Assert.Equal(1, FakeApi.Begins);
            Assert.Equal(1, FakeApi.Ends);
            Assert.All(FakeApi.Fields, field => Assert.Equal(1, field.Watches));
            FakeApi.Fields[0].FailWatch = true;
            Assert.Throws<InvalidOperationException>(() =>
                MultiplayerIntegration.BeginSettingsWatch()
            );
            Assert.Equal(2, FakeApi.Begins);
            Assert.Equal(2, FakeApi.Ends);
        }
        finally
        {
            FakeApi.IsInMultiplayer = false;
            AccessTools.Types.Remove("Multiplayer.API.MP");
            AccessTools.Types.Remove("Multiplayer.Client.Factions.FactionExtensions");
            AccessTools.Types.Remove("Multiplayer.Client.Extensions");
        }
    }

    public static class FakeApi
    {
        public static bool enabled = true;
        public static bool IsInMultiplayer { get; set; }
        public static int Begins,
            Ends;
        public static readonly List<FakeField> Fields = new();

        public static FakeField RegisterSyncField(FieldInfo field)
        {
            var handler = new FakeField { Name = field.Name };
            Fields.Add(handler);
            return handler;
        }

        public static void WatchBegin() => Begins++;

        public static void WatchEnd() => Ends++;
    }

    public sealed class FakeField
    {
        public string Name;
        public bool Buffered,
            FailWatch;
        public int Watches;

        public FakeField SetBufferChanges()
        {
            Buffered = true;
            return this;
        }

        public void Watch(object target, object index)
        {
            if (FailWatch)
                throw new InvalidOperationException("Watch failed");
            Watches++;
        }
    }

    public static class FakeFactionContext
    {
        public static readonly Stack<Faction> Stack = new();

        public static void PushFaction(Map map, Faction faction, bool force)
        {
            Stack.Push(Faction.OfPlayer);
            Faction.OfPlayer = faction;
        }

        public static Faction PopFaction(Map map) => Faction.OfPlayer = Stack.Pop();
    }
}
