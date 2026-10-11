using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class WatchtowerVisualScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static readonly Dictionary<Pawn, Vector3> projectedOccupants = new();
    private static ThingDef prototypeDef;

    [Tool(
        "totalfog/watchtower_visuals",
        Description = "Render test-only 3x3 tower artwork using native stuff, buildings and walls. Default stages eight wood/granite occupancy/rotation sketches. silhouetteOutlines uses broad shaded faces, soft bevels and joined contours; retainControls keeps two older controls, or false captures a larger current-only overview. materialComparison instead captures ten labelled wood/stone/metal towers, each with matching native walls to west/east. nativeFrontPieces optionally copies clipped wall-front pieces at native scale. No climb, sight, combat or placement claim. Requires a paused named fixture, advances no ticks and reloads that unchanged save. Definitions and drawing code are excluded from player ZIPs."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel,
        bool nativeFrontPieces = false,
        bool silhouetteOutlines = false,
        bool retainControls = true,
        bool materialComparison = false
    )
    {
        if (
            string.IsNullOrEmpty(saveName)
            || string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Supply a save name and a plain evidence label.");
        if (materialComparison && nativeFrontPieces)
            throw new ArgumentException("Use the separate native front-piece comparison.");
        await gate.WaitAsync(cancellationToken);
        var manifest = RimBridgeEvidence.CreateManifest(
            "totalfog/watchtower_visuals",
            evidenceLabel
        );
        manifest.saveName = saveName;
        var harmony = new Harmony("brrainz.totalfog.watchtower-visual-prototype");
        var owned = new List<Thing>();
        var drawings = new List<WatchtowerVisualPrototype>();
        Map map = null;
        MapVisibility fog = null;
        CellRect area = default;
        int tick = 0;
        bool staged = false;
        bool oldFade = SectionLayerFog.PrefEnableFade;
        bool oldLightingOverlay = DebugViewSettings.drawLightingOverlay;
        var oldHighlight = Prefs.DotHighlightDisplayMode;
        int areaWidth = materialComparison ? 27 : 19;
        int areaHeight = materialComparison ? 28 : 21;
        int expectedTowers = materialComparison ? 10 : 8;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    fog = map.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized test map.");
                    var candidates = materialComparison
                        ? map.AllCells
                        : GenRadial.RadialCellsAround(
                            new IntVec3(map.Size.x - 40, 0, map.Size.z / 3),
                            65,
                            true
                        );
                    var origin = candidates.First(c =>
                        new CellRect(c.x, c.z, areaWidth, areaHeight).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !map.fogGrid.IsFogged(p)
                            && !p.Roofed(map)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    area = new CellRect(origin.x, origin.z, areaWidth, areaHeight);
                    tick = Find.TickManager.TicksGame;
                    EnsureDefinition();
                    harmony.Patch(
                        AccessTools.PropertyGetter(typeof(Pawn), nameof(Pawn.DrawPos)),
                        postfix: new HarmonyMethod(
                            typeof(WatchtowerVisualScenarios),
                            nameof(ProjectOccupant)
                        )
                    );
                    SectionLayerFog.PrefEnableFade = false;
                    if (materialComparison)
                        DebugViewSettings.drawLightingOverlay = false;
                    Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.None;
                    Find.Selector.ClearSelection();
                    foreach (var cell in area)
                    {
                        foreach (var thing in cell.GetThingList(map).ToArray())
                            thing.Destroy(DestroyMode.Vanish);
                        fog.IncrementSeen(Faction.OfPlayer, map.cellIndices.CellToIndex(cell));
                    }
                    staged = true;
                    Rand.PushState(763119);
                    try
                    {
                        if (materialComparison)
                        {
                            var materials = new[]
                            {
                                "WoodLog",
                                "Steel",
                                "Plasteel",
                                "BlocksSandstone",
                                "BlocksGranite",
                                "BlocksLimestone",
                                "BlocksSlate",
                                "BlocksMarble",
                                "Uranium",
                                "Bioferrite",
                            };
                            for (int i = 0; i < materials.Length; i++)
                            {
                                int dx = i == 9 ? 13 : 4 + i % 3 * 9;
                                int dz = 24 - i / 3 * 7;
                                var stuff = DefDatabase<ThingDef>.GetNamed(materials[i]);
                                AddTower(dx, dz, stuff, Rot4.South, false);
                                drawings.Last().MaterialLabel =
                                    stuff.LabelAsStuff.CapitalizeFirst();
                                foreach (int offset in new[] { -3, -2, 2, 3 })
                                    AddWall(dx + offset, dz, stuff);
                            }
                        }
                        else
                        {
                            AddTower(3, 3, ThingDefOf.WoodLog, Rot4.South, false);
                            AddTower(9, 3, ThingDefOf.WoodLog, Rot4.South, true);
                            var granite = DefDatabase<ThingDef>.GetNamed("BlocksGranite");
                            AddTower(3, 9, granite, Rot4.East, false);
                            AddTower(9, 9, granite, Rot4.East, true);
                            AddTower(4, 16, ThingDefOf.WoodLog, Rot4.South, true);
                            AddTower(13, 16, granite, Rot4.East, true);
                            AddTower(15, 3, granite, Rot4.North, false);
                            AddTower(15, 9, granite, Rot4.West, false);
                            // A straight perimeter wall meets both sides of the timber tower.
                            foreach (int dx in new[] { 0, 1, 2, 6, 7, 8 })
                                AddWall(dx, 16, ThingDefOf.WoodLog);
                            // A corner meets the stone tower's west and north faces.
                            foreach (int dx in new[] { 9, 10, 11 })
                                AddWall(dx, 16, granite);
                            foreach (int dz in new[] { 18, 19, 20 })
                                AddWall(13, dz, granite);
                        }
                    }
                    finally
                    {
                        Rand.PopState();
                    }
                    foreach (var tower in drawings)
                    {
                        bool study =
                            materialComparison
                            || !retainControls
                            || drawings.IndexOf(tower) is not (0 or 2);
                        tower.SilhouetteOutlines =
                            materialComparison || silhouetteOutlines && study;
                        // Keep the empty wood/granite examples as the accepted
                        // design so one overview compares it with the experiment.
                        if (nativeFrontPieces && study)
                            tower.NativeFrontSource = owned.First(w =>
                                w.def == ThingDefOf.Wall
                                && w.Stuff == tower.Stuff
                                && (w.Position + IntVec3.West).GetEdifice(map)?.def
                                    == ThingDefOf.Wall
                                && (w.Position + IntVec3.East).GetEdifice(map)?.def
                                    == ThingDefOf.Wall
                            );
                        tower.RebuildWallJoins();
                    }
                    foreach (var thing in owned)
                        thing.TryGetComp<CompFog>()?.HideFromPlayer?.UpdateVisibility(true);
                    manifest.environment.gameVersion = VersionControl.CurrentVersionString;
                    manifest.environment.mapId = map.GetUniqueLoadID();
                    manifest.environment.details = new
                    {
                        tick,
                        rect = new
                        {
                            area.minX,
                            area.minZ,
                            area.Width,
                            area.Height,
                        },
                        prototypeOnly = true,
                        nativeFrontPieces,
                        silhouetteOutlines,
                        retainControls,
                        materialComparison,
                        neutralMaterialLighting = materialComparison,
                        outlineStudy = silhouetteOutlines || materialComparison
                            ? "broad shaded faces and soft beam bevels from Andreas's three building references"
                            : null,
                        nativePawns = projectedOccupants
                            .Keys.Select(p => new
                            {
                                p.ThingID,
                                cell = p.Position.ToString(),
                                drawPosition = p.DrawPos.ToString(),
                                facing = p.Rotation.ToString(),
                            })
                            .ToArray(),
                        towers = drawings
                            .Select(t => new
                            {
                                t.ThingID,
                                stuff = t.Stuff.defName,
                                appearance = t.Stuff.stuffProps.appearance?.defName,
                                color = new
                                {
                                    t.DrawColor.r,
                                    t.DrawColor.g,
                                    t.DrawColor.b,
                                    t.DrawColor.a,
                                },
                                cell = t.Position.ToString(),
                                rotation = t.Rotation.ToString(),
                                wallJoins = t.WallJoinCount,
                                nativeFront = t.HasNativeFront,
                                silhouetteOutlines = t.SilhouetteOutlines,
                                nativeFrontMappingError = t.NativeFrontMappingError,
                                cachedDrawCalls = t.CachedDrawCalls,
                            })
                            .ToArray(),
                        nativeWallJoins = drawings.Sum(t => t.WallJoinCount),
                        nativeWalls = owned
                            .Where(t => t.def == ThingDefOf.Wall)
                            .Select(w => new
                            {
                                stuff = w.Stuff.defName,
                                color = new
                                {
                                    w.DrawColor.r,
                                    w.DrawColor.g,
                                    w.DrawColor.b,
                                    w.DrawColor.a,
                                },
                                graphic = w.Graphic.GetType().Name,
                                texture = w.Graphic.MatSingleFor(w).mainTexture.name,
                            })
                            .ToArray(),
                        geometryBuiltBeforeCapture = true,
                    };
                },
                cancellationToken
            );

            if (materialComparison)
                await Capture("materials", 0, 0, areaWidth, areaHeight, 17);
            else
            {
                await Capture("overview", 0, 0, 19, 21, retainControls ? 14 : 12);
                await Capture("wood-empty-occupied", 1, 1, 11, 6);
                await Capture("granite-empty-occupied", 1, 7, 11, 6);
                await Capture("wood-straight-wall", 0, 13, 9, 7);
                await Capture("granite-corner", 9, 13, 8, 8);
                await Capture("granite-north-ladder", 13, 1, 5, 6);
                await Capture("granite-west-ladder", 13, 7, 5, 6);
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (!materialComparison)
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "occupants stay in tower center cells",
                                projectedOccupants.Keys.All(p =>
                                    drawings.Any(t => t.Position == p.Position && t.Map == p.Map)
                                )
                            )
                        );
                    if (silhouetteOutlines || materialComparison)
                        manifest.assertions.Add(
                            RimBridgeEvidence.AreEqual(
                                "requested outline-study count",
                                materialComparison ? 10
                                    : retainControls ? 6
                                    : 8,
                                drawings.Count(t => t.SilhouetteOutlines)
                            )
                        );
                    if (nativeFrontPieces)
                    {
                        manifest.assertions.Add(
                            RimBridgeEvidence.AreEqual(
                                "requested native-front study count",
                                materialComparison ? 10
                                    : retainControls ? 6
                                    : 8,
                                drawings.Count(t => t.HasNativeFront)
                            )
                        );
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "native front-piece UV mapping keeps source map scale",
                                drawings.All(t => t.NativeFrontMappingError < .0001f)
                            )
                        );
                    }
                    manifest.assertions.Add(
                        RimBridgeEvidence.AreEqual(
                            "requested cached native wall overlaps",
                            materialComparison ? 20 : 4,
                            drawings.Sum(t => t.WallJoinCount)
                        )
                    );
                    manifest.assertions.Add(
                        RimBridgeEvidence.AreEqual(
                            "no simulation ticks advanced",
                            tick,
                            Find.TickManager.TicksGame
                        )
                    );
                    if (!materialComparison)
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "occupants remain native spawned pawns",
                                projectedOccupants.Keys.All(p =>
                                    p.Spawned && p.Map == map && !p.Dead && !p.Downed
                                )
                            )
                        );
                    manifest.assertions.Add(
                        RimBridgeEvidence.AreEqual(
                            "requested native 3x3 towers",
                            expectedTowers,
                            drawings.Count(t => t.Spawned && t.def.size == new IntVec2(3, 3))
                        )
                    );
                    if (materialComparison)
                    {
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "ten distinct materials with native labels and matching wall colours",
                                drawings.Select(t => t.Stuff).Distinct().Count() == 10
                                    && drawings.All(t =>
                                        t.MaterialLabel != null
                                        && owned.Any(w =>
                                            w.def == ThingDefOf.Wall
                                            && w.Stuff == t.Stuff
                                            && w.DrawColor == t.DrawColor
                                        )
                                    )
                            )
                        );
                        manifest.assertions.Add(
                            RimBridgeEvidence.IsTrue(
                                "each material has matching west and east wall sections",
                                drawings.All(t =>
                                    new[] { IntVec3.West, IntVec3.East }.All(d =>
                                        (t.Position + d * 2).GetEdifice(map)?.Stuff == t.Stuff
                                    )
                                )
                            )
                        );
                    }
                },
                cancellationToken
            );

            void AddTower(int dx, int dz, ThingDef stuff, Rot4 rotation, bool occupied)
            {
                var cell = new IntVec3(area.minX + dx, 0, area.minZ + dz);
                var tower = (WatchtowerVisualPrototype)ThingMaker.MakeThing(prototypeDef, stuff);
                owned.Add(tower);
                tower.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(tower, cell, map, rotation);
                drawings.Add(tower);
                if (!occupied)
                    return;
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                owned.Add(pawn);
                GenSpawn.Spawn(pawn, cell, map);
                pawn.Rotation = rotation.Opposite;
                projectedOccupants.Add(
                    pawn,
                    new Vector3(0, 0, WatchtowerVisualPrototype.OccupantDrawProjection)
                );
            }

            void AddWall(int dx, int dz, ThingDef stuff)
            {
                var wall = ThingMaker.MakeThing(ThingDefOf.Wall, stuff);
                owned.Add(wall);
                wall.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(wall, new IntVec3(area.minX + dx, 0, area.minZ + dz), map);
            }

            async Task Capture(
                string label,
                int dx,
                int dz,
                int width,
                int height,
                float rootSize = 11
            )
            {
                var frame = await ctx.Tools.CallAsync(
                    "rimworld/frame_cell_rect",
                    new
                    {
                        x = area.minX + dx,
                        z = area.minZ + dz,
                        width,
                        height,
                        paddingCells = 1,
                        rootSize,
                    },
                    cancellationToken: cancellationToken
                );
                if (!frame.Succeeded())
                    throw new InvalidOperationException("Tower framing failed: " + label);
                await ctx.Game.FramesAsync(12, cancellationToken);
                var shot = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        x = area.minX + dx,
                        z = area.minZ + dz,
                        width,
                        height,
                        paddingCells = 0,
                        rootSize,
                        fileName = evidenceLabel + "-" + label,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                manifest.assertions.Add(RimBridgeEvidence.ToolSucceeded("capture " + label, shot));
                manifest.captures.Add(
                    new RimBridgeEvidenceCapture
                    {
                        label = label,
                        kind = "cell_rect",
                        path = shot.TryReadResult<string>(out var path, "path")
                            ? path
                            : string.Empty,
                        details = shot.Result,
                        success = shot.Succeeded(),
                    }
                );
                if (!shot.Succeeded())
                    throw new InvalidOperationException("Tower screenshot failed: " + label);
            }
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        harmony.UnpatchAll(harmony.Id);
                        projectedOccupants.Clear();
                        foreach (var thing in owned)
                            if (!thing.Destroyed)
                                thing.Destroy(DestroyMode.Vanish);
                        if (staged)
                            foreach (var cell in area)
                                fog.DecrementSeen(
                                    Faction.OfPlayer,
                                    map.cellIndices.CellToIndex(cell)
                                );
                        SectionLayerFog.PrefEnableFade = oldFade;
                        DebugViewSettings.drawLightingOverlay = oldLightingOverlay;
                        Prefs.DotHighlightDisplayMode = oldHighlight;
                    },
                    CancellationToken.None
                );
                if (staged)
                {
                    var reload = await ctx.Tools.CallAsync(
                        "rimworld/load_game_ready",
                        new
                        {
                            saveName,
                            readiness = "visual",
                            pauseIfNeeded = true,
                        },
                        cancellationToken: CancellationToken.None
                    );
                    manifest.assertions.Add(
                        RimBridgeEvidence.ToolSucceeded("reload unchanged fixture", reload)
                    );
                }
            }
            finally
            {
                gate.Release();
            }
        }
        RimBridgeEvidence.Complete(manifest);
        return manifest;
    }

    private static void ProjectOccupant(Pawn __instance, ref Vector3 __result)
    {
        if (projectedOccupants.TryGetValue(__instance, out var offset))
            __result += offset;
    }

    private static void EnsureDefinition()
    {
        if (prototypeDef != null)
            return;
        prototypeDef = CreateDefinition(
            "TotalFog_WatchtowerVisualPrototype",
            typeof(WatchtowerVisualPrototype),
            Traversability.Standable
        );
    }

    internal static ThingDef CreateDefinition(string name, Type type, Traversability passability)
    {
        var definition = new ThingDef
        {
            defName = name,
            label = "watchtower prototype",
            description = "Test-only tower. Not a player building or elevated sight source.",
            thingClass = type,
            category = ThingCategory.Building,
            drawerType = DrawerType.RealtimeOnly,
            drawGUIOverlay = true,
            tickerType = TickerType.Never,
            size = new IntVec2(3, 3),
            rotatable = true,
            selectable = true,
            useHitPoints = true,
            passability = passability,
            building = new BuildingProperties { isEdifice = true },
            stuffCategories = new List<StuffCategoryDef>
            {
                StuffCategoryDefOf.Woody,
                StuffCategoryDefOf.Stony,
                StuffCategoryDefOf.Metallic,
            },
            costStuffCount = 150,
            statBases = new List<StatModifier>
            {
                new() { stat = StatDefOf.MaxHitPoints, value = 450 },
            },
            graphicData = new GraphicData
            {
                graphicClass = typeof(Graphic_Single),
                texPath = "Things/Buildings/WatchTower",
                drawSize = new Vector2(3, 4),
                linkFlags = LinkFlags.Wall,
                linkType = LinkDrawerType.Basic,
            },
            modContentPack = LoadedModManager.GetMod<TotalFogMod>().Content,
        };
        PresentationScenarios.AssignHash(definition);
        DefGenerator.AddImpliedDef(definition);
        definition.ResolveReferences();
        return definition;
    }
}
