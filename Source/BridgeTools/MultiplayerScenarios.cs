using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Read-only fingerprints for comparing paused native clients at the same tick.</summary>
public sealed class MultiplayerScenarios
{
    [Tool(
        "totalfog/multiplayer_settings",
        Description = "Exercise the same native Multiplayer settings watcher as Total Fog's UI. Set BaseViewRange (10..100) or SilentRaids (true/false), or save local settings without changing sight. Returns command submission, not proof that the other client applied it."
    )]
    public static Task<object> Settings(
        IRimBridgeContext context,
        string action = "status",
        string name = "BaseViewRange",
        string value = "60"
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            var integration = typeof(FogSettings).Assembly.GetType(
                "TotalFog.Compatibility.MultiplayerIntegration"
            );
            var active = (bool)
                integration
                    .GetProperty("Active", BindingFlags.Static | BindingFlags.NonPublic)
                    .GetValue(null);
            if (action == "status")
                return new { success = true, active };
            if (action == "save-local")
            {
                LoadedModManager.GetMod<TotalFogMod>().WriteSettings();
                return new { success = true, phase = "settings-written" };
            }
            if (action != "set" || !active || Current.Game == null)
                return new
                {
                    success = false,
                    message = "Use status/save-local, or set in a joined Multiplayer game.",
                };
            if (name != "BaseViewRange" && name != "SilentRaids")
                return new
                {
                    success = false,
                    message = "Supported setting names: BaseViewRange, SilentRaids.",
                };
            object parsed;
            if (name == "BaseViewRange")
            {
                if (!int.TryParse(value, out var range) || range < 10 || range > 100)
                    return new { success = false, message = "BaseViewRange must be 10..100." };
                parsed = range;
            }
            else
            {
                if (!bool.TryParse(value, out var silent))
                    return new { success = false, message = "SilentRaids must be true/false." };
                parsed = silent;
            }
            var field = typeof(FogSettings).GetField(name);
            var before = field.GetValue(null);
            var watching = (bool)
                integration
                    .GetMethod("BeginSettingsWatch", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, null);
            if (!watching)
                return new { success = false, message = "Settings watcher is inactive." };
            try
            {
                field.SetValue(null, parsed);
            }
            finally
            {
                integration
                    .GetMethod("EndSettingsWatch", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { true });
            }
            return new
            {
                success = true,
                phase = "native-settings-watch-submitted",
                name,
                before,
                requested = parsed,
                immediateValue = field.GetValue(null),
            };
        });

    [Tool(
        "totalfog/multiplayer_snapshot",
        Description = "Read fog coverage, discovery, observations, blockers, sight sources and settings on the game thread without changing state. Compare clients at the same paused native tick. Deadlines are reported separately because load/rejoin rebuilds transient schedules."
    )]
    public static Task<object> Snapshot(IRimBridgeContext context) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            if (Current.Game == null)
                return new { success = false, message = "Load a game first." };
            return new
            {
                success = true,
                ticks = Find.TickManager.TicksGame,
                faction = Faction.OfPlayer?.loadID,
                settings = typeof(FogSettings)
                    .GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.FieldType.IsPrimitive && !field.IsLiteral)
                    .OrderBy(field => field.Name)
                    .ToDictionary(field => field.Name, field => field.GetValue(null)),
                treesBlockSight = FogSettings.TreesBlockSight,
                maps = Find.Maps.OrderBy(map => map.uniqueID).Select(ReadMap).ToArray(),
            };
        });

    private static object ReadMap(Map map)
    {
        // Do not call GetVisibility/Counts: these can create missing state.
        var fog = map.GetComponent<MapVisibility>();
        if (fog == null)
            return new { map = map.uniqueID, initialized = false };
        var grids = new List<object>();
        var player = Read(fog.Coverage, "playerCounts") as int[];
        if (player != null)
            grids.Add(ReadGrid(0, player));
        var factions = (IDictionary)Read(fog.Coverage, "factions");
        foreach (int key in factions.Keys.Cast<int>().OrderBy(key => key))
            grids.Add(ReadGrid(key, (int[])factions[key]));
        var sources = fog
            .fowWatchers.OrderBy(source => source.parent.thingIDNumber)
            .Select(source => new
            {
                id = source.parent.thingIDNumber,
                cell = source.parent.Position.ToString(),
                faction = (Read(source, "faction") as Faction)?.loadID,
                range = source.LastSightRange,
                nextCheck = Read(source, "nextCheck"),
                nextHearing = Read(source, "nextHearing"),
                lastMovement = Read(source, "lastMovement"),
            })
            .ToArray();
        var things = map
            .listerThings.AllThings.OrderBy(thing => thing.thingIDNumber)
            .Where(thing => thing.TryGetComp<CompFog>()?.HideFromPlayer != null)
            .ToArray();
        return new
        {
            map = map.uniqueID,
            fog.Initialized,
            width = map.Size.x,
            height = map.Size.z,
            knownCount = fog.knownCells.Count(known => known),
            knownHash = Hash(writer =>
            {
                foreach (bool value in fog.knownCells)
                    writer.Write(value);
            }),
            coverage = grids,
            blockerHash = Hash(writer =>
            {
                foreach (bool value in fog.viewBlockerCells)
                    writer.Write(value);
                foreach (bool value in fog.treeBlockerCells)
                    writer.Write(value);
            }),
            observationHash = Hash(writer =>
            {
                foreach (var thing in things)
                {
                    writer.Write(thing.thingIDNumber);
                    writer.Write(thing.TryGetComp<CompFog>().HideFromPlayer.SeenByPlayer);
                }
            }),
            observedThings = things.Count(thing =>
                thing.TryGetComp<CompFog>().HideFromPlayer.SeenByPlayer
            ),
            sourceCount = sources.Length,
            sources,
            sourceOrderHash = Hash(writer =>
            {
                foreach (var source in fog.fowWatchers)
                    writer.Write(source.parent.thingIDNumber);
            }),
            pendingSources = ((IEnumerable)Read(fog, "dirtySources"))
                .Cast<CompSightSource>()
                .Select(source => source.parent.thingIDNumber)
                .ToArray(),
        };
    }

    private static object ReadGrid(int faction, int[] counts) =>
        new
        {
            faction,
            visibleCells = counts.Count(count => count > 0),
            contributions = counts.Sum(count => (long)count),
            hash = Hash(writer =>
            {
                foreach (int count in counts)
                    writer.Write(count);
            }),
        };

    private static object Read(object target, string name)
    {
        var field = target
            .GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
            throw new MissingFieldException(target.GetType().FullName, name);
        return field.GetValue(target);
    }

    private static string Hash(Action<BinaryWriter> write)
    {
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                write(writer);
            using (var hash = SHA256.Create())
                return BitConverter
                    .ToString(hash.ComputeHash(stream.ToArray()))
                    .Replace("-", "")
                    .ToLowerInvariant();
        }
    }
}
