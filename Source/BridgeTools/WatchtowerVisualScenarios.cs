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
        Description = "Render eight test-only 3x3 tower sketches using native stuff, buildings, walls and colonists. Capture one combined overview plus tight wood/granite empty/occupied, straight-wall, corner and four ladder rotation views. No climb, sight, combat or placement claim. Requires a paused named fixture, advances no game ticks and reloads that unchanged save. Test definitions and drawing code are excluded from player ZIPs."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (
            string.IsNullOrEmpty(saveName)
            || string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Supply a save name and a plain evidence label.");
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
        var oldHighlight = Prefs.DotHighlightDisplayMode;
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
                    var origin = GenRadial
                        .RadialCellsAround(
                            new IntVec3(map.Size.x - 40, 0, map.Size.z / 3),
                            65,
                            true
                        )
                        .First(c =>
                            new CellRect(c.x, c.z, 19, 21).All(p =>
                                p.InBounds(map)
                                && p.Standable(map)
                                && !map.fogGrid.IsFogged(p)
                                && !p.Roofed(map)
                                && p.GetThingList(map).All(t => t is Plant or Filth)
                            )
                        );
                    area = new CellRect(origin.x, origin.z, 19, 21);
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
                    finally
                    {
                        Rand.PopState();
                    }
                    foreach (var tower in drawings)
                        tower.RebuildWallJoins();
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
                                cell = t.Position.ToString(),
                                rotation = t.Rotation.ToString(),
                                wallJoins = t.WallJoinCount,
                                cachedDrawCalls = t.CachedDrawCalls,
                            })
                            .ToArray(),
                        nativeWallJoins = drawings.Sum(t => t.WallJoinCount),
                        geometryBuiltBeforeCapture = true,
                    };
                },
                cancellationToken
            );

            await Capture("overview", 0, 0, 19, 21, 14);
            await Capture("wood-empty-occupied", 1, 1, 11, 6);
            await Capture("granite-empty-occupied", 1, 7, 11, 6);
            await Capture("wood-straight-wall", 0, 13, 9, 7);
            await Capture("granite-corner", 9, 13, 8, 8);
            await Capture("granite-north-ladder", 13, 1, 5, 6);
            await Capture("granite-west-ladder", 13, 7, 5, 6);
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    manifest.assertions.Add(
                        RimBridgeEvidence.IsTrue(
                            "occupants stay in tower center cells",
                            projectedOccupants.Keys.All(p =>
                                drawings.Any(t => t.Position == p.Position && t.Map == p.Map)
                            )
                        )
                    );
                    manifest.assertions.Add(
                        RimBridgeEvidence.AreEqual(
                            "four cached native wall overlaps",
                            4,
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
                            "eight native 3x3 towers",
                            8,
                            drawings.Count(t => t.Spawned && t.def.size == new IntVec2(3, 3))
                        )
                    );
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
        prototypeDef = new ThingDef
        {
            defName = "TotalFog_WatchtowerVisualPrototype",
            label = "watchtower visual prototype",
            description = "Test-only tower artwork. No sight or gameplay behavior.",
            thingClass = typeof(WatchtowerVisualPrototype),
            category = ThingCategory.Building,
            drawerType = DrawerType.RealtimeOnly,
            tickerType = TickerType.Never,
            size = new IntVec2(3, 3),
            rotatable = true,
            selectable = true,
            useHitPoints = true,
            passability = Traversability.Standable,
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
        PresentationScenarios.AssignHash(prototypeDef);
        DefGenerator.AddImpliedDef(prototypeDef);
        prototypeDef.ResolveReferences();
    }
}
