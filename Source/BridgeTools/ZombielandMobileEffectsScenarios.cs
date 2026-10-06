using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Unity.Collections;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

public sealed partial class ZombielandEffectsScenarios
{
    private static Pawn climbingZombie;
    private static Mote blockMote;
    private static int climbingDraws, blockDraws;
    private static IntVec3 climbingCullCell, blockCullCell;
    private static bool climbingShouldDraw, blockShouldDraw;
    private static readonly List<object> wallTicks = new();
    private static bool wallReadsPure, wallTickRotationCorrect, wallExtraReads;

    [Tool("totalfog/zombieland_wall_crossings", Description = "Run complete native Stumble wall crossings at the default threshold of eighteen with fourteen real supporting zombies. Their counts are registered through the production ExecuteMove path, never invented grid counts. Compare fog, camera and extra DrawPos reads; verify read purity, tick-owned rotation, landing, wall/roof preservation and live draw registration. Restore scoped settings/probes and reload the unchanged named base save.")]
    public static async Task<object> WallCrossings(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string saveName, int x = 212, int z = 79)
    {
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.wall-crossing-probe");
        bool oldBypass = FogSettings.OnlyOutsideColony;
        Map map = null;
        MapVisibility fog = null;
        var cell = new IntVec3(x, 0, z);
        var area = new CellRect();
        bool addedSight = false, passed = true;
        var owned = new List<Thing>();
        var options = new List<(object group, object minimum, object sound, object warning)>();
        FieldInfo values = null, minimum = null, sound = null, warning = null;
        object originalValues = null;
        var rows = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load the base fixture first.");
                fog = map.GetVisibility();
                if (!Find.TickManager.Paused || !fog.Initialized || !map.IsPlayerHome)
                    throw new InvalidOperationException("Use a paused, initialized home-map fixture.");
                cell = GenRadial.RadialCellsAround(cell, 25, true).FirstOrDefault(candidate =>
                {
                    var bounds = new CellRect(candidate.x - 2, candidate.z - 2, 5, 5);
                    return bounds.All(c => c.InBounds(map) && !map.fogGrid.IsFogged(c) && !fog.IsShown(Faction.OfPlayer, c)) &&
                        new[] { candidate, candidate + IntVec3.South, candidate + IntVec3.North, candidate + IntVec3.North * 2 }
                            .All(c => c.Standable(map) && !c.GetThingList(map).OfType<Pawn>().Any()) &&
                        GenAdj.CardinalDirections.All(d => (candidate + d).GetEdifice(map) == null) &&
                        map.roofGrid.RoofAt(candidate + IntVec3.North * 2) is not { isThickRoof: true } &&
                        map.roofGrid.RoofAt(candidate + IntVec3.North * 2) != RoofDefOf.RoofRockThin;
                });
                if (!cell.IsValid || cell == IntVec3.Zero) throw new InvalidOperationException("No remote wall-crossing strip exists.");
                area = new CellRect(cell.x - 2, cell.z - 2, 5, 5);
                var settingsType = AccessTools.TypeByName("ZombieLand.ZombieSettings");
                values = AccessTools.Field(settingsType, "Values"); originalValues = values.GetValue(null);
                minimum = AccessTools.Field(originalValues.GetType(), "minimumZombiesForWallPushing");
                sound = AccessTools.Field(originalValues.GetType(), "playWallAndSabotageSounds");
                warning = AccessTools.Field(originalValues.GetType(), "dangerousSituationMessage");
                var groups = new List<object> { originalValues };
                if (AccessTools.Field(settingsType, "ValuesOverTime").GetValue(null) is IEnumerable timeline)
                    foreach (var frame in timeline)
                        if (frame != null && AccessTools.Field(frame.GetType(), "values").GetValue(frame) is object group) groups.Add(group);
                foreach (var group in groups.Distinct())
                    options.Add((group, minimum.GetValue(group), sound.GetValue(group), warning.GetValue(group)));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ZombieLand.Zombie"), "CustomTick"),
                    prefix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(SuppressMobileFixtureCleanup)));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ZombieLand.ZombieStateHandler"), "WallPushing"),
                    prefix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(BeforeWallTick)),
                    postfix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(ObserveWallTick)));
                harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                    postfix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(ObserveClimbingDraw)));
            }, cancellationToken);
            foreach (string state in new[] { "hidden-near", "visible-near", "hidden-far", "visible-far", "bypass-near", "extra-reads-near" })
            {
                bool visible = state.StartsWith("visible") || state.StartsWith("extra"), far = state.EndsWith("far");
                Thing wall = null;
                int count = 0, startTick = 0, wallHitPoints = 0;
                await ctx.MainThread.InvokeAsync(() =>
                {
                    var manager = map.components.Single(c => c.GetType().FullName == "ZombieLand.TickManager");
                    var cached = AccessTools.Field(manager.GetType(), "allZombiesCached").GetValue(manager);
                    foreach (var t in owned)
                    {
                        if (!t.Destroyed) t.Destroy();
                        // Remove only this probe's completed fixtures from the
                        // manager cache before staging the next landing cell.
                        if (t is Pawn) AccessTools.Method(cached.GetType(), "Remove").Invoke(cached, new object[] { t });
                    }
                    owned.Clear(); SetSight(visible);
                    FogSettings.OnlyOutsideColony = state == "bypass-near";
                    foreach (var group in options.Select(o => o.group).Append(values.GetValue(null)).Distinct())
                    { minimum.SetValue(group, 18); sound.SetValue(group, false); warning.SetValue(group, false); }
                    wall = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog), cell + IntVec3.North, map);
                    owned.Add(wall); wallHitPoints = wall.HitPoints;
                    map.roofGrid.SetRoof(cell + IntVec3.North * 2, RoofDefOf.RoofConstructed);
                    var zombieType = AccessTools.TypeByName("ZombieLand.ZombieType");
                    var spawn = AccessTools.Method(AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions"), "SpawnZombie");
                    var handler = AccessTools.TypeByName("ZombieLand.ZombieStateHandler");
                    var grid = map.components.Single(c => c.GetType().FullName == "ZombieLand.PheromoneGrid");
                    var countMethod = AccessTools.Method(grid.GetType(), "GetZombieCountInBounds");
                    var supportCell = cell + IntVec3.South;
                    if ((int)countMethod.Invoke(grid, new object[] { supportCell }) != 0)
                        throw new InvalidOperationException("The supporting crowd cell already has native zombie counts.");
                    // Register one real pawn per production movement ledger entry.
                    // Holding the staged supporters here isolates the crossing;
                    // the climber itself runs only through normal playback.
                    for (int i = 0; i < 14; i++)
                    {
                        var supporter = (Pawn)spawn.Invoke(null, new object[] { supportCell, map, Enum.Parse(zombieType, "Normal"), true });
                        if (supporter == null) throw new InvalidOperationException("Supporting zombie spawn failed.");
                        owned.Add(supporter);
                        supporter.jobs.StartJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Stumble")), JobCondition.InterruptForced);
                        var driver = supporter.jobs.curDriver;
                        AccessTools.Field(driver.GetType(), "destination").SetValue(driver, supportCell);
                        AccessTools.Method(handler, "ExecuteMove").Invoke(null, new object[] { driver, supporter, grid });
                        var wait = JobMaker.MakeJob(JobDefOf.Wait); wait.expiryInterval = 1000;
                        supporter.jobs.StartJob(wait, JobCondition.InterruptForced);
                    }
                    count = (int)countMethod.Invoke(grid, new object[] { supportCell });
                    if (count != 14 || owned.OfType<Pawn>().Count(p => p.Position == supportCell && p.Spawned) != 14)
                        throw new InvalidOperationException("Native supporting count does not match the fourteen real pawns.");
                    climbingZombie = (Pawn)spawn.Invoke(null, new object[] { cell, map, Enum.Parse(zombieType, "Normal"), true });
                    if (climbingZombie == null) throw new InvalidOperationException("Climber spawn failed.");
                    owned.Add(climbingZombie);
                    AccessTools.Field(climbingZombie.GetType(), "wallPushCooldown").SetValue(climbingZombie, 0);
                    climbingZombie.jobs.StartJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Stumble")), JobCondition.InterruptForced);
                    wallTicks.Clear(); wallReadsPure = true; wallTickRotationCorrect = true;
                    wallExtraReads = state == "extra-reads-near"; climbingDraws = 0;
                    startTick = Find.TickManager.TicksGame;
                }, cancellationToken);
                var camera = await ctx.Tools.CallAsync("rimworld/jump_camera_to_cell",
                    new { x = far ? 20 : cell.x, z = far ? 220 : cell.z }, cancellationToken: cancellationToken);
                if (!camera.Succeeded()) throw new InvalidOperationException("Crossing camera setup failed.");
                await ctx.MainThread.InvokeAsync(() => Find.TickManager.CurTimeSpeed = TimeSpeed.Normal, cancellationToken);
                var run = await ctx.Game.RunUntilAsync(() =>
                {
                    bool done = climbingZombie.Destroyed || climbingZombie.Position == cell + IntVec3.North * 2 &&
                        (float)AccessTools.Field(climbingZombie.GetType(), "wallPushProgress").GetValue(climbingZombie) < 0 ||
                        Find.TickManager.TicksGame - startTick >= 240;
                    if (done) Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    return done;
                }, new RimBridgeWaitOptions { TimeoutMs = 30000 }, cancellationToken);
                if (!run.Success) throw new InvalidOperationException("Normal wall crossing did not finish: " + run.Message);
                await ctx.MainThread.InvokeAsync(() =>
                {
                    int registrations = map.dynamicDrawManager.DrawThings.Count(t => t == climbingZombie);
                    bool landed = climbingZombie.Spawned && !climbingZombie.Dead && climbingZombie.Position == cell + IntVec3.North * 2 &&
                        (float)AccessTools.Field(climbingZombie.GetType(), "wallPushProgress").GetValue(climbingZombie) < 0;
                    bool shouldHaveDraws = !far && (visible || state == "bypass-near");
                    bool valid = landed && wall.Spawned && wall.HitPoints == wallHitPoints && map.roofGrid.RoofAt(climbingZombie.Position) == null &&
                        registrations == 1 && wallTicks.Count == 101 && wallReadsPure && wallTickRotationCorrect &&
                        (int)minimum.GetValue(values.GetValue(null)) == 18 && (shouldHaveDraws ? climbingDraws > 0 : climbingDraws == 0);
                    passed &= valid;
                    rows.Add(new { state, passed = valid, landed, wallReadsPure, wallTickRotationCorrect, supportingZombies = count,
                        defaultMinimum = 18, nativeWallTicks = wallTicks.Count, elapsedGameTicks = Find.TickManager.TicksGame - startTick,
                        registrations, drawCalls = climbingDraws, shouldHaveDraws, wallPreserved = wall.Spawned && wall.HitPoints == wallHitPoints,
                        roofRemoved = map.roofGrid.RoofAt(climbingZombie.Position) == null, ticks = wallTicks.ToArray() });
                }, cancellationToken);
            }
            return new { passed, normalPlayback = true, fakeHordeCounts = false, stagedSupporters = true, fixtureCell = cell.ToString(), rows };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id); Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                    foreach (var t in owned.Where(t => !t.Destroyed)) t.Destroy();
                    climbingZombie = null; wallTicks.Clear();
                    if (addedSight) SetSight(false);
                    foreach (var option in options)
                    { minimum.SetValue(option.group, option.minimum); sound.SetValue(option.group, option.sound); warning.SetValue(option.group, option.warning); }
                    if (originalValues != null) values.SetValue(null, originalValues);
                    FogSettings.OnlyOutsideColony = oldBypass;
                }, CancellationToken.None);
                var reload = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!reload.Succeeded()) throw new InvalidOperationException("Crossing fixture restoration failed.");
            }
            finally { gate.Release(); }
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight) return;
            foreach (var c in area)
            {
                int index = map.cellIndices.CellToIndex(c);
                if (visible) fog.IncrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                else fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
            }
            addedSight = visible;
        }
    }

    private static void BeforeWallTick(Pawn zombie, out int __state) => __state = zombie.Rotation.AsInt;
    private static void ObserveWallTick(Pawn zombie, int __state)
    {
        if (zombie != climbingZombie) return;
        float progress = (float)AccessTools.Field(zombie.GetType(), "wallPushProgress").GetValue(zombie);
        int beforeRead = zombie.Rotation.AsInt;
        var drawPos = zombie.DrawPos;
        int reads = wallExtraReads ? 25 : 1;
        for (int i = 1; i < reads; i++) _ = zombie.DrawPos;
        bool pure = beforeRead == zombie.Rotation.AsInt;
        bool tickCorrect = progress < 0 || beforeRead == (__state + (GenTicks.TicksGame % 10 == 0 ? 1 : 0)) % 4;
        wallReadsPure &= pure; wallTickRotationCorrect &= tickCorrect;
        wallTicks.Add(new { tick = GenTicks.TicksGame, progress, position = zombie.Position.ToString(), drawPos = drawPos.ToString(),
            rotationBeforeTick = __state, rotationAfterTick = beforeRead, rotationAfterReads = zombie.Rotation.AsInt, reads, pure, tickCorrect });
    }

    [Tool("totalfog/zombieland_mobile_effects", Description = "Observe a staged climbing zombie at the native drawn-cell boundary and a production attached block mote through sight transitions. Then reuse Zombieland's real wall-crossing contract with zero-threat fixture cleanup suppressed. Restores settings/diagnostics and reloads the unchanged named save.")]
    public static async Task<object> MobileEffects(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string zombieId, string defenderId, string saveName, int frames = 12)
    {
        if (frames < 3 || frames > 30) throw new ArgumentException("Use 3..30 frames per sample.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.zombieland-mobile-effects-probe");
        int oldRange = FogSettings.BaseViewRange, startTick = -1;
        var rows = new List<object>();
        Pawn viewer = null;
        Map map = null;
        MapVisibility fog = null;
        IntVec3 start = IntVec3.Invalid;
        bool passed = true;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load a staged fixture first.");
                fog = map.GetComponent<MapVisibility>();
                if (!Find.TickManager.Paused || !fog.Initialized || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use paused initialized colony fog with bypass disabled.");
                climbingZombie = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == zombieId);
                var defender = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == defenderId);
                if (climbingZombie.GetType().FullName != "ZombieLand.Zombie" ||
                    defender.Faction == Faction.OfPlayer || climbingZombie.Faction == Faction.OfPlayer ||
                    defender.Dead || climbingZombie.Dead || defender == climbingZombie)
                    throw new InvalidOperationException("Stage two distinct living non-player pawns, including an ordinary zombie.");
                start = GenRadial.RadialCellsAround(climbingZombie.Position, 25, true).FirstOrDefault(cell =>
                    Enumerable.Range(-6, 15).All(offset =>
                    {
                        var p = cell + IntVec3.East * offset;
                        return p.InBounds(map) && p.Standable(map) && !map.fogGrid.IsFogged(p);
                    }));
                if (!start.IsValid || start == IntVec3.Zero)
                    throw new InvalidOperationException("No open east-west strip exists near the staged zombie.");
                climbingZombie.pather.StopDead();
                climbingZombie.Position = start;
                viewer = map.mapPawns.FreeColonistsSpawned.First(p => p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null);
                startTick = Find.TickManager.TicksGame;
                FogSettings.BaseViewRange = 5;
                foreach (var observer in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer &&
                    p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null).ToArray())
                    Move(observer, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
                foreach (var source in fog.fowWatchers) source.UpdateFoV(true);
                var type = climbingZombie.GetType();
                AccessTools.Field(type, "wallPushStart").SetValue(climbingZombie, start.ToVector3Shifted());
                AccessTools.Field(type, "wallPushDestination").SetValue(climbingZombie, (start + IntVec3.East * 2).ToVector3Shifted());
                harmony.Patch(AccessTools.Method(typeof(DynamicDrawManager), "ComputeCulledThings"),
                    postfix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(ObserveMobileCull)) { priority = Priority.Last });
                harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                    postfix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(ObserveClimbingDraw)));
                harmony.Patch(AccessTools.DeclaredMethod(typeof(Mote), "DrawAt"),
                    postfix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(ObserveBlockDraw)));
            }, cancellationToken);
            var camera = await ctx.Tools.CallAsync("rimworld/jump_camera_to_cell", new { x = start.x, z = start.z }, cancellationToken: cancellationToken);
            if (!camera.Succeeded()) throw new InvalidOperationException("Camera staging failed.");
            foreach (float progress in new[] { 0f, .5f, .9f })
                foreach (int observerOffset in new[] { -4, 6 })
                {
                    bool expected = await ctx.MainThread.InvokeAsync(() =>
                    {
                        AccessTools.Field(climbingZombie.GetType(), "wallPushProgress").SetValue(climbingZombie, progress);
                        Move(viewer, start + IntVec3.East * observerOffset);
                        var drawn = climbingZombie.DrawPos.ToIntVec3();
                        bool logical = fog.IsShown(Faction.OfPlayer, climbingZombie.Position);
                        bool visible = fog.IsShown(Faction.OfPlayer, drawn);
                        if (progress == .9f && logical == visible)
                            throw new InvalidOperationException("The fixture does not separate logical and rendered-cell sight.");
                        climbingDraws = 0; climbingCullCell = IntVec3.Invalid; climbingShouldDraw = false;
                        return visible;
                    }, cancellationToken);
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        bool ok = climbingCullCell == climbingZombie.DrawPos.ToIntVec3() &&
                            climbingShouldDraw == expected && (expected ? climbingDraws > 0 : climbingDraws == 0);
                        passed &= ok;
                        rows.Add(new { kind = "climbing", progress, observerOffset, expected, passed = ok,
                            logicalCell = climbingZombie.Position.ToString(), drawnCell = climbingZombie.DrawPos.ToIntVec3().ToString(),
                            nativeCullCell = climbingCullCell.ToString(), logicalVisible = fog.IsShown(Faction.OfPlayer, climbingZombie.Position),
                            shouldDraw = climbingShouldDraw, drawCalls = climbingDraws });
                    }, cancellationToken);
                }
            await ctx.MainThread.InvokeAsync(() =>
            {
                AccessTools.Field(climbingZombie.GetType(), "wallPushProgress").SetValue(climbingZombie, -1f);
                var defender = map.mapPawns.AllPawnsSpawned.Single(p => p.ThingID == defenderId);
                defender.pather.StopDead();
                defender.Position = start + IntVec3.North;
                var def = DefDatabase<ThingDef>.GetNamed("Mote_Block");
                var before = new HashSet<Thing>(map.dynamicDrawManager.DrawThings.Where(t => t.def == def));
                AccessTools.Method(AccessTools.TypeByName("ZombieLand.Tools"), "CastBlockBubble")
                    .Invoke(null, new object[] { climbingZombie, defender });
                blockMote = (Mote)map.dynamicDrawManager.DrawThings.Single(t => t.def == def && !before.Contains(t));
                if (blockMote.link1.Target.Thing != defender) throw new InvalidOperationException("Block mote did not attach to the defender.");
                // Resolve its real attachment once without advancing the world;
                // the paused mote then keeps the production draw position.
                AccessTools.DeclaredMethod(typeof(MoteAttached), "TimeInterval").Invoke(blockMote, new object[] { 0f });
                blockMote.ForceSpawnTick(startTick - 6);
            }, cancellationToken);
            foreach (bool visible in new[] { false, true, false })
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    Move(viewer, visible ? blockMote.Position : blockMote.Position + IntVec3.East * 8);
                    blockDraws = 0; blockCullCell = IntVec3.Invalid; blockShouldDraw = false;
                }, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                await ctx.MainThread.InvokeAsync(() =>
                {
                    bool inSight = fog.IsShown(Faction.OfPlayer, blockMote.Position);
                    bool ok = inSight == visible && blockMote.Spawned && blockMote.Alpha > 0 && blockShouldDraw == visible &&
                        (visible ? blockDraws > 0 : blockDraws == 0);
                    passed &= ok;
                    rows.Add(new { kind = "block-mote", expected = visible, inSight, passed = ok, shouldDraw = blockShouldDraw,
                        drawCalls = blockDraws, nativeCullCell = blockCullCell.ToString(), drawnCell = blockMote.DrawPos.ToIntVec3().ToString(),
                        attachedTo = blockMote.link1.Target.Thing.ThingID, alpha = blockMote.Alpha });
                }, cancellationToken);
            }
            int pausedEnd = await ctx.MainThread.InvokeAsync(() => Find.TickManager.TicksGame, cancellationToken);
            await ctx.MainThread.InvokeAsync(() => harmony.Patch(AccessTools.Method(climbingZombie.GetType(), "CustomTick"),
                prefix: new HarmonyMethod(typeof(ZombielandEffectsScenarios), nameof(SuppressMobileFixtureCleanup))), cancellationToken);
            var crossing = await ctx.Tools.CallAsync("zombieland/wall_push_over_wall_contract", new { x = start.x - 15, z = start.z }, cancellationToken: cancellationToken);
            return new { presentationPassed = passed && startTick == pausedEnd, rows, startTick, pausedEnd, crossing,
                crossingRequiresSuccessCheck = true, suppressedZeroThreatCleanupDuringCrossing = true,
                zombieMvid = climbingZombie.GetType().Assembly.ManifestModule.ModuleVersionId.ToString() };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id);
                    climbingZombie = null; blockMote = null;
                    FogSettings.BaseViewRange = oldRange;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                }, CancellationToken.None);
                var reload = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!reload.Succeeded()) throw new InvalidOperationException("Fixture restoration failed.");
            }
            finally { gate.Release(); }
        }
    }

    private static void SuppressMobileFixtureCleanup(ref float threatLevel)
    {
        // Isolated diagnostic save only: the zero-threat fixture otherwise
        // deletes its newly spawned climber before the progress window ends.
        if (threatLevel <= 0f) threatLevel = 1f;
    }
    private static void ObserveMobileCull(NativeArray<DynamicDrawManager.ThingCullDetails> details, List<Thing> ___drawThings)
    {
        for (int i = 0; i < details.Length; i++)
            if (___drawThings[i] == climbingZombie) { climbingCullCell = details[i].cell; climbingShouldDraw = details[i].shouldDraw; }
            else if (___drawThings[i] == blockMote) { blockCullCell = details[i].cell; blockShouldDraw = details[i].shouldDraw; }
    }
    private static void ObserveClimbingDraw(Pawn __instance, DrawPhase phase, bool __runOriginal)
    {
        if (__runOriginal && __instance == climbingZombie && phase == DrawPhase.Draw) climbingDraws++;
    }
    private static void ObserveBlockDraw(Mote __instance, bool __runOriginal)
    {
        if (__runOriginal && __instance == blockMote) blockDraws++;
    }
}
