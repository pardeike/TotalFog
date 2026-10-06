using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Exercises the installed CE method and the actual Total Fog postfix on a paused test map.</summary>
public sealed class CombatExtendedScenarios
{
    [Tool("totalfog/ce_hit_checks", Description = "Verify native CE hit results across hidden/revealed cells, failed ballistics and the enemy-fog setting. Uses a temporary shooter/wall, restores settings and patches, and leaves the fixture paused.")]
    public static async Task<object> HitChecks(IRimBridgeContext ctx, CancellationToken cancellationToken)
    {
        return await ctx.MainThread.InvokeAsync(() =>
        {
            if (!ModsConfig.IsActive("ceteam.combatextended"))
                throw new InvalidOperationException("Enable the isolated CE test profile first.");
            var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a test map first.");
            if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the map first.");
            var type = AccessTools.TypeByName("CombatExtended.Verb_LaunchProjectileCE");
            if (type == null) throw new InvalidOperationException("The installed CE projectile verb is unavailable.");
            var method = AccessTools.DeclaredMethod(type, "CanHitCellFromCellIgnoringRange",
                new[] { typeof(Vector3), typeof(IntVec3), typeof(Thing) });
            if (method == null) throw new InvalidOperationException("The installed CE hit-check overload is unavailable.");
            var patch = Harmony.GetPatchInfo(method)?.Postfixes.SingleOrDefault(p => p.owner == "brrainz.totalfog" &&
                p.PatchMethod.DeclaringType.FullName == "TotalFog.Compatibility.CombatExtendedIntegration");
            if (patch == null) throw new InvalidOperationException("The actual CE hit-check method has no Total Fog postfix.");
            var fog = map.GetComponent<MapVisibility>();
            var oldRange = FogSettings.BaseViewRange;
            int startTick = Find.TickManager.TicksGame;
            bool oldEnemyFog = FogSettings.AISmart, addedSight = false;
            Pawn pawn = null;
            Thing wall = null;
            int index = -1;
            var harmony = new Harmony("brrainz.totalfog");
            bool unpatched = false;
            try
            {
                IntVec3 source = IntVec3.Invalid;
                foreach (var cell in map.AllCells)
                {
                    if (cell.x < 5 || cell.z < 5 || cell.x + 20 >= map.Size.x || cell.z + 1 >= map.Size.z) continue;
                    bool clear = true;
                    for (int x = 0; x <= 20 && clear; x++)
                    {
                        var strip = cell + new IntVec3(x, 0, 0);
                        clear = strip.Standable(map) && strip.GetThingList(map).Count == 0 && !fog.IsShown(Faction.OfPlayer, strip);
                    }
                    if (clear) { source = cell; break; }
                }
                if (!source.IsValid) throw new InvalidOperationException("No empty unseen shooting strip is available.");
                var target = source + new IntVec3(16, 0, 0);
                index = map.cellIndices.CellToIndex(target);
                FogSettings.BaseViewRange = 5; FogSettings.AISmart = false;
                pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                pawn.equipment.DestroyAllEquipment();
                var gun = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));
                pawn.equipment.AddEquipment(gun);
                GenSpawn.Spawn(pawn, source, map);
                pawn.drafter.Drafted = true;
                var sight = pawn.TryGetComp<CompFog>().FieldOfViewWatcher;
                sight.UpdateFoV(true);
                var verb = gun.TryGetComp<CompEquippable>().PrimaryVerb;
                if (!type.IsInstanceOfType(verb)) throw new InvalidOperationException("Test gun does not supply a CE projectile verb.");
                bool Call() => (bool)method.Invoke(verb, new object[] { source.ToVector3Shifted() + Vector3.up, target, null });
                void RestorePatch()
                {
                    if (!unpatched) return;
                    harmony.Patch(method, postfix: new HarmonyMethod(patch.PatchMethod) { priority = patch.priority });
                    unpatched = false;
                }
                bool Native()
                {
                    harmony.Unpatch(method, HarmonyPatchType.Postfix, "brrainz.totalfog"); unpatched = true;
                    try { return Call(); }
                    finally { RestorePatch(); }
                }
                bool nativeOpen = Native(), hiddenPlayer = Call();
                fog.IncrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index); addedSight = true;
                bool revealedPlayer = Call();
                wall = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                GenSpawn.Spawn(wall, source + new IntVec3(8, 0, 0), map);
                // The installed CE cover-height cache expires at a native tick
                // boundary. Let its normal invalidation run after spawning.
                Find.TickManager.DoSingleTick();
                bool nativeBlocked = Native(), blockedWithFog = Call();
                wall.Destroy(); wall = null;
                Find.TickManager.DoSingleTick();
                fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index); addedSight = false;
                pawn.SetFaction(Faction.OfAncientsHostile);
                sight.UpdateFoV(true);
                bool nativeEnemy = Native(), enemyDisabled = Call();
                FogSettings.AISmart = true; sight.UpdateFoV(true);
                bool enemyEnabled = Call();
                RestorePatch();
                return new
                {
                    success = nativeOpen && !hiddenPlayer && revealedPlayer && !nativeBlocked && !blockedWithFog &&
                        nativeEnemy && enemyDisabled && !enemyEnabled,
                    nativeOpen, hiddenPlayer, revealedPlayer, nativeBlocked, blockedWithFog,
                    nativeEnemy, enemyDisabled, enemyEnabled,
                    source = source.ToString(), target = target.ToString(), verbType = verb.GetType().FullName,
                    method = method.ToString(), ceMvid = type.Assembly.ManifestModule.ModuleVersionId.ToString(),
                    totalFogPostfix = patch.PatchMethod.ToString(),
                    nativeTicks = Find.TickManager.TicksGame - startTick
                };
            }
            finally
            {
                if (unpatched) harmony.Patch(method, postfix: new HarmonyMethod(patch.PatchMethod) { priority = patch.priority });
                if (addedSight) fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                if (wall != null && !wall.Destroyed) wall.Destroy();
                if (pawn != null && !pawn.Destroyed) pawn.Destroy();
                FogSettings.BaseViewRange = oldRange; FogSettings.AISmart = oldEnemyFog;
                foreach (var watcher in fog.fowWatchers.ToArray()) watcher.UpdateFoV(true);
            }
        }, cancellationToken);
    }
}
