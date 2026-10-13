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
using Verse.AI;

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
        var lightingMeshes = new List<Color32[]>();
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
                            var lightingLayer = (
                                (IEnumerable<SectionLayer>)
                                    AccessTools
                                        .Field(typeof(Section), "layers")
                                        .GetValue(map.mapDrawer.SectionAt(lamp.Position))
                            )
                                .OfType<SectionLayer_LightingOverlay>()
                                .Single();
                            // Inspect uploaded native mesh colors as well as
                            // the adapter. This catches a missed dirty flag.
                            lightingMeshes.Add(
                                lightingLayer.GetSubMesh(MatBases.LightOverlay).mesh.colors32
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
                                lightingMeshVertices = lightingMeshes.Last().Length,
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
                lightingMeshChangesWithSight = lightingMeshes.Count == 3
                    && lightingMeshes[0].Length > 0
                    && !lightingMeshes[0].SequenceEqual(lightingMeshes[1]),
                lightingMeshRestored = lightingMeshes.Count == 3
                    && lightingMeshes[0].SequenceEqual(lightingMeshes[2]),
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

    [Tool(
        "totalfog/stream_bug_presentation",
        Description = "Check Spitter dots/silhouettes with visible/hidden/off controls and scheduled hearing rings from a hidden walking Spitter behind a real wall. Captures native pixels and reloads the unchanged fixture."
    )]
    public static async Task<object> BugPresentation(
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
        var oldStyle = Prefs.HighlightStyleMode;
        var oldMode = Prefs.DotHighlightDisplayMode;
        bool oldCues = FogSettings.ShowHearingCues,
            oldBypass = FogSettings.OnlyOutsideColony;
        float oldRange = FogSettings.BaseHearingRange;
        var rows = new List<object>();
        var pictures = new List<object>();
        Map map = null;
        MapVisibility fog = null;
        CellRect area = default,
            sightArea = default;
        bool sightAdded = false;
        Pawn listener = null,
            spitter = null;
        try
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    map =
                        Find.CurrentMap
                        ?? throw new InvalidOperationException("Load the named fixture.");
                    fog = map.GetVisibility();
                    if (!Find.TickManager.Paused || !fog.Initialized)
                        throw new InvalidOperationException("Use a paused initialized fixture.");
                    var cell = map.AllCells.First(c =>
                        c.x > 10
                        && c.z > 10
                        && c.x < map.Size.x - 12
                        && c.z < map.Size.z - 12
                        && new CellRect(c.x, c.z, 10, 9).All(p =>
                            p.InBounds(map)
                            && p.Standable(map)
                            && !fog.IsShown(Faction.OfPlayer, p)
                            && p.GetThingList(map).All(t => t is Plant or Filth)
                        )
                    );
                    area = new CellRect(cell.x, cell.z, 10, 9);
                    foreach (var p in area)
                        map.fogGrid.Unfog(p);
                    for (int z = 0; z < 9; z++)
                        GenSpawn.Spawn(
                            ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog),
                            cell + new IntVec3(2, 0, z),
                            map
                        );
                    listener = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    GenSpawn.Spawn(listener, cell + new IntVec3(0, 0, 4), map);
                    var wait = JobMaker.MakeJob(JobDefOf.Wait);
                    wait.expiryInterval = 3000;
                    listener.jobs.StartJob(wait, Verse.AI.JobCondition.InterruptForced);
                    var spitterKind = DefDatabase<PawnKindDef>.GetNamed("ZombieSpitter");
                    var zombieFaction = Find.FactionManager.FirstFactionOfDef(
                        DefDatabase<FactionDef>.GetNamed("Zombies")
                    );
                    spitter = PawnGenerator.GeneratePawn(spitterKind, zombieFaction);
                    GenSpawn.Spawn(spitter, cell + new IntVec3(4, 0, 4), map);
                    FogSettings.OnlyOutsideColony = false;
                    listener.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
                    sightArea = new CellRect(cell.x + 3, cell.z + 2, 5, 5);
                },
                cancellationToken
            );
            var zoom = await ctx.Tools.CallAsync(
                "rimworld/set_camera_zoom",
                new { rootSize = 60 },
                cancellationToken: cancellationToken
            );
            if (!zoom.Succeeded())
                throw new InvalidOperationException("Highlight zoom setup failed.");
            foreach (var style in new[] { HighlightStyleMode.Dots, HighlightStyleMode.Silhouettes })
            foreach (string state in new[] { "visible", "hidden", "off" })
            {
                await ctx.MainThread.InvokeAsync(
                    () =>
                    {
                        bool wantSight = state != "hidden";
                        if (sightAdded != wantSight)
                            foreach (var p in sightArea)
                                if (wantSight)
                                    fog.IncrementSeen(
                                        Faction.OfPlayer,
                                        map.cellIndices.CellToIndex(p)
                                    );
                                else
                                    fog.DecrementSeen(
                                        Faction.OfPlayer,
                                        map.cellIndices.CellToIndex(p)
                                    );
                        sightAdded = wantSight;
                        Prefs.HighlightStyleMode = style;
                        Prefs.DotHighlightDisplayMode =
                            state == "off"
                                ? DotHighlightDisplayMode.None
                                : DotHighlightDisplayMode.HighlightHostiles;
                        spitter.shouldHighlightCachedTick = -1;
                        rows.Add(
                            new
                            {
                                style = style.ToString(),
                                state,
                                enabled = spitter.def.drawHighlight,
                                visible = Visibility.IsVisible(spitter),
                                overlayAllowed = Detours.Pawn.DrawGUIOverlay_Prefix(spitter),
                                silhouette = SilhouetteUtility.ShouldDrawSilhouette(spitter),
                                dotEligible = (bool)
                                    AccessTools
                                        .Method(
                                            typeof(SilhouetteUtility),
                                            "ShouldDrawPawnDotSilhouette"
                                        )
                                        .Invoke(null, new object[] { spitter }),
                            }
                        );
                    },
                    cancellationToken
                );
                var picture = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        fileName = evidenceLabel
                            + "-spitter-"
                            + style.ToString().ToLowerInvariant()
                            + "-"
                            + state,
                        x = area.minX + 3,
                        z = area.minZ + 2,
                        width = 5,
                        height = 5,
                        paddingCells = 1,
                        rootSize = 60,
                        includeTargets = false,
                        doNotResetCamera = true,
                    },
                    cancellationToken: cancellationToken
                );
                if (!picture.Succeeded())
                    throw new InvalidOperationException("Spitter screenshot failed.");
                pictures.Add(picture);
            }
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    if (sightAdded)
                        foreach (var p in sightArea)
                            fog.DecrementSeen(Faction.OfPlayer, map.cellIndices.CellToIndex(p));
                    sightAdded = false;
                    Prefs.DotHighlightDisplayMode = DotHighlightDisplayMode.None;
                    FogSettings.ShowHearingCues = true;
                    FogSettings.BaseHearingRange = 10;
                    spitter.jobs.StartJob(
                        JobMaker.MakeJob(JobDefOf.Goto, spitter.Position + new IntVec3(4, 0, 0)),
                        Verse.AI.JobCondition.InterruptForced
                    );
                },
                cancellationToken
            );
            foreach (bool enabled in new[] { true, false })
            {
                await ctx.MainThread.InvokeAsync(
                    () => FogSettings.ShowHearingCues = enabled,
                    cancellationToken
                );
                var ticks = await ctx.Game.StepTicksAsync(
                    110,
                    cancellationToken: cancellationToken
                );
                if (!ticks.Success)
                    throw new InvalidOperationException("Scheduled hearing ticks failed.");
                rows.Add(
                    await ctx.MainThread.InvokeAsync<object>(
                        () =>
                            new
                            {
                                hearingEnabled = enabled,
                                listener = listener.Position.ToString(),
                                source = spitter.Position.ToString(),
                                moving = spitter.pather.Moving,
                                hidden = !Visibility.IsVisible(spitter),
                                range = FogSettings.BaseHearingRange
                                    * listener.health.capacities.GetLevel(
                                        PawnCapacityDefOf.Hearing
                                    ),
                                cues = map
                                    .dynamicDrawManager.DrawThings.OfType<Mote_HearingCue>()
                                    .Where(c => !c.Destroyed)
                                    .Select(c => new
                                    {
                                        c.ObserverFactionId,
                                        visible = Visibility.IsVisible(c),
                                        c.Alpha,
                                        scale = c.ExactScale.ToString(),
                                        age = c.AgeSecs,
                                        c.spawnTick,
                                    })
                                    .ToArray(),
                            },
                        cancellationToken
                    )
                );
                var picture = await ctx.Tools.CallAsync(
                    "rimworld/screenshot_cell_rect",
                    new
                    {
                        fileName = evidenceLabel + (enabled ? "-hearing-on" : "-hearing-off"),
                        x = area.minX + 3,
                        z = area.minZ + 2,
                        width = 6,
                        height = 5,
                        paddingCells = 1,
                        rootSize = 12,
                        includeTargets = false,
                    },
                    cancellationToken: cancellationToken
                );
                if (!picture.Succeeded())
                    throw new InvalidOperationException("Hearing screenshot failed.");
                pictures.Add(picture);
            }
            return new { rows, pictures };
        }
        finally
        {
            await ctx.MainThread.InvokeAsync(
                () =>
                {
                    Prefs.HighlightStyleMode = oldStyle;
                    Prefs.DotHighlightDisplayMode = oldMode;
                    FogSettings.ShowHearingCues = oldCues;
                    FogSettings.BaseHearingRange = oldRange;
                    FogSettings.OnlyOutsideColony = oldBypass;
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
                },
                cancellationToken: CancellationToken.None
            );
            if (!reset.Succeeded())
                throw new InvalidOperationException("Feedback fixture cleanup failed.");
        }
    }
}
