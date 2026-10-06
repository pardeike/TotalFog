using System;
using System.Collections;
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

public sealed class ContaminationScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);
    private static Mesh contaminationMesh;
    private static CellRect probeArea;
    private static int markerDraws;

    [Tool("totalfog/contamination_overlay", Description = "Stage contaminated ground and one steel stack in a remote paused cell. Capture native detailed and zoomed-out cached overlays on/off across hidden/visible/hidden-again/colony-bypass sight, with actual marker calls, cached giver answers, native selection and unchanged contamination data. No ticks or quest changes. Raw stat entries are diagnostic, not proof of reachable UI. Restores scoped options/probes and reloads the unchanged named fixture.")]
    public static async Task<object> Overlay(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string saveName, string evidenceLabel, int x = 212, int z = 78, int frames = 12, bool nativeSightSource = false,
        bool refreshChecks = false)
    {
        if (frames < 12 || frames > 30 || string.IsNullOrEmpty(evidenceLabel) ||
            evidenceLabel.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
            throw new ArgumentException("Use 12..30 frames and a plain evidence label.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.contamination-overlay-probe");
        Map map = null; MapVisibility fog = null; Thing item = null; ThingWithComps observer = null; object manager = null;
        System.Reflection.FieldInfo overlay = null;
        bool oldOverlay = false, oldBypass = FogSettings.OnlyOutsideColony, oldFade = SectionLayerFog.PrefEnableFade;
        bool addedSight = false, passed = true;
        var rows = new List<object>(); var pictures = new List<object>();
        var cell = new IntVec3(x, 0, z); int tick = 0;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load the named fixture first.");
                fog = map.GetVisibility(); probeArea = new CellRect(x - 1, z - 1, 3, 3);
                if (!Find.TickManager.Paused || !fog.Initialized || !map.IsPlayerHome ||
                    probeArea.Any(c => !c.InBounds(map) || map.fogGrid.IsFogged(c) || fog.IsShown(Faction.OfPlayer, c)) ||
                    cell.GetEdifice(map) != null || cell.GetThingList(map).Any(t => t is Pawn || t.def.category == ThingCategory.Item))
                    throw new InvalidOperationException("Use an explored remote home-map cell without pawns, items or edifices.");
                var type = AccessTools.TypeByName("ZombieLand.ContaminationManager");
                manager = AccessTools.PropertyGetter(type, "Instance").Invoke(null, null);
                overlay = AccessTools.Field(type, "showContaminationOverlay"); oldOverlay = (bool)overlay.GetValue(manager);
                var graphics = AccessTools.TypeByName("ZombieLand.GraphicToolbox");
                contaminationMesh = (Mesh)AccessTools.Field(graphics, "contaminationMesh").GetValue(null);
                harmony.Patch(AccessTools.Method(graphics, "DrawScaledMesh"),
                    prefix: new HarmonyMethod(typeof(ContaminationScenarios), nameof(ObserveMarker)));
                FogSettings.OnlyOutsideColony = false; SectionLayerFog.PrefEnableFade = false;
                Find.Selector.ClearSelection();
                foreach (var c in probeArea) fog.RevealCell(map.cellIndices.CellToIndex(c));
                item = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), cell, map);
                tick = Find.TickManager.TicksGame;
            }, cancellationToken);
            var written = await ctx.Tools.CallAsync("zombieland/write_contamination_state",
                new { value = .65f, cells = x + "," + z, things = item.ThingID }, cancellationToken: cancellationToken);
            if (!written.Succeeded()) throw new InvalidOperationException("Contamination setup failed.");
            foreach (string mode in new[] { "detailed", "cached" })
            {
                float rootSize = mode == "detailed" ? 12 : 60;
                var camera = await ctx.Tools.CallAsync("rimworld/frame_cell_rect",
                    new { x, z, width = 1, height = 1, paddingCells = 1, rootSize }, cancellationToken: cancellationToken);
                if (!camera.Succeeded()) throw new InvalidOperationException("Overlay camera setup failed.");
                var states = new[] { "hidden", "visible", "hidden-again", "colony-bypass" }.Concat(
                    refreshChecks && mode == "cached" ? new[] { "hidden-mutated", "revealed-mutated",
                        "offscreen-mutated-return", "zoom-mutated-return", "closed-mutated-reopen", "revealed-cleared" } : Array.Empty<string>());
                float currentValue = .65f;
                foreach (string state in states)
                {
                    bool expected = state is not ("hidden" or "hidden-again" or "hidden-mutated");
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        FogSettings.OnlyOutsideColony = state == "colony-bypass"; SetSight(expected && state != "colony-bypass");
                        overlay.SetValue(manager, state != "closed-mutated-reopen"); markerDraws = 0;
                        Find.Selector.ClearSelection();
                        Find.Selector.Select(item, false, false);
                    }, cancellationToken);
                    if (state is "offscreen-mutated-return" or "zoom-mutated-return")
                    {
                        var away = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new { x = state == "offscreen-mutated-return" ? 40 : x,
                            z = state == "offscreen-mutated-return" ? 40 : z, width = 1, height = 1, rootSize = 12f }, cancellationToken: cancellationToken);
                        if (!away.Succeeded()) throw new InvalidOperationException("Refresh camera transition failed.");
                    }
                    float nextValue = state switch { "hidden-mutated" => .12f, "offscreen-mutated-return" => .05f,
                        "zoom-mutated-return" => .1f, "closed-mutated-reopen" => .2f, "revealed-cleared" => 0f, _ => currentValue };
                    if (nextValue != currentValue)
                    {
                        var changed = await ctx.Tools.CallAsync("zombieland/write_contamination_state",
                            new { value = nextValue, cells = x + "," + z, things = item.ThingID }, cancellationToken: cancellationToken);
                        if (!changed.Succeeded()) throw new InvalidOperationException("Native contamination mutation failed.");
                        currentValue = nextValue;
                        await ctx.Game.FramesAsync(65, cancellationToken);
                    }
                    if (state is "offscreen-mutated-return" or "zoom-mutated-return")
                    {
                        var returned = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new { x, z, width = 1, height = 1,
                            paddingCells = 1, rootSize }, cancellationToken: cancellationToken);
                        if (!returned.Succeeded()) throw new InvalidOperationException("Refresh camera return failed.");
                    }
                    await ctx.MainThread.InvokeAsync(() => overlay.SetValue(manager, true), cancellationToken);
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                    rows.Add(await ctx.MainThread.InvokeAsync(() =>
                    {
                        int index = map.cellIndices.CellToIndex(cell);
                        bool expectedMarker = expected && currentValue > 0;
                        bool cachedRoute = Find.CameraDriver.CurrentViewRect.Area >=
                            (int)AccessTools.Field(AccessTools.TypeByName("ZombieLand.Constants"), "MAX_CELLS_FOR_DETAILED_CONTAMINATION").GetValue(null);
                        var grounds = (IDictionary)AccessTools.Field(manager.GetType(), "grounds").GetValue(manager);
                        var ground = (ICellBoolGiver)grounds[map.Index];
                        bool groundAnswer = ground.GetCellBool(index);
                        bool thingAnswer = mode == "cached" && ((ICellBoolGiver)manager).GetCellBool(index);
                        var contamination = (IDictionary)AccessTools.Field(manager.GetType(), "contaminations").GetValue(manager);
                        float value = contamination.Contains(item.thingIDNumber) ? (float)contamination[item.thingIDNumber] : 0f;
                        var values = (float[])AccessTools.Field(ground.GetType(), "cells").GetValue(ground);
                        bool selected = Find.Selector.IsSelected(item);
                        bool valid = (cachedRoute == (mode == "cached")) && selected == expected && Math.Abs(value - currentValue) < 0.000001f &&
                            Math.Abs(values[index] - currentValue) < 0.000001f && tick == Find.TickManager.TicksGame &&
                            (mode == "detailed" ? (markerDraws > 0) == expectedMarker : groundAnswer == expectedMarker && thingAnswer == expectedMarker);
                        passed &= valid;
                        return new { mode, state, passed = valid, expected, expectedMarker, expectedValue = currentValue, cachedRoute,
                            viewCells = Find.CameraDriver.CurrentViewRect.Area, markerDraws, groundAnswer, thingAnswer,
                            selected, value, groundValue = values[index], tick,
                            rawStatValues = item.SpecialDisplayStats().Where(e => e.LabelCap.Equals("ZombieContamination".Translate().ToString(), StringComparison.OrdinalIgnoreCase))
                                .Select(e => e.ValueString).ToArray() };
                    }, cancellationToken));
                    pictures.Add(await Picture(mode + "-" + state + "-on", rootSize));
                    await ctx.MainThread.InvokeAsync(() => overlay.SetValue(manager, false), cancellationToken);
                    await ctx.Game.FramesAsync(frames, cancellationToken);
                    pictures.Add(await Picture(mode + "-" + state + "-off", rootSize));
                }
            }
            return new { passed, stagedSight = !nativeSightSource, nativeSightSource, refreshChecks,
                advancedTicks = 0, pixelAcceptance = false, rows, pictures };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id);
                    if (item?.Destroyed == false) item.Destroy();
                    if (addedSight) SetSight(false);
                    if (manager != null) overlay.SetValue(manager, oldOverlay);
                    FogSettings.OnlyOutsideColony = oldBypass; SectionLayerFog.PrefEnableFade = oldFade;
                    contaminationMesh = null;
                }, CancellationToken.None);
                var reload = await ctx.Tools.CallAsync("rimworld/load_game_ready",
                    new { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!reload.Succeeded()) throw new InvalidOperationException("Contamination fixture restoration failed.");
            }
            finally { gate.Release(); }
        }
        void SetSight(bool visible)
        {
            if (visible == addedSight) return;
            if (nativeSightSource)
            {
                if (visible)
                {
                    observer = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("SecurityBellSmall"));
                    observer.SetFaction(Faction.OfPlayer);
                    GenSpawn.Spawn(observer, cell + IntVec3.West, map);
                }
                else { observer.Destroy(); observer = null; }
                addedSight = visible;
                return;
            }
            foreach (var c in probeArea)
            {
                int index = map.cellIndices.CellToIndex(c);
                if (visible) fog.IncrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
                else fog.DecrementSeen(Faction.OfPlayer, fog.GetFactionShownCells(Faction.OfPlayer), index);
            }
            addedSight = visible;
        }
        async Task<object> Picture(string name, float rootSize)
        {
            var result = await ctx.Tools.CallAsync("rimworld/screenshot_cell_rect", new { fileName = evidenceLabel + "-" + name,
                x, z, width = 1, height = 1, paddingCells = 1, rootSize, doNotResetCamera = true,
                includeTargets = false }, cancellationToken: cancellationToken);
            if (!result.Succeeded()) throw new InvalidOperationException("Contamination screenshot failed.");
            return result;
        }
    }
    private static void ObserveMarker(Mesh mesh, Vector3 pos)
    { if (mesh == contaminationMesh && probeArea.Contains(pos.ToIntVec3())) markerDraws++; }
}
