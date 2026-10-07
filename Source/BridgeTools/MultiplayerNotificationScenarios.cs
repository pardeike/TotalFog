using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Opt-in native synchronized notification probe, excluded from player packages.</summary>
public sealed class MultiplayerNotificationScenarios
{
    private static object syncMethod;
    private static Func<object, object[], bool> submit;
    private static object worldSyncMethod;
    private static Func<object, object[], bool> submitWorld;
    private static object lastWorldEnqueue;
    private static Action<Map, Faction, bool> push;
    private static Func<Map, Faction> pop;
    private const string Prefix = "Total Fog MP probe ";

    [Tool(
        "totalfog/multiplayer_notifications",
        Description = "Opt-in native Multiplayer notification probe. Register on BOTH main menus before loading/hosting; appends a handler without changing existing IDs. Queue submits a real message/letter in map context. For world-clock controls, register-world THIRD after notifications and caravan on BOTH main menus, then queue-world. World commands serialize primitive IDs, preserving the world clock while addressing a map. Status reads queues, per-faction archives and the last world enqueue clock. Probe saves require the same handlers; excluded from player ZIPs."
    )]
    public static Task<object> Notifications(
        IRimBridgeContext context,
        string action = "status",
        int mapId = 0,
        int factionId = 16,
        int x = 2,
        int z = 2,
        string label = "A",
        string kind = "message",
        bool historical = true,
        int delayTicks = 0
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            if (action == "register")
                return Register();
            if (action == "register-world")
            {
                Register();
                if (worldSyncMethod == null)
                    worldSyncMethod = MultiplayerProbeRegistration.Register(
                        typeof(MultiplayerNotificationScenarios).GetMethod(
                            nameof(CreateWorldNotification),
                            BindingFlags.NonPublic | BindingFlags.Static
                        ),
                        out submitWorld
                    );
                return new
                {
                    success = true,
                    registered = true,
                    existingCommandIdsPreserved = true,
                    syncId = MultiplayerProbeRegistration.Id(worldSyncMethod),
                };
            }
            if (action == "status")
                return Status();
            bool worldCommand = action == "queue-world";
            var sender = worldCommand ? submitWorld : submit;
            if ((action != "queue" && !worldCommand) || sender == null || Current.Game == null)
                throw new InvalidOperationException(
                    "Register the requested handler on both main menus, then use queue/queue-world/status in a live game."
                );
            var api = AccessTools.TypeByName("Multiplayer.Client.Multiplayer");
            var session = AccessTools.Field(api, "session").GetValue(null);
            if (
                session == null
                || AccessTools.Field(session.GetType(), "desynced").GetValue(session) is not false
                || AccessTools.Property(api, "IsReplay").GetValue(null) is not false
            )
                throw new InvalidOperationException(
                    "Use a live, non-desynced Multiplayer session."
                );
            var map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            var faction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f =>
                f.IsPlayer && f.loadID == factionId
            );
            if (map == null || faction == null || !new IntVec3(x, 0, z).InBounds(map))
                throw new ArgumentException(
                    "Use an existing map/player faction and in-bounds cell."
                );
            if (
                string.IsNullOrEmpty(label)
                || label.Length > 32
                || label.Any(c => c > 127 || !char.IsLetterOrDigit(c))
            )
                throw new ArgumentException(
                    "Use a 1..32 character ASCII alphanumeric probe label."
                );
            if (kind != "message" && kind != "letter" || delayTicks < 0 || delayTicks > 600)
                throw new ArgumentException("Use message/letter and delayTicks 0..600.");
            bool submitted = sender(
                null,
                new object[]
                {
                    worldCommand ? (object)mapId : map,
                    factionId,
                    x,
                    z,
                    label,
                    kind,
                    historical,
                    delayTicks,
                }
            );
            return new
            {
                success = submitted,
                phase = "native-notification-command-submitted",
                mapId,
                factionId,
                label,
                kind,
                worldCommand,
            };
        });

    private static object Register()
    {
        if (Current.Game != null)
            throw new InvalidOperationException(
                "Register at the main menu before loading/hosting."
            );
        if (syncMethod != null)
            return new
            {
                success = true,
                registered = true,
                syncId = Read(syncMethod, "syncId"),
            };
        var method = typeof(MultiplayerNotificationScenarios).GetMethod(
            nameof(CreateNotification),
            BindingFlags.NonPublic | BindingFlags.Static
        );
        syncMethod = MultiplayerProbeRegistration.Register(method, out submit);
        var factions = AccessTools.TypeByName("Multiplayer.Client.Factions.FactionExtensions");
        push =
            (Action<Map, Faction, bool>)
                Delegate.CreateDelegate(
                    typeof(Action<Map, Faction, bool>),
                    factions.GetMethod(
                        "PushFaction",
                        new[] { typeof(Map), typeof(Faction), typeof(bool) }
                    )
                );
        pop =
            (Func<Map, Faction>)
                Delegate.CreateDelegate(
                    typeof(Func<Map, Faction>),
                    factions.GetMethod("PopFaction", new[] { typeof(Map) })
                );
        return new
        {
            success = true,
            registered = true,
            existingCommandIdsPreserved = true,
            syncId = Read(syncMethod, "syncId"),
        };
    }

    private static void CreateWorldNotification(
        int mapId,
        int factionId,
        int x,
        int z,
        string label,
        string kind,
        bool historical,
        int delayTicks
    )
    {
        var map = Find.Maps.Single(map => map.uniqueID == mapId);
        CreateNotification(map, factionId, x, z, label, kind, historical, delayTicks);
        lastWorldEnqueue = new
        {
            worldContextTicks = Find.TickManager.TicksGame,
            mapId,
            factionId,
            label,
            delayTicks,
        };
    }

    private static void CreateNotification(
        Map map,
        int factionId,
        int x,
        int z,
        string label,
        string kind,
        bool historical,
        int delayTicks
    )
    {
        var faction = Find.FactionManager.AllFactionsListForReading.First(f =>
            f.loadID == factionId
        );
        push(map, faction, true);
        try
        {
            var targets = new LookTargets(new TargetInfo(new IntVec3(x, 0, z), map));
            if (kind == "message")
                Messages.Message(
                    new Message(Prefix + label, MessageTypeDefOf.NeutralEvent, targets),
                    historical
                );
            else
                Find.LetterStack.ReceiveLetter(
                    LetterMaker.MakeLetter(
                        Prefix + label,
                        Prefix + label,
                        LetterDefOf.NeutralEvent,
                        targets
                    ),
                    "Total Fog native MP probe",
                    delayTicks,
                    false
                );
        }
        finally
        {
            pop(map);
        }
    }

    private static object Status()
    {
        if (Current.Game == null || push == null)
            return new { success = true, registered = syncMethod != null };
        return new
        {
            success = true,
            registered = true,
            syncId = Read(syncMethod, "syncId"),
            worldSyncId = worldSyncMethod == null
                ? (int?)null
                : MultiplayerProbeRegistration.Id(worldSyncMethod),
            lastWorldEnqueue,
            maps = Find
                .Maps.OrderBy(map => map.uniqueID)
                .Select(map => new
                {
                    map = map.uniqueID,
                    pending = (
                        (IEnumerable)Read(
                            Read(map.GetComponent<DeferredNotifications>(), "queue"),
                            "Items"
                        )
                    )
                        .Cast<DeferredNotification>()
                        .Select(n => new
                        {
                            label = n.letter != null ? n.letter.Label.ToString() : n.message?.text,
                            faction = n.faction?.loadID,
                            kind = n.letter == null ? "message" : "letter",
                            n.historical,
                            n.earliestTick,
                            targets = n
                                .Targets.targets.Select(t => new
                                {
                                    map = t.Map?.uniqueID,
                                    cell = t.Cell.ToString(),
                                    thing = t.Thing?.ThingID,
                                })
                                .ToArray(),
                        })
                        .ToArray(),
                    archives = Find
                        .FactionManager.AllFactionsListForReading.Where(f => f.IsPlayer)
                        .OrderBy(f => f.loadID)
                        .Select(faction => ReadArchive(map, faction))
                        .ToArray(),
                })
                .ToArray(),
        };
    }

    private static object ReadArchive(Map map, Faction faction)
    {
        push(map, faction, true);
        try
        {
            return new
            {
                faction = faction.loadID,
                messages = Find
                    .Archive.ArchivablesListForReading.OfType<Message>()
                    .Where(m => m.text.StartsWith(Prefix, StringComparison.Ordinal))
                    .Select(m => m.text)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray(),
                letters = Find
                    .Archive.ArchivablesListForReading.OfType<Letter>()
                    .Where(l => l.Label.ToString().StartsWith(Prefix, StringComparison.Ordinal))
                    .Select(l => l.Label.ToString())
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray(),
            };
        }
        finally
        {
            pop(map);
        }
    }

    private static object Read(object target, string field) =>
        AccessTools.Field(target.GetType(), field).GetValue(target);
}
