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
        "totalfog/multiplayer_cell",
        Description = "Read one cell's native building/door state, fog blockers and observer-specific discovery/sight without changing simulation. Negative mapId/factionId use the current map/viewer. Compare at matching paused native map clocks."
    )]
    public static Task<object> Cell(
        IRimBridgeContext context,
        int x,
        int z,
        int mapId = -1,
        int factionId = -1
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            var map =
                mapId < 0 ? Find.CurrentMap : Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            var observer =
                factionId < 0
                    ? Faction.OfPlayer
                    : Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f =>
                        f.IsPlayer && f.loadID == factionId
                    );
            if (observer == null)
                return new
                {
                    success = false,
                    message = "Use an existing player observer faction.",
                };
            var cell = new IntVec3(x, 0, z);
            var fog = map?.GetComponent<MapVisibility>();
            if (fog == null || !cell.InBounds(map))
                return new { success = false, message = "Use an in-bounds cell on a loaded map." };
            int index = map.cellIndices.CellToIndex(cell);
            var building = cell.GetEdifice(map);
            return new
            {
                success = true,
                ticks = Find.TickManager.TicksGame,
                map = map.uniqueID,
                faction = observer.loadID,
                x,
                z,
                initialized = fog.Initialized,
                blocked = fog.viewBlockerCells[index],
                treeBlocked = fog.treeBlockerCells[index],
                known = StoredKnown(fog, observer)?[index] == true,
                visible = fog.Initialized && fog.IsShown(observer, cell),
                vanillaFog = map.fogGrid.IsFogged(cell),
                building = building == null
                    ? null
                    : new
                    {
                        id = building.ThingID,
                        def = building.def.defName,
                        building.HitPoints,
                        building.def.blockLight,
                        canBeSeenOver = building.CanBeSeenOver(),
                        doorOpen = (building as Building_Door)?.Open,
                    },
            };
        });

    [Tool(
        "totalfog/multiplayer_pawns",
        Description = "Read native pawns or corpse inner pawns on the requested map by comma-separated ThingIDs. A negative mapId uses the current map. Reports combat jobs, targets, health and the local viewer's fog visibility without issuing orders or changing simulation. Compare only at matching paused native map clocks."
    )]
    public static Task<object> Pawns(IRimBridgeContext context, string thingIds, int mapId = -1) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            var ids = thingIds
                .Split(',')
                .Select(id => id.Trim())
                .Where(id => id.Length > 0)
                .ToArray();
            var map =
                mapId < 0 ? Find.CurrentMap : Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            if (ids.Length == 0 || ids.Length > 8 || map == null)
                return new
                {
                    success = false,
                    message = "Use 1..8 comma-separated ThingIDs on a loaded map.",
                };
            var pawns = map
                .mapPawns.AllPawns.Concat(
                    map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                        .OfType<Corpse>()
                        .Select(corpse => corpse.InnerPawn)
                )
                .Distinct()
                .ToDictionary(pawn => pawn.ThingID);
            return new
            {
                success = true,
                ticks = Find.TickManager.TicksGame,
                map = map.uniqueID,
                pawns = ids.Select(id =>
                        pawns.TryGetValue(id, out var pawn)
                            ? ReadPawn(pawn)
                            : new { id, found = false }
                    )
                    .ToArray(),
            };
        });

    private static object ReadPawn(Pawn pawn) =>
        new
        {
            id = pawn.ThingID,
            found = true,
            position = pawn.PositionHeld.ToString(),
            faction = pawn.Faction?.loadID,
            pawn.Dead,
            pawn.Downed,
            drafted = pawn.drafter?.Drafted,
            visible = Visibility.IsVisible(pawn),
            weapon = pawn.equipment?.Primary?.def.defName,
            health = pawn.health.summaryHealth.SummaryHealthPercent,
            hediffs = pawn
                .health.hediffSet.hediffs.Select(hediff => new
                {
                    def = hediff.def.defName,
                    part = hediff.Part?.def.defName,
                    hediff.Severity,
                })
                .ToArray(),
            job = pawn.CurJob?.def.defName,
            jobId = pawn.CurJob?.loadID,
            targetA = ReadTarget(pawn.CurJob?.targetA ?? LocalTargetInfo.Invalid),
            targetB = ReadTarget(pawn.CurJob?.targetB ?? LocalTargetInfo.Invalid),
        };

    private static object ReadTarget(LocalTargetInfo target) =>
        new
        {
            valid = target.IsValid,
            thing = target.Thing?.ThingID,
            cell = target.Cell.ToString(),
        };

    [Tool(
        "totalfog/multiplayer_settings",
        Description = "Exercise the same native Multiplayer settings watcher as Total Fog's UI. Set BaseViewRange (10..100) or any public boolean FogSettings field (true/false), or save local settings without changing sight. Read names from multiplayer_snapshot. Returns command submission, not proof that the other client applied it."
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
            var field = typeof(FogSettings).GetField(
                name,
                BindingFlags.Public | BindingFlags.Static
            );
            if (
                name != "BaseViewRange"
                && (
                    field == null
                    || field.FieldType != typeof(bool)
                    || field.IsLiteral
                    || field.IsInitOnly
                )
            )
                return new
                {
                    success = false,
                    message = "Use BaseViewRange or a public boolean FogSettings field from multiplayer_snapshot.",
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
                if (!bool.TryParse(value, out var boolean))
                    return new
                    {
                        success = false,
                        message = "Boolean settings must be true/false.",
                    };
                parsed = boolean;
            }
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
            discoveryByFaction = Find
                .FactionManager.AllFactionsListForReading.Where(faction => faction.IsPlayer)
                .OrderBy(faction => faction.loadID)
                .Select(faction => new
                {
                    faction = faction.loadID,
                    knownCount = StoredKnown(fog, faction)?.Count(value => value) ?? 0,
                    observedThings = things.Count(thing =>
                        thing.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(faction)
                    ),
                    knownHash = Hash(writer =>
                    {
                        var known = StoredKnown(fog, faction);
                        if (known != null)
                            foreach (bool value in known)
                                writer.Write(value);
                    }),
                    observationHash = Hash(writer =>
                    {
                        foreach (var thing in things)
                        {
                            writer.Write(thing.thingIDNumber);
                            writer.Write(
                                thing.TryGetComp<CompFog>().HideFromPlayer.WasSeenBy(faction)
                            );
                        }
                    }),
                })
                .ToArray(),
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

    private static bool[] StoredKnown(MapVisibility fog, Faction faction)
    {
        if (faction == null)
            return null;
        if ((int)Read(fog, "primaryPlayerFactionId") == faction.loadID)
            return fog.knownCells;
        var discovery = (Dictionary<int, bool[]>)Read(fog, "factionDiscovery");
        return discovery.TryGetValue(faction.loadID, out var known) ? known : null;
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
