using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Fresh-session synchronized setup and native player tower orders.</summary>
public sealed class MultiplayerWatchtowerScenarios
{
    private static object handler;
    private static Func<object, object[], bool> submit;

    [Tool(
        "totalfog/multiplayer_watchtower",
        Description = "Register this probe ALONE on BOTH main menus for a FRESH session; require equal appended IDs. Do not load older instrumented MP saves. Setup/collapse/cleanup use synchronized native world commands. Watch/descend invoke the tower's actual float-menu action outside simulation, exercising native MP ordered-job serialization. Status is read-only and resolves saved native objects by IDs, including after rejoin. Never steps ticks. Use a task-owned fresh save; excluded from player ZIPs."
    )]
    public static Task<object> Fixture(
        IRimBridgeContext context,
        string action = "status",
        int mapId = 0,
        int towerId = 0,
        int pawnId = 0
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            if (action == "register")
            {
                if (handler == null)
                    handler = MultiplayerProbeRegistration.Register(
                        typeof(MultiplayerWatchtowerScenarios).GetMethod(
                            nameof(Command),
                            BindingFlags.Static | BindingFlags.NonPublic
                        ),
                        out submit
                    );
                return new
                {
                    success = true,
                    existingCommandIdsPreserved = true,
                    syncId = MultiplayerProbeRegistration.Id(handler),
                };
            }
            var map = Find.Maps.FirstOrDefault(m => m.uniqueID == mapId);
            if (action == "status")
                return State(map);
            var api = AccessTools.TypeByName("Multiplayer.Client.Multiplayer");
            var session = api == null ? null : AccessTools.Field(api, "session").GetValue(null);
            if (
                submit == null
                || map == null
                || session == null
                || AccessTools.Field(session.GetType(), "desynced").GetValue(session) is not false
                || AccessTools.Property(api, "IsReplay").GetValue(null) is not false
            )
                throw new InvalidOperationException("Use a registered live non-desynced session.");
            if (action is "watch" or "descend")
            {
                var (tower, pawn) = Resolve(map, towerId, pawnId);
                if (pawn.Faction != Faction.OfPlayer)
                    throw new InvalidOperationException("Use the local player's owned pawn.");
                string label = (
                    action == "watch" ? "TotalFog_TowerWatch" : "TotalFog_TowerDescend"
                ).Translate();
                var option = tower.GetFloatMenuOptions(pawn).Single(o => o.Label == label);
                if (option.Disabled)
                    throw new InvalidOperationException("The native menu option is disabled.");
                string before = pawn.CurJob?.def.defName;
                option.action();
                return new
                {
                    success = true,
                    phase = "native-float-menu-action-submitted",
                    action,
                    before,
                    immediateJob = pawn.CurJob?.def.defName,
                };
            }
            if (action is not ("setup" or "collapse" or "cleanup"))
                throw new ArgumentException("Use setup/watch/descend/collapse/cleanup/status.");
            if (action != "setup")
                Resolve(map, towerId, pawnId);
            return new
            {
                success = submit(null, new object[] { action, mapId, towerId, pawnId }),
                phase = "native-tower-command-submitted",
                action,
            };
        });

    private static (Building_Watchtower tower, Pawn pawn) Resolve(
        Map map,
        int towerId,
        int pawnId
    ) =>
        (
            map.listerThings.AllThings.OfType<Building_Watchtower>()
                .Single(t => t.thingIDNumber == towerId),
            map.mapPawns.AllPawnsSpawned.Single(p => p.thingIDNumber == pawnId)
        );

    private static void Command(string action, int mapId, int towerId, int pawnId)
    {
        var map = Find.Maps.Single(m => m.uniqueID == mapId);
        if (action == "setup")
        {
            if (map.listerThings.AllThings.OfType<Building_Watchtower>().Any())
                throw new InvalidOperationException(
                    "Use a fresh task-owned fixture without towers."
                );
            var cell = GenRadial
                .RadialCellsAround(new IntVec3(map.Size.x - 45, 0, map.Size.z / 3), 75, true)
                .First(c =>
                    new CellRect(c.x - 4, c.z - 4, 9, 9).All(p =>
                        p.InBounds(map)
                        && p.Standable(map)
                        && !p.Roofed(map)
                        && !p.Fogged(map)
                        && p.GetThingList(map).All(t => t is Plant or Filth)
                    )
                );
            foreach (var p in new CellRect(cell.x - 4, cell.z - 4, 9, 9))
            foreach (var thing in p.GetThingList(map).ToArray())
                thing.Destroy(DestroyMode.Vanish);
            var tower = (Building_Watchtower)
                ThingMaker.MakeThing(WatchtowerDefOf.TotalFog_Watchtower, ThingDefOf.WoodLog);
            tower.SetFaction(Faction.OfPlayer);
            GenSpawn.Spawn(tower, cell, map, Rot4.South);
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            pawn.Name = new NameSingle("Tower MP lookout");
            pawn.equipment.DestroyAllEquipment();
            GenSpawn.Spawn(pawn, tower.Approach, map);
            pawn.drafter.Drafted = true;
            pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 20000));
            return;
        }
        var (existing, actor) = Resolve(map, towerId, pawnId);
        if (action == "cleanup")
        {
            actor.Destroy(DestroyMode.Vanish);
            existing.Destroy(DestroyMode.Vanish);
        }
        else if (action == "collapse")
            existing.Destroy(DestroyMode.KillFinalize);
    }

    private static object State(Map map) =>
        new
        {
            success = map != null,
            map = map?.uniqueID,
            ticks = Find.TickManager.TicksGame,
            towers = map
                ?.listerThings.AllThings.OfType<Building_Watchtower>()
                .OrderBy(t => t.thingIDNumber)
                .Select(t => new
                {
                    id = t.thingIDNumber,
                    position = t.Position.ToString(),
                    approach = t.Approach.ToString(),
                    t.HitPoints,
                    t.Elevation,
                    t.RescuerElevation,
                    t.Watching,
                    t.ProvidesSight,
                    occupant = t.Occupant?.thingIDNumber,
                    rescuer = t.Rescuer?.thingIDNumber,
                })
                .ToArray(),
            pawns = map
                ?.mapPawns.AllPawnsSpawned.Where(p => p.Name?.ToStringShort == "Tower MP lookout")
                .OrderBy(p => p.thingIDNumber)
                .Select(p => new
                {
                    id = p.thingIDNumber,
                    position = p.Position.ToString(),
                    p.Downed,
                    health = p.health.summaryHealth.SummaryHealthPercent,
                    job = p.CurJob?.def.defName,
                    jobId = p.CurJob?.loadID,
                    range = p.TryGetComp<CompFog>()?.FieldOfViewWatcher?.LastSightRange,
                    offset = TowerPresentation.Offset(p).ToString(),
                })
                .ToArray(),
        };
}
