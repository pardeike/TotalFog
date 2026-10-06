using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using RimWorld.Planet;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace TotalFog.BridgeTools;

public sealed class SymbiantCombatSightScenarios
{
    [Tool("totalfog/symbiant_combat_sight", Description = "Use the existing combat fixture and real faction sight to check automatic Symbiant acquisition and logical hit cells across disabled enemy fog, hidden, visible-root and visible-body/hidden-root states. Ranged or melee variants use the native fight scan flags. If the paused matrix passes, order a native hostile attack for 25 seconds at Normal speed with enemy fog enabled while an actual security bell supplies partial player sight. No nearby player pawn competes for targeting. Requires shared damage, unchanged host injury and bell health, a hidden root and partly visible body. Optionally save/reload the active fight in process, verify exact identities, health, body, job and partial sight, then require further damage during 10 seconds of native playback. Retains the named PartialSymbiant fixture; restores options and reloads the unchanged named base. With resumeLoadedFight, load the previously prepared variant using the same sight settings and resume ten seconds of native combat without creating a new fixture. Caller must restart first and compare returned loaded identities/health/body/tick with preparation evidence. Not every weapon/sight range.")]
    public static async Task<object> SymbiantCombatSight(IRimBridgeContext ctx,
        CancellationToken cancellationToken, string saveName, bool melee = false, bool saveAndReload = false,
        bool resumeLoadedFight = false)
    {
        if (resumeLoadedFight)
        {
            if (saveAndReload) throw new ArgumentException("Choose preparation or saved-fight resumption.");
            return await ResumeSavedFight(ctx, cancellationToken, saveName, melee);
        }
        int oldRange = FogSettings.BaseViewRange;
        bool oldEnemyFog = FogSettings.AISmart;
        var rows = new List<object>();
        bool passed = true;
        object before = null, after = null, playback = null;
        object persistence = null;
        Pawn target = null, attacker = null, observer = null;
        ThingWithComps sightSource = null;
        Map map = null;
        float healthBefore = 0, hostInjuriesBefore = 0;
        int sightSourceHealthBefore = 0;
        int playbackStartTick = 0;
        Pawn host = null;
        var type = AccessTools.TypeByName("ZombieLand.ZombieSymbiant");
        var combat = AccessTools.TypeByName("ZombieLand.ZombieSymbiantCombat");
        if (type == null || combat == null) throw new InvalidOperationException("Load the paired Zombieland profile.");
        var absoluteCells = AccessTools.Property(type, "AbsoluteCells");
        var health = AccessTools.Property(type, "SharedHealthCurrent");
        IntVec3[] Cells() => ((IEnumerable<IntVec3>)absoluteCells.GetValue(target)).ToArray();
        float HostInjuries() => host.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(injury => injury.Severity);
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                if (Find.CurrentMap == null || !Find.TickManager.Paused || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use a paused paired base with colony fog enabled.");
                if (saveAndReload && !Prefs.PauseOnLoad)
                    throw new InvalidOperationException("The save/reload contract requires native pause on load.");
                FogSettings.AISmart = false;
            }, cancellationToken);
            var fixture = await ctx.Tools.CallAsync("zombieland/symbiant_combat_isolation_contract",
                new { mode = melee ? "setup-melee" : "setup-assault", cleanup = false }, cancellationToken: cancellationToken);
            if (!fixture.Succeeded() || !fixture.ReadResult<bool>("success"))
                throw new InvalidOperationException("The retained assault fixture did not pass.");
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap;
                target = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.GetType() == type);
                attacker = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.Name?.ToStringShort == "ZL_SymbiantCombat_Enemy");
                host = (Pawn)AccessTools.Property(type, "LinkedHost").GetValue(target);
                observer = map.mapPawns.AllPawnsSpawned.First(pawn => pawn.Faction == Faction.OfPlayer &&
                    pawn.RaceProps.Humanlike && pawn.TryGetComp<CompFog>()?.FieldOfViewWatcher != null);
                FogSettings.BaseViewRange = 3;
                void Move(Pawn pawn, IntVec3 cell)
                {
                    pawn.pather.StopDead();
                    pawn.Position = cell;
                    pawn.TryGetComp<CompFog>()?.FieldOfViewWatcher.UpdateFoV(true);
                }
                attacker.jobs.StopAll(false, true);
                observer.jobs.StopAll(false, true);
                var remote = new IntVec3(map.Size.x - 20, 0, map.Size.z - 20);
                foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(pawn =>
                    (pawn.Faction == Faction.OfPlayer || pawn.Faction == attacker.Faction) &&
                    pawn != target && pawn != attacker && pawn != observer).ToArray())
                {
                    pawn.GetLord()?.RemovePawn(pawn);
                    pawn.DeSpawn();
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                }
                Move(observer, remote);
                void Refresh() { foreach (var watcher in map.GetVisibility().fowWatchers.ToArray()) watcher.UpdateFoV(true); }
                int Seen(Faction faction) => Cells().Count(cell => map.GetVisibility().IsShown(faction, cell));
                bool RootSeen() => map.GetVisibility().IsShown(attacker.Faction, target.Position);
                var flags = TargetScanFlags.NeedLOSToPawns | TargetScanFlags.NeedReachableIfCantHitFromMyPos |
                    TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable;
                void Record(string state, bool expectedTarget)
                {
                    var acquired = AttackTargetFinder.BestAttackTarget(attacker, flags, thing => thing == target,
                        0f, 999f, canTakeTargetsCloserThanEffectiveMinRange: true);
                    object[] args = melee
                        ? new object[] { attacker, target, IntVec3.Invalid, IntVec3.Invalid, Danger.Deadly, null, false, false }
                        : new object[] { attacker.CurrentEffectiveVerb, attacker.Position, target, IntVec3.Invalid, default(ShootLine), false, null };
                    bool canHit = (bool)AccessTools.Method(combat, melee ? "TrySelectMeleeCells" : "TrySelectRangedCell").Invoke(null, args);
                    var hitCell = (IntVec3)args[3];
                    bool hitVisible = canHit && map.GetVisibility().IsShown(attacker.Faction, hitCell);
                    bool valid = (acquired?.Thing == target) == expectedTarget && canHit == expectedTarget &&
                        (!FogSettings.AISmart || !canHit || hitVisible);
                    passed &= valid;
                    rows.Add(new { state, passed = valid, enemyFog = FogSettings.AISmart, expectedTarget,
                        acquired = acquired?.Thing?.ThingID, canHit, hitCell = hitCell.ToString(), hitVisible,
                        rootVisible = RootSeen(), visibleBodyCells = Seen(attacker.Faction),
                        playerVisibleBodyCells = Seen(Faction.OfPlayer), attackerCell = attacker.Position.ToString(),
                        actualSightRange = attacker.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange,
                        nativeCanSee = AttackTargetFinder.CanSee(attacker, target) });
                }
                Move(attacker, target.Position + new IntVec3(12, 0, 0));
                Refresh(); Record("enemy-fog-disabled", true);
                FogSettings.AISmart = true; Refresh();
                if (Seen(attacker.Faction) != 0) throw new InvalidOperationException("The hidden control has faction sight.");
                Record("all-hidden", false);
                var candidates = CellRect.CenteredOn(target.Position, 5).Where(cell => cell.InBounds(map) &&
                    cell.Standable(map) && cell.GetFirstPawn(map) != target).ToArray();
                bool Place(bool root, bool partial)
                {
                    foreach (var cell in candidates)
                    {
                        Move(attacker, cell);
                        int count = Seen(attacker.Faction);
                        if (RootSeen() == root && count > 0 && (partial ? count < Cells().Length : count == Cells().Length)) return true;
                    }
                    return false;
                }
                if (!Place(false, true)) throw new InvalidOperationException("No actual faction view separates root and body.");
                Record("visible-body-hidden-root", true);
                if (!Place(true, false)) throw new InvalidOperationException("No actual faction view sees the whole fixture.");
                Record("visible-root", true);
                Move(attacker, target.Position + new IntVec3(12, 0, 0));
                Record("hidden-after-reveal", false);
                // Keep a colonist on the map for native colony-center queries,
                // far from the fight. Building sight supplies the partial view.
                sightSource = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SecurityBellSmall"));
                sightSource.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(sightSource, remote, map);
                bool playerPartial = false;
                foreach (var cell in candidates)
                {
                    sightSource.Position = cell;
                    sightSource.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    int count = Seen(Faction.OfPlayer);
                    if (count <= 0 || count >= Cells().Length || map.GetVisibility().IsShown(Faction.OfPlayer, target.Position)) continue;
                    playerPartial = true; break;
                }
                if (!playerPartial) throw new InvalidOperationException("No actual player view separates root and body.");
                if (!Place(false, true)) throw new InvalidOperationException("No actual hostile view separates root and body for playback.");
                var nativeTarget = AttackTargetFinder.BestAttackTarget(attacker, flags, null,
                    0f, 999f, canTakeTargetsCloserThanEffectiveMinRange: true);
                if (nativeTarget?.Thing != target) throw new InvalidOperationException("Another native threat still competes with the Symbiant.");
                attacker.jobs.StartJob(JobMaker.MakeJob(melee ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target),
                    JobCondition.InterruptForced);
                healthBefore = (float)health.GetValue(target); hostInjuriesBefore = HostInjuries();
                sightSourceHealthBefore = sightSource.HitPoints;
                playbackStartTick = Find.TickManager.TicksGame;
                before = new { health = healthBefore, hostInjuries = hostInjuriesBefore,
                    rootVisible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position),
                    visibleBodyCells = Seen(Faction.OfPlayer), cells = Cells().Length,
                    sightSource = sightSource.def.defName, sourceCell = sightSource.Position.ToString(),
                    sourceRange = sightSource.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange,
                    sourceHealth = sightSourceHealthBefore, enemyFog = FogSettings.AISmart,
                    nativeTarget = nativeTarget.Thing.ThingID,
                    attackerJob = attacker.CurJob?.def.defName,
                    enemyTarget = attacker.mindState.enemyTarget?.ThingID,
                    lastAttackedTarget = attacker.mindState.lastAttackedTarget.Thing?.ThingID,
                    lastAttackTargetTick = attacker.mindState.lastAttackTargetTick,
                    startTick = playbackStartTick };
            }, cancellationToken);
            if (passed)
            {
                var run = await ctx.Tools.CallAsync("rimworld/play_for", new { durationMs = 25000, speed = "Normal",
                    forceRequestedSpeed = false }, cancellationToken: cancellationToken);
                if (!run.Succeeded() || !run.ReadResult<bool>("success")) throw new InvalidOperationException("Native assault playback failed.");
                playback = run;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    float currentHealth = (float)health.GetValue(target), hostInjuries = HostInjuries();
                    bool attacked = attacker.mindState.lastAttackedTarget.Thing == target &&
                        attacker.mindState.lastAttackTargetTick >= playbackStartTick;
                    int visibleBodyCells = Cells().Count(cell => map.GetVisibility().IsShown(Faction.OfPlayer, cell));
                    bool rootVisible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position);
                    bool valid = currentHealth < healthBefore && hostInjuries == hostInjuriesBefore && attacked &&
                        sightSource.Spawned && sightSource.HitPoints == sightSourceHealthBefore &&
                        !rootVisible && visibleBodyCells > 0 && visibleBodyCells < Cells().Length;
                    passed &= valid;
                    after = new { passed = valid, health = currentHealth, hostInjuries, attacked,
                        job = attacker.CurJob?.def.defName, target = attacker.CurJob?.targetA.Thing?.ThingID,
                        enemyTarget = attacker.mindState.enemyTarget?.ThingID,
                        lastAttackTargetTick = attacker.mindState.lastAttackTargetTick,
                        attackerCell = attacker.Position.ToString(),
                        stance = attacker.stances.curStance.GetType().Name,
                        stanceTarget = (attacker.stances.curStance as Stance_Busy)?.focusTarg.Thing?.ThingID,
                        rootVisible, visibleBodyCells, sourceCell = sightSource.Position.ToString(),
                        sourceRange = sightSource.TryGetComp<CompFog>().FieldOfViewWatcher.LastSightRange,
                        sourceHealth = sightSource.HitPoints, sourceSpawned = sightSource.Spawned,
                        enemyRootVisible = map.GetVisibility().IsShown(attacker.Faction, target.Position),
                        enemyVisibleBodyCells = Cells().Count(cell => map.GetVisibility().IsShown(attacker.Faction, cell)),
                        cells = Cells().Length, endTick = Find.TickManager.TicksGame };
                }, cancellationToken);
            }
            if (passed && saveAndReload)
            {
                string fixtureSaveName = saveName + "_PartialSymbiant_" + (melee ? "Melee" : "Ranged");
                string targetId = null, attackerId = null, sourceId = null, hostId = null;
                int savedTick = 0, mapId = 0;
                float savedHealth = 0;
                IntVec3[] savedBody = null;
                IntVec3 sourceCell = IntVec3.Invalid;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause the completed playback before saving.");
                    targetId = target.ThingID; attackerId = attacker.ThingID;
                    sourceId = sightSource.ThingID; hostId = host.ThingID;
                    mapId = map.uniqueID; savedTick = Find.TickManager.TicksGame;
                    savedHealth = (float)health.GetValue(target); savedBody = Cells();
                    sourceCell = sightSource.Position;
                }, cancellationToken);
                var saved = await ctx.Tools.CallAsync("rimworld/save_game", new { saveName = fixtureSaveName },
                    cancellationToken: cancellationToken);
                if (!saved.Succeeded() || !saved.ReadResult<bool>("success") || !saved.ReadResult<bool>("exists"))
                    throw new InvalidOperationException("The active partial-sight fight was not saved.");
                var loaded = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName = fixtureSaveName,
                    readiness = "visual", pauseIfNeeded = true, timeoutMs = 120000 }, cancellationToken: cancellationToken);
                if (!loaded.Succeeded() || !loaded.ReadResult<bool>("success"))
                    throw new InvalidOperationException("The active partial-sight fight did not reload.");
                object reloaded = null, resumed = null;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    map = Find.Maps.Single(loadedMap => loadedMap.uniqueID == mapId);
                    target = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.ThingID == targetId);
                    attacker = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.ThingID == attackerId);
                    sightSource = (ThingWithComps)map.listerThings.AllThings.Single(thing => thing.ThingID == sourceId);
                    host = (Pawn)AccessTools.Property(type, "LinkedHost").GetValue(target);
                    float currentHealth = (float)health.GetValue(target), hostInjuries = HostInjuries();
                    int seen = Cells().Count(cell => map.GetVisibility().IsShown(Faction.OfPlayer, cell));
                    bool rootVisible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position);
                    bool bodyMatches = new HashSet<IntVec3>(savedBody).SetEquals(Cells());
                    bool jobMatches = attacker.CurJob?.def == (melee ? JobDefOf.AttackMelee : JobDefOf.AttackStatic) &&
                        attacker.CurJob.targetA.Thing == target;
                    // Game.LoadGame deliberately runs one native tick before pausing.
                    bool valid = Find.TickManager.Paused && Find.TickManager.TicksGame == savedTick + 1 &&
                        currentHealth == savedHealth && bodyMatches && host.ThingID == hostId && !host.Spawned &&
                        hostInjuries == hostInjuriesBefore && sightSource.Spawned && sightSource.Position == sourceCell &&
                        sightSource.HitPoints == sightSourceHealthBefore && FogSettings.AISmart && jobMatches &&
                        !rootVisible && seen > 0 && seen < savedBody.Length;
                    passed &= valid;
                    reloaded = new { passed = valid, tick = Find.TickManager.TicksGame, savedTick,
                        health = currentHealth, savedHealth, bodyMatches, cells = Cells().Length,
                        host = host.ThingID, hostInjuries, hostSpawned = host.Spawned, jobMatches,
                        job = attacker.CurJob?.def.defName, target = attacker.CurJob?.targetA.Thing?.ThingID,
                        rootVisible, visibleBodyCells = seen, source = sightSource.ThingID,
                        sourceCell = sightSource.Position.ToString(), sourceHealth = sightSource.HitPoints };
                }, cancellationToken);
                object resumedPlayback = null;
                if (passed)
                {
                    var run = await ctx.Tools.CallAsync("rimworld/play_for", new { durationMs = 10000, speed = "Normal",
                        forceRequestedSpeed = false }, cancellationToken: cancellationToken);
                    if (!run.Succeeded() || !run.ReadResult<bool>("success"))
                        throw new InvalidOperationException("The loaded partial-sight fight did not resume.");
                    resumedPlayback = run;
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        float currentHealth = (float)health.GetValue(target), hostInjuries = HostInjuries();
                        int seen = Cells().Count(cell => map.GetVisibility().IsShown(Faction.OfPlayer, cell));
                        bool rootVisible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position);
                        bool attacked = attacker.mindState.lastAttackedTarget.Thing == target &&
                            attacker.mindState.lastAttackTargetTick >= savedTick;
                        bool valid = currentHealth < savedHealth && hostInjuries == hostInjuriesBefore && attacked &&
                            sightSource.Spawned && sightSource.HitPoints == sightSourceHealthBefore &&
                            !rootVisible && seen > 0 && seen < Cells().Length;
                        passed &= valid;
                        resumed = new { passed = valid, health = currentHealth, savedHealth, hostInjuries,
                            attacked, lastAttackTargetTick = attacker.mindState.lastAttackTargetTick,
                            rootVisible, visibleBodyCells = seen, sourceHealth = sightSource.HitPoints,
                            job = attacker.CurJob?.def.defName, endTick = Find.TickManager.TicksGame };
                    }, cancellationToken);
                }
                persistence = new { fixtureSaveName, reloaded, resumed, resumedPlayback };
            }
            return new { passed, melee, saveAndReload, rows, before, after, playback, persistence };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() => { FogSettings.BaseViewRange = oldRange; FogSettings.AISmart = oldEnemyFog; }, CancellationToken.None);
            var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual",
                pauseIfNeeded = true, timeoutMs = 120000 }, cancellationToken: CancellationToken.None);
            if (!restored.Succeeded() || !restored.ReadResult<bool>("success")) throw new InvalidOperationException("The unchanged base did not restore.");
        }
    }

    // The caller restarts the game before this mode and compares the returned
    // loaded values with the preparation report. No cached fixture refs survive.
    private static async Task<object> ResumeSavedFight(IRimBridgeContext ctx,
        CancellationToken cancellationToken, string saveName, bool melee)
    {
        int oldRange = FogSettings.BaseViewRange;
        bool oldEnemyFog = FogSettings.AISmart;
        string fixtureSaveName = saveName + "_PartialSymbiant_" + (melee ? "Melee" : "Ranged");
        var type = AccessTools.TypeByName("ZombieLand.ZombieSymbiant");
        if (type == null) throw new InvalidOperationException("Load the paired Zombieland profile.");
        Pawn target = null, attacker = null, host = null;
        ThingWithComps sightSource = null;
        Map map = null;
        var absoluteCells = AccessTools.Property(type, "AbsoluteCells");
        var health = AccessTools.Property(type, "SharedHealthCurrent");
        IntVec3[] Cells() => ((IEnumerable<IntVec3>)absoluteCells.GetValue(target)).ToArray();
        float Health() => (float)health.GetValue(target);
        float HostInjuries() => host.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(injury => injury.Severity);
        object Snapshot() => new
        {
            tick = Find.TickManager.TicksGame, paused = Find.TickManager.Paused,
            target = target.ThingID, health = Health(), body = Cells().Select(cell => cell.ToString()).ToArray(),
            host = host.ThingID, hostInjuries = HostInjuries(), hostSpawned = host.Spawned,
            attacker = attacker.ThingID, job = attacker.CurJob?.def.defName,
            jobTarget = attacker.CurJob?.targetA.Thing?.ThingID,
            lastAttackedTarget = attacker.mindState.lastAttackedTarget.Thing?.ThingID,
            lastAttackTargetTick = attacker.mindState.lastAttackTargetTick,
            source = sightSource.ThingID, sourceCell = sightSource.Position.ToString(),
            sourceHealth = sightSource.HitPoints, sourceSpawned = sightSource.Spawned,
            rootVisible = map.GetVisibility().IsShown(Faction.OfPlayer, target.Position),
            visibleBodyCells = Cells().Count(cell => map.GetVisibility().IsShown(Faction.OfPlayer, cell)),
            enemyFog = FogSettings.AISmart, baseViewRange = FogSettings.BaseViewRange
        };
        bool PartialView()
        {
            var cells = Cells();
            int seen = cells.Count(cell => map.GetVisibility().IsShown(Faction.OfPlayer, cell));
            return !map.GetVisibility().IsShown(Faction.OfPlayer, target.Position) && seen > 0 && seen < cells.Length;
        }
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                if (!Prefs.PauseOnLoad || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use native pause on load and colony fog.");
                FogSettings.BaseViewRange = 3;
                FogSettings.AISmart = true;
            }, cancellationToken);
            var loaded = await ctx.Tools.CallAsync("rimworld/load_game_ready", new
            {
                saveName = fixtureSaveName, readiness = "visual", pauseIfNeeded = true, timeoutMs = 120000
            }, cancellationToken: cancellationToken);
            if (!loaded.Succeeded() || !loaded.ReadResult<bool>("success"))
                throw new InvalidOperationException("The prepared partial-sight fight did not load.");
            object before = null, after = null;
            float initialHealth = 0, initialInjuries = 0;
            int initialSourceHealth = 0, initialTick = 0;
            bool loadedPassed = false, resumedPassed = false;
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap;
                target = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.GetType() == type);
                attacker = map.mapPawns.AllPawnsSpawned.Single(pawn => pawn.Name?.ToStringShort == "ZL_SymbiantCombat_Enemy");
                host = (Pawn)AccessTools.Property(type, "LinkedHost").GetValue(target);
                sightSource = map.listerThings.AllThings.OfType<ThingWithComps>()
                    .Single(thing => thing.def.defName == "SecurityBellSmall");
                initialHealth = Health(); initialInjuries = HostInjuries();
                initialSourceHealth = sightSource.HitPoints; initialTick = Find.TickManager.TicksGame;
                loadedPassed = Find.TickManager.Paused && target.Spawned && !host.Spawned &&
                    sightSource.Spawned && Cells().Length == 5 && PartialView() &&
                    attacker.CurJob?.def == (melee ? JobDefOf.AttackMelee : JobDefOf.AttackStatic) &&
                    attacker.CurJob.targetA.Thing == target;
                before = Snapshot();
            }, cancellationToken);
            object playback = null;
            if (loadedPassed)
            {
                var run = await ctx.Tools.CallAsync("rimworld/play_for", new
                {
                    durationMs = 10000, speed = "Normal", forceRequestedSpeed = false
                }, cancellationToken: cancellationToken);
                if (!run.Succeeded() || !run.ReadResult<bool>("success"))
                    throw new InvalidOperationException("The restarted partial-sight fight did not resume.");
                playback = run;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    resumedPassed = Health() < initialHealth && HostInjuries() == initialInjuries &&
                        sightSource.Spawned && sightSource.HitPoints == initialSourceHealth && PartialView() &&
                        attacker.mindState.lastAttackedTarget.Thing == target &&
                        attacker.mindState.lastAttackTargetTick >= initialTick;
                    after = Snapshot();
                }, cancellationToken);
            }
            return new { passed = loadedPassed && resumedPassed, fixtureSaveName, melee,
                resumeLoadedFight = true, loadedPassed, resumedPassed, before, after, playback };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                FogSettings.BaseViewRange = oldRange; FogSettings.AISmart = oldEnemyFog;
            }, CancellationToken.None);
            var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new
            {
                saveName, readiness = "visual", pauseIfNeeded = true, timeoutMs = 120000
            }, cancellationToken: CancellationToken.None);
            if (!restored.Succeeded() || !restored.ReadResult<bool>("success"))
                throw new InvalidOperationException("The unchanged base did not restore.");
        }
    }
}
