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

public sealed class StreamFeedbackScenarios
{
    [Tool(
        "totalfog/stream_feedback",
        Description = "Stage native wall/door/floor blueprints, a corpse, a player door and torchlight. Check observed/hidden/revealed render state, hidden door area orders and selection rejection, plus unchanged native glow versus the lighting adapter. Capture actual screenshots and reload the unchanged fixture. Does not prove natural corpse resurrection or pixel acceptance."
    )]
    public static async Task<object> Run(
        IRimBridgeContext ctx,
        CancellationToken cancellationToken,
        string saveName,
        string evidenceLabel
    )
    {
        if (
            string.IsNullOrEmpty(evidenceLabel)
            || evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-')
        )
            throw new ArgumentException("Use a plain evidence label.");
        Map map = null;
        MapVisibility fog = null;
        CellRect area = default;
        var owned = new List<Thing>();
        var plans = new List<Thing>();
        Building_Door door = null;
        Corpse corpse = null;
        Thing lamp = null;
        Pawn zombie = null;
        var oldHighlight = Prefs.DotHighlightDisplayMode;
        var oldHighlightStyle = Prefs.HighlightStyleMode;
        bool sight = false,
            oldBypass = FogSettings.OnlyOutsideColony,
            oldFade = SectionLayerFog.PrefEnableFade;
        var rows = new List<object>();
        var pictures = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture first.");
                    fog = map.GetComponent<MapVisibility>();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized map.");
                    var origin = new IntVec3(212, 0, 79);
                    var cell = GenRadial
                        .RadialCellsAround(origin, 40, true)
                        .First(c =>
                            new CellRect(c.x, c.z, 8, 6).All(p =>
                                p.InBounds(map)
                                && p.Standable(map)
                                && !map.fogGrid.IsFogged(p)
                                && !fog.IsShown(Faction.OfPlayer, p)
                                && p.GetThingList(map).All(t => t is Plant or Filth)
                            )
                        );
                    area = new CellRect(cell.x, cell.z, 8, 6);
                    FogSettings.OnlyOutsideColony = false;
                    SectionLayerFog.PrefEnableFade = false;
                    foreach (var p in area)
                        fog.IncrementSeen(Faction.OfPlayer, map.cellIndices.CellToIndex(p));
                    sight = true;
                    Thing Spawn(ThingDef def, int dx, int dz, bool player = false)
                    {
                        var thing = ThingMaker.MakeThing(
                            def,
                            def.MadeFromStuff ? ThingDefOf.WoodLog : null
                        );
                        if (player)
                            thing.SetFaction(Faction.OfPlayer);
                        owned.Add(GenSpawn.Spawn(thing, cell + new IntVec3(dx, 0, dz), map));
                        return thing;
                    }
                    plans.Add(Spawn(ThingDefOf.Wall.blueprintDef, 0, 2, true));
                    plans.Add(Spawn(ThingDefOf.Door.blueprintDef, 2, 2, true));
                    plans.Add(
                        Spawn(DefDatabase<TerrainDef>.GetNamed("Concrete").blueprintDef, 4, 2, true)
                    );
                    door = (Building_Door)Spawn(ThingDefOf.Door, 0, 0, true);
                    lamp = Spawn(DefDatabase<ThingDef>.GetNamed("TorchLamp"), 6, 2, true);
                    Spawn(DefDatabase<ThingDef>.GetNamed("WatchTower"), 0, 4, true);
                    Spawn(DefDatabase<ThingDef>.GetNamed("SurveillanceCamera_Ground"), 2, 5, true);
                    Spawn(DefDatabase<ThingDef>.GetNamed("CameraConsole"), 5, 4, true);
                    var fire = (Fire)Spawn(DefDatabase<ThingDef>.GetNamed("Fire"), 6, 0);
                    fire.fireSize = 1;
                    var actions = AccessTools.TypeByName("ZombieLand.ZombieRuntimeActions");
                    if (actions != null)
                    {
                        var type = AccessTools.TypeByName("ZombieLand.ZombieType");
                        zombie = (Pawn)
                            AccessTools
                                .Method(actions, "SpawnZombie")
                                .Invoke(
                                    null,
                                    new object[]
                                    {
                                        cell + new IntVec3(4, 0, 0),
                                        map,
                                        Enum.Parse(type, "Normal"),
                                        true,
                                    }
                                );
                        if (zombie == null)
                            throw new InvalidOperationException("Native zombie spawn failed.");
                        owned.Add(zombie);
                    }
                    Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.HighlightAll;
                    Prefs.HighlightStyleMode = HighlightStyleMode.Silhouettes;
                    var victim = PawnGenerator.GeneratePawn(
                        PawnKindDefOf.Colonist,
                        Faction.OfAncients
                    );
                    GenSpawn.Spawn(victim, cell + new IntVec3(2, 0, 0), map);
                    victim.Kill(null);
                    corpse =
                        victim.Corpse
                        ?? throw new InvalidOperationException("Native death created no corpse.");
                    owned.Add(corpse);
                    foreach (var thing in owned)
                        thing.TryGetComp<CompFog>()?.HideFromPlayer?.UpdateVisibility(true);
                    Find.Selector.ClearSelection();
                },
                cancellationToken
            );
            foreach (bool visible in new[] { true, false, true })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        if (visible != sight)
                            foreach (var p in area)
                                if (visible)
                                    fog.IncrementSeen(
                                        Faction.OfPlayer,
                                        map.cellIndices.CellToIndex(p)
                                    );
                                else
                                    fog.DecrementSeen(
                                        Faction.OfPlayer,
                                        map.cellIndices.CellToIndex(p)
                                    );
                        sight = visible;
                        AccessTools
                            .Method(typeof(GlowGrid), "GlowGridUpdate_First")
                            .Invoke(map.glowGrid, null);
                    },
                    cancellationToken
                );
                await ctx.Game.FramesAsync(15, cancellationToken);
                rows.Add(
                    await ctx.MainThread.InvokeAsync<object>(
                        () =>
                        {
                            var index = map.cellIndices.CellToIndex(lamp.Position);
                            var native = map.glowGrid.VisualGlowAt(index);
                            var sample = AccessTools.Method(
                                AccessTools.TypeByName("TotalFog.Presentation.LightingVisibility"),
                                "VisualGlowAt"
                            );
                            var visual = (Color32)
                                sample.Invoke(
                                    null,
                                    new object[]
                                    {
                                        map.glowGrid,
                                        index,
                                        fog.GetFactionShownCells(Faction.OfPlayer),
                                    }
                                );
                            bool doorOrders = true,
                                rejectedSelection = true;
                            if (!visible)
                            {
                                var forbid = new Designator_Forbid();
                                var allow = new Designator_Unforbid();
                                door.SetForbidden(false, false);
                                doorOrders = forbid.CanDesignateThing(door).Accepted;
                                forbid.DesignateThing(door);
                                doorOrders &=
                                    door.IsForbidden(Faction.OfPlayer)
                                    && allow.CanDesignateThing(door).Accepted;
                                allow.DesignateThing(door);
                                doorOrders &= !door.IsForbidden(Faction.OfPlayer);
                                Find.Selector.Select(door, false, false);
                                rejectedSelection = !Find.Selector.IsSelected(door);
                                Find.Selector.ClearSelection();
                            }
                            var plansShown = plans.All(p =>
                                !p.TryGetComp<CompFog>().Hiddenable.Hidden
                            );
                            var corpseShown = !corpse.TryGetComp<CompFog>().Hiddenable.Hidden;
                            return new
                            {
                                visible,
                                plansShown,
                                corpseShown,
                                corpse.Spawned,
                                corpse.Destroyed,
                                doorOrders,
                                rejectedSelection,
                                nativeGlow = native.ToString(),
                                visualGlow = visual.ToString(),
                                gameplayGlowUnchanged = native.Equals(
                                    map.glowGrid.VisualGlowAt(index)
                                ),
                                passed = plansShown
                                    && corpseShown == visible
                                    && doorOrders
                                    && rejectedSelection
                                    && (native.r > 0 || native.g > 0 || native.b > 0)
                                    && (
                                        visible
                                            ? visual.Equals(native)
                                            : visual.Equals(default(Color32))
                                    ),
                            };
                        },
                        cancellationToken
                    )
                );
                var picture = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        fileName = evidenceLabel
                            + "-"
                            + (
                                rows.Count == 1 ? "observed"
                                : visible ? "revealed"
                                : "hidden"
                            ),
                        x = area.minX,
                        z = area.minZ,
                        width = area.Width,
                        height = area.Height,
                        paddingCells = 2,
                        rootSize = 12,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                if (!picture.Succeeded())
                    throw new InvalidOperationException("Feedback screenshot failed.");
                pictures.Add(picture);
                if (zombie != null)
                {
                    var widePicture = await ctx.Tools.CallAsync(
                        "rimworld/screenshot_cell_rect",
                        new
                        {
                            fileName = evidenceLabel + "-wide-" + rows.Count,
                            x = area.minX,
                            z = area.minZ,
                            width = area.Width,
                            height = area.Height,
                            paddingCells = 2,
                            rootSize = 60,
                            includeTargets = false,
                            doNotResetCamera = true,
                        },
                        cancellationToken: cancellationToken
                    );
                    if (!widePicture.Succeeded())
                        throw new InvalidOperationException("Wide highlight screenshot failed.");
                    pictures.Add(widePicture);
                    var highlight = await ctx.MainThread.InvokeAsync<object>(
                        () =>
                            new
                            {
                                visible,
                                highlightEnabled = zombie.def.drawHighlight,
                                shouldDraw = SilhouetteUtility.ShouldDrawSilhouette(zombie),
                                passed = zombie.def.drawHighlight
                                    && SilhouetteUtility.ShouldDrawSilhouette(zombie) == visible,
                            },
                        cancellationToken
                    );
                    rows.Add(highlight);
                }
            }
            return new
            {
                rows,
                pictures,
                area = new
                {
                    area.minX,
                    area.minZ,
                    area.Width,
                    area.Height,
                },
            };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (sight && fog != null)
                        foreach (var p in area)
                            fog.DecrementSeen(Faction.OfPlayer, map.cellIndices.CellToIndex(p));
                    foreach (var thing in owned)
                        if (!thing.Destroyed)
                            thing.Destroy();
                    FogSettings.OnlyOutsideColony = oldBypass;
                    SectionLayerFog.PrefEnableFade = oldFade;
                    Prefs.DotHighlightDisplayMode = oldHighlight;
                    Prefs.HighlightStyleMode = oldHighlightStyle;
                },
                CancellationToken.None
            );
            var reset = await ctx.Tools.CallAsync(
                "rimworld/load_game_ready",
                new
                {
                    saveName,
                    readiness = "visual",
                    pauseIfNeeded = true,
                    ignoreModCompatibility = true,
                },
                cancellationToken: CancellationToken.None
            );
            if (!reset.Succeeded())
                throw new InvalidOperationException("Feedback fixture cleanup failed.");
        }
    }
}
