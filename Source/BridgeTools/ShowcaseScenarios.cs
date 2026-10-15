using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using TotalFog.Watchtowers;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TotalFog.BridgeTools;

/// <summary>Photography fixtures only. Never loaded by the player mod.</summary>
public sealed class ShowcaseScenarios
{
    [Serializable]
    public sealed class Recipe
    {
        public int schemaVersion,
            width,
            height,
            fps,
            seed,
            visionRange;
        public float uiScale;
        public string saveName;
        public Shot[] shots;
    }

    [Serializable]
    public sealed class Shot
    {
        public string id,
            title,
            mode;
        public float seconds,
            x,
            z,
            endX,
            endZ,
            zoom,
            endZoom;
    }

    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool(
        "totalfog/showcase",
        Description = "Create or photograph the isolated ShowcaseUserData colony. Pin and verify a 1920x1080 window and UI scale 1. Capture uses the checked-in recipe and reference save, native jobs, native fog and native screenshots; never alters player saves."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string recipePath,
        string action = "capture",
        string outputDirectory = null,
        bool video = true,
        string shotId = null
    )
    {
        var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(recipePath));
        if (
            recipe.schemaVersion != 1
            || recipe.width != 1920
            || recipe.height != 1080
            || recipe.uiScale != 1
            || recipe.fps != 30
            || recipe.shots == null
            || recipe.shots.Any(s => s.seconds <= 0 || s.seconds > 10)
            || recipe.shots.Sum(s => s.seconds) > 20
            || action is not ("stage" or "capture")
        )
            throw new ArgumentException("Invalid or unsupported showcase recipe.");
        if (action == "capture" && string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("Capture needs an output directory.");
        await gate.WaitAsync(cancellationToken);
        var proof = RimBridgeEvidence.CreateManifest("totalfog/showcase");
        proof.saveName = recipe.saveName;
        try
        {
            await On(() =>
            {
                if (
                    !Path.GetFullPath(GenFilePaths.SaveDataFolderPath)
                        .TrimEnd(Path.DirectorySeparatorChar)
                        .EndsWith(Path.DirectorySeparatorChar + "ShowcaseUserData")
                    || Compatibility.MultiplayerIntegration.Active
                )
                    throw new InvalidOperationException(
                        "Use the isolated ShowcaseUserData profile."
                    );
                Prefs.UIScale = recipe.uiScale;
                Prefs.AdaptiveTrainingEnabled = false;
                Prefs.EdgeScreenScroll = false;
                Prefs.PlantWindSway = false;
                AccessTools
                    .Field(typeof(DebugViewSettings), "showTpsCounter")
                    ?.SetValue(null, false);
                AccessTools
                    .Field(typeof(DebugViewSettings), "showFpsCounter")
                    ?.SetValue(null, false);
                Screen.SetResolution(recipe.width, recipe.height, false);
                Prefs.PauseOnLoad = true;
            });
            await ctx.Game.FramesAsync(10, cancellationToken);
            await On(() =>
            {
                CheckViewport(recipe);
                proof.gameVersion = VersionControl.CurrentVersionStringWithRev;
                proof.modVersion = FogSettings.CurrentVersion;
                proof.environment.details = new
                {
                    width = Screen.width,
                    height = Screen.height,
                    fullscreen = Screen.fullScreen,
                    uiScale = Prefs.UIScale,
                    game = VersionControl.CurrentVersionStringWithRev,
                    gameplayMvid = typeof(TotalFogMod).Assembly.ManifestModule.ModuleVersionId,
                    graphicsDevice = SystemInfo.graphicsDeviceName,
                    activeMods = LoadedModManager
                        .RunningModsListForReading.Select(m => m.PackageId)
                        .ToArray(),
                    recipe.seed,
                    recipe.visionRange,
                    ticksPerVideoFrame = 2,
                };
            });
            if (action == "stage")
            {
                await On(() => Stage(recipe));
                await Step(90);
                // Visit rooms and the old path with an actual colonist, then
                // leave. These are native observations, not painted fog masks.
                foreach (
                    var cell in new[]
                    {
                        new IntVec3(97, 0, 121),
                        new IntVec3(107, 0, 121),
                        new IntVec3(118, 0, 121),
                        new IntVec3(100, 0, 151),
                    }
                )
                {
                    await On(() =>
                    {
                        var pawn = Actor("Bea");
                        pawn.Position = cell;
                        pawn.Notify_Teleported();
                        RefreshSight();
                    });
                    await Step(35);
                }
                await On(() =>
                {
                    var pawn = Actor("Bea");
                    pawn.Position = new IntVec3(140, 0, 106);
                    pawn.Notify_Teleported();
                    RefreshSight();
                    Find.CameraDriver.SetRootPosAndSize(new Vector3(112, 0, 111), 38);
                });
                await Step(35);
                await Call("rimworld/save_game", new { saveName = recipe.saveName });
            }
            else
            {
                Directory.CreateDirectory(outputDirectory);
                foreach (var shot in recipe.shots.Where(s => shotId == null || s.id == shotId))
                {
                    await Call(
                        "rimworld/load_game_ready",
                        new
                        {
                            saveName = recipe.saveName,
                            readiness = "visual",
                            pauseIfNeeded = true,
                        }
                    );
                    await On(() => PrepareShot(recipe, shot));
                    if (shot.mode == "wide")
                        await Step(180);
                    if (shot.mode is "vision" or "audio")
                        await Call(
                            "rimworld/open_mod_settings",
                            new { modId = "brrainz.totalfog" }
                        );
                    await ctx.Game.FramesAsync(5, cancellationToken);
                    int frames =
                        video && shot.mode is not ("vision" or "audio")
                            ? (int)Math.Round(shot.seconds * recipe.fps)
                            : 1;
                    if (!video && shot.mode is ("explore" or "tower"))
                        await Step(150);
                    var folder = Path.Combine(outputDirectory, shot.id);
                    Directory.CreateDirectory(folder);
                    int start = await ctx.MainThread.InvokeAsync(
                        () => Find.TickManager.TicksGame,
                        cancellationToken
                    );
                    for (int frame = 0; frame < frames; frame++)
                    {
                        float t = frames == 1 ? .5f : (float)frame / (frames - 1);
                        await On(() =>
                        {
                            CheckViewport(recipe);
                            float ease = t * t * (3 - 2 * t);
                            Find.CameraDriver.SetRootPosAndSize(
                                new Vector3(
                                    Mathf.Lerp(shot.x, shot.endX, ease),
                                    0,
                                    Mathf.Lerp(shot.z, shot.endZ, ease)
                                ),
                                Mathf.Lerp(shot.zoom, shot.endZoom, ease)
                            );
                        });
                        if (shot.mode is not ("vision" or "audio"))
                            await Step(2);
                        await On(Messages.Clear);
                        var capture = await ctx.Tools.CallAsync(
                            "rimworld/take_screenshot",
                            new
                            {
                                fileName = "frame-" + frame.ToString("D5"),
                                includeTargets = false,
                                suppressMessage = true,
                                outputDirectory = folder,
                            },
                            cancellationToken: cancellationToken
                        );
                        if (!capture.Succeeded())
                            throw new InvalidOperationException(
                                "Native frame capture failed: " + capture.Error
                            );
                        if (frame == frames / 2)
                            proof.captures.Add(
                                new RimBridgeEvidenceCapture
                                {
                                    label = shot.id,
                                    path = Path.Combine(
                                        folder,
                                        "frame-" + frame.ToString("D5") + ".png"
                                    ),
                                    kind = "native-screenshot",
                                }
                            );
                    }
                    int end = await ctx.MainThread.InvokeAsync(
                        () => Find.TickManager.TicksGame,
                        cancellationToken
                    );
                    proof.assertions.Add(
                        new RimBridgeEvidenceAssertion
                        {
                            name = shot.id + " fixed simulation interval",
                            success =
                                end - start == (shot.mode is "vision" or "audio" ? 0 : 2 * frames),
                            expected = shot.mode is "vision" or "audio" ? 0 : 2 * frames,
                            actual = end - start,
                        }
                    );
                }
                await Call(
                    "rimworld/load_game_ready",
                    new
                    {
                        saveName = recipe.saveName,
                        readiness = "visual",
                        pauseIfNeeded = true,
                    }
                );
            }
            proof.success = proof.assertions.All(a => a.success);
            proof.completedAtUtc = DateTimeOffset.UtcNow;
            return proof;
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (Current.Game != null)
                        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                },
                CancellationToken.None
            );
            gate.Release();
        }

        Task On(Action action) => ctx.MainThread.InvokeAsync(action, cancellationToken);
        async Task Step(int ticks)
        {
            var result = await ctx.Game.StepTicksAsync(ticks, cancellationToken: cancellationToken);
            if (!result.Success || result.AdvancedTicks != ticks)
                throw new InvalidOperationException(
                    "Native simulation did not advance the requested ticks."
                );
        }
        async Task Call(string name, object args)
        {
            var result = await ctx.Tools.CallAsync(
                name,
                args,
                cancellationToken: cancellationToken
            );
            if (!result.Succeeded())
                throw new InvalidOperationException(name + " failed: " + result.Error);
        }
    }

    private static void CheckViewport(Recipe recipe)
    {
        if (
            Screen.width != recipe.width
            || Screen.height != recipe.height
            || Screen.fullScreen
            || Prefs.UIScale != recipe.uiScale
            || UI.screenWidth != recipe.width
            || UI.screenHeight != recipe.height
        )
            throw new InvalidOperationException(
                $"Capture viewport differs: {Screen.width}x{Screen.height}, UI {Prefs.UIScale}, fullscreen {Screen.fullScreen}."
            );
    }

    private static Pawn Actor(string name) =>
        Find.CurrentMap.mapPawns.FreeColonistsSpawned.Single(p => p.Name.ToStringShort == name);

    private static void RefreshSight()
    {
        foreach (var watcher in Find.CurrentMap.GetVisibility().fowWatchers.ToArray())
            watcher.UpdateFoV(true);
        Find.CurrentMap.mapDrawer.RegenerateEverythingNow();
    }

    private static void PrepareShot(Recipe recipe, Shot shot)
    {
        if (Find.CurrentMap == null || !Find.TickManager.Paused)
            throw new InvalidOperationException("Load the showcase fixture paused.");
        Prefs.DevMode = false;
        FogSettings.BaseViewRange = recipe.visionRange;
        FogSettings.DoAudioCheck = true;
        Messages.Clear();
        foreach (var letter in Find.LetterStack.LettersListForReading.ToArray())
            Find.LetterStack.RemoveLetter(letter);
        Find.Selector.ClearSelection();
        if (Find.MainTabsRoot.OpenTab != null)
            Find.MainTabsRoot.EscapeCurrentTab();
        if (shot.mode == "explore")
            Actor("Bea")
                .jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(JobDefOf.Goto, new IntVec3(175, 0, 106)),
                    JobTag.Misc
                );
        if (shot.mode is "tower" or "wide")
        {
            var tower = Find
                .CurrentMap.listerThings.AllThings.OfType<Building_Watchtower>()
                .Single(t => t.Position.x == 123);
            Actor("Ada")
                .jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(WatchtowerDefOf.TotalFog_WatchtowerWatch, tower),
                    JobTag.Misc
                );
        }
        AccessTools
            .Field(typeof(FogSettings), "settingsPage")
            .SetValue(
                null,
                Enum.ToObject(
                    AccessTools.Inner(typeof(FogSettings), "SettingsPage"),
                    shot.mode == "audio" ? 3 : 1
                )
            );
        AccessTools.Field(typeof(FogSettings), "scrollPosition").SetValue(null, Vector2.zero);
        RefreshSight();
        Find.CameraDriver.SetRootPosAndSize(new Vector3(shot.x, 0, shot.z), shot.zoom);
    }

    private static void Stage(Recipe recipe)
    {
        var map =
            Find.CurrentMap
            ?? throw new InvalidOperationException("Generate a native debug map first.");
        if (!Find.TickManager.Paused || map.Size.x < 200 || map.Size.z < 200)
            throw new InvalidOperationException(
                "Use a paused showcase map of at least 200x200 cells."
            );
        var area = new CellRect(73, 84, 120, 88);
        foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToArray())
            pawn.Destroy(DestroyMode.Vanish);
        foreach (var cell in area)
        {
            foreach (var thing in cell.GetThingList(map).ToArray())
                if (thing.def.destroyable)
                    thing.Destroy(DestroyMode.Vanish);
                else
                    thing.DeSpawn(DestroyMode.Vanish);
            map.roofGrid.SetRoof(cell, null);
            map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
            map.snowGrid.SetDepth(cell, 0);
        }
        FogSettings.BaseViewRange = recipe.visionRange;
        FogSettings.OnlyOutsideColony = false;
        Prefs.DevMode = false;
        map.weatherManager.TransitionTo(WeatherDefOf.Clear);
        // The immutable reference keeps the same local summer noon lighting.
        Find.TickManager.DebugSetTicksGame(
            Find.TickManager.TicksGame
                + 15 * 60000
                + Mathf.RoundToInt((.5f - GenLocalDate.DayPercent(map)) * 60000)
        );
        Rand.PushState(recipe.seed);
        try
        {
            Thing Place(string def, int x, int z, string stuff = null, Rot4? rotation = null)
            {
                var definition =
                    DefDatabase<ThingDef>.GetNamedSilentFail(def)
                    ?? throw new InvalidOperationException("Showcase needs native ThingDef " + def);
                var thing = ThingMaker.MakeThing(
                    definition,
                    definition.MadeFromStuff && stuff != null ? ThingDef.Named(stuff) : null
                );
                if (thing.def.CanHaveFaction)
                    thing.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(thing, new IntVec3(x, 0, z), map, rotation ?? Rot4.North);
                thing.TryGetComp<CompRefuelable>()?.Refuel(20);
                return thing;
            }
            void Floor(int x, int z, int w, int h, string terrain)
            {
                foreach (var cell in new CellRect(x, z, w, h))
                    map.terrainGrid.SetTerrain(cell, TerrainDef.Named(terrain));
            }
            Floor(90, 99, 36, 29, "PavedTile");
            Floor(94, 104, 28, 11, "SoilRich");
            for (int x = 90; x <= 125; x++)
            {
                if (x < 122)
                    Place(x == 106 ? "Door" : "Wall", x, 100, "BlocksGranite");
                Place("Wall", x, 127, "BlocksGranite");
            }
            for (int z = 101; z < 127; z++)
            {
                Place("Wall", 90, z, "BlocksGranite");
                Place("Wall", 125, z, "BlocksGranite");
            }
            Place("TotalFog_Watchtower", 123, 100, "BlocksGranite", Rot4.South);
            Place("TotalFog_Watchtower", 88, 100, "WoodLog", Rot4.South);
            for (int x = 91; x < 125; x++)
                Place(x is 97 or 107 or 118 ? "Door" : "Wall", x, 116, "WoodLog");
            for (int z = 117; z <= 126; z++)
                foreach (int x in new[] { 102, 112 })
                    Place("Wall", x, z, "WoodLog");
            Floor(91, 117, 34, 10, "WoodPlankFloor");
            foreach (var cell in new CellRect(91, 117, 34, 10))
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            foreach (int x in new[] { 93, 97, 104, 108 })
                Place("Bed", x, 124, "WoodLog");
            Place("Table2x2c", 96, 120, "WoodLog");
            Place("DiningChair", 94, 120, "WoodLog", Rot4.East);
            Place("DiningChair", 98, 120, "WoodLog", Rot4.West);
            Place("FueledStove", 121, 124, "Steel");
            Place("TableButcher", 116, 124, "WoodLog");
            Place("ChessTable", 105, 119, "WoodLog");
            Place("DiningChair", 105, 118, "WoodLog", Rot4.North);
            foreach (int x in new[] { 94, 110, 123 })
                Place("TorchLamp", x, 122);
            var stockpile = new Zone_Stockpile(
                StorageSettingsPreset.DefaultStockpile,
                map.zoneManager
            );
            map.zoneManager.RegisterZone(stockpile);
            foreach (var cell in new CellRect(113, 118, 11, 4))
                stockpile.AddCell(cell);
            foreach (var cell in new CellRect(90, 99, 36, 29))
                map.areaManager.Home[cell] = true;
            foreach (var cell in new CellRect(95, 105, 10, 8))
            {
                var plant = (Plant)Place("Plant_Rice", cell.x, cell.z);
                plant.Growth = .65f;
            }
            foreach (var cell in new CellRect(111, 105, 9, 8))
            {
                var plant = (Plant)Place("Plant_Cotton", cell.x, cell.z);
                plant.Growth = .8f;
            }
            Floor(106, 100, 3, 16, "PavedTile");
            Floor(126, 104, 52, 3, "Gravel");
            for (int i = 0; i < 1000; i++)
            {
                int x = Rand.Range(74, 192),
                    z = Rand.Range(85, 171);
                if (
                    new CellRect(87, 97, 42, 34).Contains(new IntVec3(x, 0, z))
                    || z is >= 103 and <= 108
                )
                    continue;
                var cell = new IntVec3(x, 0, z);
                if (cell.GetThingList(map).Count != 0)
                    continue;
                var plant = (Plant)Place(i % 7 == 0 ? "Plant_TreeOak" : "Plant_Grass", x, z);
                plant.Growth = i % 7 == 0 ? .9f : .6f;
            }
            foreach (
                var item in new[]
                {
                    ("Steel", 114),
                    ("WoodLog", 116),
                    ("MedicineIndustrial", 118),
                    ("MealSurvivalPack", 120),
                }
            )
            {
                var thing = Place(item.Item1, item.Item2, 119);
                thing.stackCount = Math.Min(
                    thing.def.stackLimit,
                    item.Item1 == "MedicineIndustrial" ? 12 : 35
                );
            }
            foreach (int z in new[] { 118, 120, 121 })
                Place("MealSurvivalPack", 120, z).stackCount = 10;
            map.resourceCounter.UpdateResourceCounts();
            string[] names = { "Ada", "Bea", "Cam", "Dee" };
            IntVec3[] positions =
            {
                new(123, 0, 97),
                new(140, 0, 106),
                new(97, 0, 120),
                new(108, 0, 111),
            };
            for (int i = 0; i < names.Length; i++)
            {
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                pawn.Name = new NameTriple(names[i], names[i], "Vale");
                foreach (var hediff in pawn.health.hediffSet.hediffs.ToArray())
                    pawn.health.RemoveHediff(hediff);
                pawn.apparel.Wear(
                    (Apparel)
                        ThingMaker.MakeThing(
                            ThingDef.Named("Apparel_Parka"),
                            ThingDef.Named("Cloth")
                        ),
                    false
                );
                GenSpawn.Spawn(pawn, positions[i], map);
                pawn.drafter.Drafted = i < 2;
                pawn.jobs.StartJob(JobMaker.MakeJob(JobDefOf.Wait, 60000));
                if (pawn.needs.food != null)
                    pawn.needs.food.CurLevelPercentage = 1;
                if (pawn.needs.rest != null)
                    pawn.needs.rest.CurLevelPercentage = 1;
            }
            map.GetVisibility().DebugSetFog(new CellRect(0, 0, map.Size.x, map.Size.z), true, true);
            RefreshSight();
        }
        finally
        {
            Rand.PopState();
        }
    }
}
