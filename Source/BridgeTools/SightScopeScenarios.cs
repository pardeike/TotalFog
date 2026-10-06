using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class SightScopeScenarios
{
    [Tool(
        "totalfog/sight_work_scope",
        Description = "Toggle enemy fog targeting on a paused map, verify enemy grids exist only when enabled, verify the actual hit-check patch respects the setting and preserves player sight, then restore the setting and coverage."
    )]
    public static async Task<object> SightWorkScope(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken
    )
    {
        return await ctx.MainThread.InvokeAsync(
            () =>
            {
                var map =
                    Find.CurrentMap ?? throw new InvalidOperationException("Load a map first.");
                if (!Find.TickManager.Paused)
                    throw new InvalidOperationException("Pause the map first.");
                var fog = map.GetComponent<MapVisibility>();
                var player = map.mapPawns.AllPawnsSpawned.First(p =>
                    p.Faction == Faction.OfPlayer && !p.Dead
                );
                var enemy = map.mapPawns.AllPawnsSpawned.First(p =>
                    p.Faction != null && p.Faction != Faction.OfPlayer && !p.Dead
                );
                bool previous = FogSettings.AISmart;
                var factionField = AccessTools.Field(typeof(CompSightSource), "faction");
                var hitPatch = typeof(MapVisibility)
                    .Assembly.GetType("TotalFog.Detours.Verb")
                    .GetMethod(
                        "CanHitCellFromCellIgnoringRange_Postfix",
                        BindingFlags.Static | BindingFlags.NonPublic
                    );
                int EnemySources() =>
                    fog.fowWatchers.Count(w =>
                        w.LastSightRange > 0
                        && factionField.GetValue(w) is Faction faction
                        && faction != Faction.OfPlayer
                    );
                void Refresh()
                {
                    foreach (var source in fog.fowWatchers.ToArray())
                        source.UpdateFoV(true);
                }
                // A visual-ready load can have newly computed lighting/render cells.
                // Compare stable source samples, not initial-load coverage.
                Refresh();
                var before = fog.Coverage.Counts(0).ToArray();
                int Difference() =>
                    before.Zip(fog.Coverage.Counts(0), (a, b) => a != b ? 1 : 0).Sum();
                bool Hit(Thing caster, bool nativeResult)
                {
                    var verb = new Verb_Shoot
                    {
                        caster = caster,
                        verbProps = new VerbProperties { requireLineOfSight = true },
                    };
                    object[] args =
                    {
                        verb,
                        nativeResult,
                        caster.Position,
                        new IntVec3(map.Size.x + 100, 0, map.Size.z + 100),
                        false,
                    };
                    hitPatch.Invoke(null, args);
                    return (bool)args[1];
                }
                try
                {
                    FogSettings.AISmart = false;
                    Refresh();
                    int disabledEnemies = EnemySources();
                    bool disabledPlayerUnchanged = before.SequenceEqual(fog.Coverage.Counts(0));
                    int disabledChangedCells = Difference();
                    bool disabledEnemyNativeTrue = Hit(enemy, true),
                        disabledEnemyNativeFalse = Hit(enemy, false);
                    bool disabledPlayerHiddenTarget = Hit(player, true);
                    FogSettings.AISmart = true;
                    Refresh();
                    int enabledEnemies = EnemySources();
                    bool enabledPlayerUnchanged = before.SequenceEqual(fog.Coverage.Counts(0));
                    int enabledChangedCells = Difference();
                    bool enabledEnemyHiddenTarget = Hit(enemy, true);
                    return new
                    {
                        success = disabledEnemies == 0
                            && enabledEnemies > 0
                            && disabledPlayerUnchanged
                            && enabledPlayerUnchanged
                            && disabledEnemyNativeTrue
                            && !disabledEnemyNativeFalse
                            && !disabledPlayerHiddenTarget
                            && !enabledEnemyHiddenTarget,
                        disabledEnemies,
                        enabledEnemies,
                        disabledPlayerUnchanged,
                        enabledPlayerUnchanged,
                        disabledEnemyNativeTrue,
                        disabledEnemyNativeFalse,
                        disabledPlayerHiddenTarget,
                        enabledEnemyHiddenTarget,
                        disabledChangedCells,
                        enabledChangedCells,
                        factions = fog
                            .fowWatchers.Select(w => w.parent.Faction)
                            .Where(f => f != null)
                            .Distinct()
                            .Select(f => new
                            {
                                f.loadID,
                                f.IsPlayer,
                                def = f.def.defName,
                            })
                            .ToArray(),
                    };
                }
                finally
                {
                    FogSettings.AISmart = previous;
                    Refresh();
                }
            },
            cancellationToken
        );
    }
}
