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
            Assert.Equal(0, FakeFactionContext.Stack.Count);
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
        }
    }

    public static class FakeApi
    {
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
