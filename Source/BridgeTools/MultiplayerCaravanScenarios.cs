using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld.Planet;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Opt-in native caravan lifecycle probe, excluded from player packages.</summary>
public sealed class MultiplayerCaravanScenarios
{
    private static object syncMethod;
    private static Func<object, object[], bool> submit;
    private static object lastTransfer;

    [Tool(
        "totalfog/multiplayer_caravan",
        Description = "Opt-in native caravan transfer probe. Register notifications FIRST and this probe SECOND on BOTH main menus before loading/hosting; require equal IDs. Transfer submits a world-context native command that exits one owned colonist into a real caravan and enters another loaded map at an empty standable cell. Skips travel time, uses native exit/world-pawn/entry lifecycle, never steps ticks. Status reads the last command receipt. Probe saves require both handlers; excluded from player ZIPs."
    )]
    public static Task<object> Caravan(
        IRimBridgeContext context,
        string action = "status",
        int pawnId = 390,
        int destinationMapId = 1,
        int x = 30,
        int z = 30
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            if (action == "register")
            {
                if (Current.Game != null)
                    throw new InvalidOperationException("Register before loading/hosting.");
                if (syncMethod == null)
                    syncMethod = MultiplayerProbeRegistration.Register(
                        typeof(MultiplayerCaravanScenarios).GetMethod(
                            nameof(Transfer),
                            BindingFlags.NonPublic | BindingFlags.Static
                        ),
                        out submit
                    );
                return new
                {
                    success = true,
                    registered = true,
                    existingCommandIdsPreserved = true,
                    syncId = MultiplayerProbeRegistration.Id(syncMethod),
                };
            }
            if (action == "status")
                return new
                {
                    success = true,
                    registered = syncMethod != null,
                    lastTransfer,
                };
            if (action != "transfer" || submit == null || Current.Game == null)
                throw new InvalidOperationException(
                    "Register on both main menus, then use transfer/status."
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
            Validate(pawnId, destinationMapId, new IntVec3(x, 0, z));
            return new
            {
                success = submit(null, new object[] { pawnId, destinationMapId, x, z }),
                phase = "native-caravan-command-submitted",
                pawnId,
                destinationMapId,
            };
        });

    private static (Pawn pawn, Map destination) Validate(
        int pawnId,
        int destinationMapId,
        IntVec3 cell
    )
    {
        var pawn = Find
            .Maps.SelectMany(map => map.mapPawns.AllPawnsSpawned)
            .FirstOrDefault(pawn => pawn.thingIDNumber == pawnId);
        var destination = Find.Maps.FirstOrDefault(map => map.uniqueID == destinationMapId);
        if (
            pawn == null
            || !pawn.IsColonistPlayerControlled
            || pawn.Dead
            || pawn.Downed
            || destination == null
            || destination == pawn.Map
            || !cell.InBounds(destination)
            || !cell.Standable(destination)
            || cell.GetEdifice(destination) != null
            || cell.GetFirstPawn(destination) != null
        )
            throw new ArgumentException(
                "Use a healthy owned colonist and another loaded map's empty standable cell."
            );
        return (pawn, destination);
    }

    // Primitive IDs deliberately keep this command in native world context,
    // like caravan arrival. No map or pawn serializer selects a map clock.
    private static void Transfer(int pawnId, int destinationMapId, int x, int z)
    {
        var cell = new IntVec3(x, 0, z);
        var (pawn, destination) = Validate(pawnId, destinationMapId, cell);
        var origin = pawn.Map;
        var before = State(pawn);
        var caravan =
            CaravanExitMapUtility.ExitMapAndCreateCaravan(
                new[] { pawn },
                pawn.Faction,
                origin.Tile,
                origin.Tile,
                PlanetTile.Invalid,
                sendMessage: false
            ) ?? throw new InvalidOperationException("Native caravan creation failed.");
        var exited = State(pawn);
        caravan.Tile = destination.Tile;
        CaravanEnterMapUtility.Enter(
            caravan,
            destination,
            _ => cell,
            CaravanDropInventoryMode.DoNotDrop,
            draftColonists: true
        );
        lastTransfer = new
        {
            worldContextTicks = Find.TickManager.TicksGame,
            origin = origin.uniqueID,
            destination = destination.uniqueID,
            before,
            exited,
            entered = State(pawn),
            caravanDestroyed = caravan.Destroyed,
        };
    }

    private static object State(Pawn pawn)
    {
        var fog = pawn.TryGetComp<CompFog>();
        var source = fog?.FieldOfViewWatcher;
        return new
        {
            id = pawn.ThingID,
            pawn.Spawned,
            worldPawn = pawn.IsWorldPawn(),
            map = pawn.Map?.uniqueID,
            faction = pawn.Faction?.loadID,
            position = pawn.Position.ToString(),
            registrations = Find
                .Maps.Where(map => map.GetComponent<MapVisibility>().fowWatchers.Contains(source))
                .Select(map => map.uniqueID)
                .ToArray(),
            range = source?.LastSightRange,
            nextCheck = Read(source, "nextCheck"),
            nextHearing = Read(source, "nextHearing"),
            lastMovement = Read(source, "lastMovement"),
            observationNextCheck = Read(fog?.HideFromPlayer, "nextCheck"),
        };
    }

    private static object Read(object target, string name) =>
        target == null ? null : AccessTools.Field(target.GetType(), name).GetValue(target);
}
