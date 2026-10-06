using System;
using System.Collections.Generic;
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

/// <summary>Native draw/overlay evidence for staged third-party pawn fixtures.</summary>
public sealed class ModdedPawnScenarios
{
    private sealed class Calls
    {
        internal readonly int[] Phases = new int[3];
        internal int DrawAt, Overlay;
    }
    private static readonly Dictionary<Pawn, Calls> calls = new();
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool("totalfog/symbiant_boundary", Description = "Create a hostless render-only blob, mature it through 61 native ticks, observe root-visible/body-partial, root-hidden/body-visible and all-hidden native rendering, and capture body-on/off screenshots. Restores sight settings/diagnostics and reloads the unchanged named save.")]
    public static async Task<object> SymbiantBoundary(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string saveName, string evidenceLabel, int x = 212, int z = 79, int frames = 12, bool extended = false)
    {
        if (frames < 3 || frames > 30) throw new ArgumentException("Use 3..30 frames per sample.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.symbiant-boundary-probe");
        int oldRange = FogSettings.BaseViewRange, startTick = -1, matureTick = -1;
        bool oldFade = SectionLayerFog.PrefEnableFade;
        string oldProfile = null;
        Type symbiantType = null;
        Pawn target = null, viewer = null;
        IntVec3 originalCore = IntVec3.Invalid;
        Map map = null;
        var rows = new List<object>();
        var pictures = new List<object>();
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load the named fixture first.");
                if (!Find.TickManager.Paused || !map.GetComponent<MapVisibility>().Initialized || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use paused initialized colony fog with bypass disabled.");
                if (!new IntVec3(x - 10, 0, z).InBounds(map) || !new IntVec3(x + 8, 0, z + 1).InBounds(map))
                    throw new ArgumentException("The render strip must fit inside the current map.");
                startTick = Find.TickManager.TicksGame;
                symbiantType = AccessTools.TypeByName("ZombieLand.ZombieSymbiant") ??
                    throw new InvalidOperationException("Zombieland Symbiant is unavailable.");
                oldProfile = (string)AccessTools.Property(symbiantType, "DebugPerfProfile").GetValue(null);
            }, cancellationToken);
            string cells = string.Join(";", Enumerable.Range(0, 9).SelectMany(cx => new[] { cx + ",0", cx + ",1" }));
            var blob = await ctx.Tools.CallAsync("zombieland/symbiant_render_blob",
                new { cells, x, z, select = false, jump = false, perfProfile = "renderOnly" }, cancellationToken: cancellationToken);
            if (!blob.Succeeded()) throw new InvalidOperationException("Render-blob creation failed.");
            await ctx.MainThread.InvokeAsync(() =>
            {
                target = map.mapPawns.AllPawnsSpawned.Single(p => p.GetType().FullName == "ZombieLand.ZombieSymbiant");
                viewer = map.mapPawns.FreeColonistsSpawned.First(p => p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null);
                FogSettings.BaseViewRange = 5;
                SectionLayerFog.PrefEnableFade = false;
                foreach (var observer in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer &&
                    p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null).ToArray())
                    MoveViewer(observer, new IntVec3(map.Size.x / 2, 0, map.Size.z / 2));
                foreach (var source in map.GetComponent<MapVisibility>().fowWatchers) source.UpdateFoV(true);
                lock (calls) { calls.Clear(); calls.Add(target, new Calls()); }
                foreach (var pair in new[] { (nameof(Pawn.DynamicDrawPhaseAt), nameof(ObservePhase)),
                    (nameof(Thing.DrawAt), nameof(ObserveDrawAt)), (nameof(Thing.DrawGUIOverlay), nameof(ObserveOverlay)) })
                {
                    var method = AccessTools.Method(target.GetType(), pair.Item1);
                    method = AccessTools.DeclaredMethod(method.DeclaringType, pair.Item1,
                        method.GetParameters().Select(p => p.ParameterType).ToArray());
                    harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModdedPawnScenarios), pair.Item2));
                }
                // Observe the whole strip first so the negative part is explored
                // translucent fog, rather than hidden by unexplored black terrain.
                MoveViewer(viewer, new IntVec3(x, 0, z));
                MoveViewer(viewer, new IntVec3(x + 4, 0, z));
                MoveViewer(viewer, new IntVec3(x + 8, 0, z));
            }, cancellationToken);
            await ctx.Game.StepTicksAsync(61, cancellationToken: cancellationToken);
            await ctx.MainThread.InvokeAsync(() =>
            {
                matureTick = Find.TickManager.TicksGame;
                if (matureTick - startTick < 61 || !target.Spawned)
                    throw new InvalidOperationException("The fixed render blob did not mature through 61 native ticks.");
                Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                Find.Selector.ClearSelection();
            }, cancellationToken);
            var camera = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new { x = x - 3, z = z - 3,
                width = 15, height = 8, rootSize = 12f }, cancellationToken: cancellationToken);
            if (!camera.Succeeded()) throw new InvalidOperationException("Render framing failed.");
            var samples = new List<(int Offset, string Name, bool Away, bool VanillaRoot, bool Fallback)>
                { (0, "0", false, false, false), (8, "8", false, false, false), (-10, "-10", false, false, false) };
            if (extended) samples.AddRange(new[] { (8, "vanilla-root", false, true, false),
                (8, "off-camera", true, false, false), (0, "fallback-root", false, false, true),
                (8, "fallback-end", false, false, true), (-3, "core-boundary", false, false, false),
                (8, "resource-rebuild", false, false, false),
                (0, "after-fallback", false, false, false),
                (8, "selection-root-hidden", false, false, false),
                (0, "selection-core-hidden", false, false, false) });
            foreach (var sample in samples)
            {
                var frame = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new { x = sample.Away ? 120 : x - 3,
                    z = sample.Away ? 120 : z - 3, width = 15, height = 8, rootSize = 12f }, cancellationToken: cancellationToken);
                if (!frame.Succeeded()) throw new InvalidOperationException("Sample framing failed.");
                await ctx.MainThread.InvokeAsync(() =>
                {
                    MoveViewer(viewer, new IntVec3(x + sample.Offset, 0, z));
                    ((Unity.Collections.NativeBitArray)AccessTools.Property(typeof(FogGrid), "FogGrid_Unsafe").GetValue(map.fogGrid))
                        .Set(map.cellIndices.CellToIndex(target.Position), sample.VanillaRoot);
                    AccessTools.Field(symbiantType, "debugForceMetaballFallback").SetValue(target, sample.Fallback);
                    if (sample.Name.StartsWith("selection-", StringComparison.Ordinal))
                    {
                        originalCore = (IntVec3)AccessTools.Field(symbiantType, "selectionCoreRelative").GetValue(target);
                        AccessTools.Method(symbiantType, "ClearSelectionCoreMotion").Invoke(target, null);
                        AccessTools.Field(symbiantType, "selectionCoreRelative").SetValue(target, new IntVec3(8, 0, 0));
                    }
                    if (sample.Name == "core-boundary")
                    {
                        originalCore = (IntVec3)AccessTools.Field(symbiantType, "selectionCoreRelative").GetValue(target);
                        // Pawn sight modifiers can change the strip boundary.
                        // Stage across the measured boundary, not a fixed cell pair.
                        var body = ((IEnumerable<IntVec3>)AccessTools.Property(symbiantType, "AbsoluteCells").GetValue(target)).ToArray();
                        var boundary = body.Where(cell => Visibility.IsVisible(map, cell))
                            .SelectMany(from => body.Where(to => from.AdjacentToCardinal(to) && !Visibility.IsVisible(map, to))
                                .Select(to => (From: from, To: to))).FirstOrDefault();
                        if (boundary.From == boundary.To) throw new InvalidOperationException("No visible/hidden body boundary is available.");
                        bool begun = (bool)AccessTools.Method(symbiantType, "DebugBeginSelectionCoreHandoff").Invoke(target,
                            new object[] { boundary.From, boundary.To });
                        bool staged = begun && (bool)AccessTools.Method(symbiantType, "DebugSetSelectionCoreHandoffProgress")
                            .Invoke(target, new object[] { 0.5f });
                        if (!staged) throw new InvalidOperationException("The core must straddle a visible/hidden cell boundary.");
                    }
                    if (sample.Name == "resource-rebuild")
                        AccessTools.Method(symbiantType, "ReleaseRenderResources").Invoke(target, new object[] { false });
                    map.GetComponent<MapVisibility>().MapComponentTick();
                    map.mapDrawer.RegenerateEverythingNow();
                    Reset();
                }, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                rows.Add(await ctx.MainThread.InvokeAsync<object>(() =>
                {
                    var fog = map.GetComponent<MapVisibility>();
                    var absolute = ((IEnumerable<IntVec3>)AccessTools.Property(target.GetType(), "AbsoluteCells").GetValue(target)).ToArray();
                    var visibleCells = absolute.Where(cell => !map.fogGrid.IsFogged(cell) && fog.IsShown(Faction.OfPlayer, cell)).ToArray();
                    int visible = visibleCells.Length;
                    var state = Snapshot(target);
                    bool expected = visible > 0 && !sample.Away;
                    return new { offset = sample.Offset, sample = sample.Name, sample.Away, sample.Fallback,
                        nativeRootFogged = map.fogGrid.IsFogged(target.Position),
                        nativeCameraContainsBody = Find.CameraDriver.CurrentViewRect.Overlaps(target.OccupiedDrawRect()),
                        rootVisible = state.InSight, bodyVisibleCells = visible, totalBodyCells = absolute.Length,
                        visibleBodyCells = visibleCells.Select(cell => cell.ToString()).ToArray(),
                        capturedCellSight = new CellRect(x - 2, z - 2, 13, 6)
                            .Select(cell => new { x = cell.x, z = cell.z, visible = Visibility.IsVisible(map, cell) }).ToArray(),
                        state, shouldSubmitBody = expected, bodySubmissionMatchesSight = (state.DrawAtCalls > 0) == expected,
                        interaction = sample.Name == "core-boundary" || sample.Name.StartsWith("selection-", StringComparison.Ordinal)
                            ? SymbiantInteraction(target, absolute) : null,
                        geometry = state.DrawAtCalls == 0 ? null : SymbiantSightGeometry(target, sample.Fallback),
                        fallbackCoreDrawSucceeded = AccessTools.Field(symbiantType, "lastFallbackSelectionCoreDrawSucceeded").GetValue(target),
                        root = target.Position.ToString(), drawPos = target.DrawPos.ToString(),
                        renderPatchCount = AccessTools.Property(target.GetType(), "RenderPatchCount").GetValue(target),
                        renderMetaballElements = AccessTools.Property(target.GetType(), "RenderMetaballElementCount").GetValue(target),
                        gpuMask = AccessTools.Property(target.GetType(), "RenderUsesGpuMetaballMask").GetValue(target),
                        selectionCore = AccessTools.Property(target.GetType(), "SelectionCoreCell").GetValue(target).ToString(),
                        absoluteCells = absolute.Select(cell => cell.ToString()).ToArray() };
                }, cancellationToken));
                if (sample.Away) continue; // A cell-rect screenshot reframes the camera and would invalidate this native cull case.
                var picture = await ctx.Tools.CallAsync("rimworld/screenshot_cell_rect", new { fileName = evidenceLabel + "-" + sample.Name,
                    x, z, width = 9, height = 2, paddingCells = 2, rootSize = 12f, doNotResetCamera = true,
                    includeTargets = false }, cancellationToken: cancellationToken);
                if (!picture.Succeeded()) throw new InvalidOperationException("Screenshot capture failed.");
                pictures.Add(picture);
                await ctx.MainThread.InvokeAsync(() =>
                    AccessTools.Method(symbiantType, "SetDebugPerfProfile").Invoke(null, new object[] { "noRender" }), cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                var withoutBody = await ctx.Tools.CallAsync("rimworld/screenshot_cell_rect", new { fileName = evidenceLabel + "-" + sample.Name + "-body-off",
                    x, z, width = 9, height = 2, paddingCells = 2, rootSize = 12f, doNotResetCamera = true,
                    includeTargets = false }, cancellationToken: cancellationToken);
                if (!withoutBody.Succeeded()) throw new InvalidOperationException("Body-off screenshot capture failed.");
                pictures.Add(withoutBody);
                await ctx.MainThread.InvokeAsync(() =>
                {
                    AccessTools.Method(symbiantType, "SetDebugPerfProfile").Invoke(null, new object[] { "renderOnly" });
                    if (sample.Name == "core-boundary" || sample.Name.StartsWith("selection-", StringComparison.Ordinal))
                    {
                        AccessTools.Method(symbiantType, "ClearSelectionCoreMotion").Invoke(target, null);
                        AccessTools.Field(symbiantType, "selectionCoreRelative").SetValue(target, originalCore);
                    }
                }, cancellationToken);
            }
            int endTick = await ctx.MainThread.InvokeAsync(() => Find.TickManager.TicksGame, cancellationToken);
            return new { rows, pictures, startTick, matureTick, endTick, target = target.ThingID,
                zombieMvid = target.GetType().Assembly.ManifestModule.ModuleVersionId.ToString() };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id); lock (calls) calls.Clear();
                    FogSettings.BaseViewRange = oldRange; SectionLayerFog.PrefEnableFade = oldFade;
                    if (oldProfile != null)
                        AccessTools.Method(symbiantType, "SetDebugPerfProfile").Invoke(null, new object[] { oldProfile });
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                }, CancellationToken.None);
                var reload = await ctx.Tools.CallAsync("rimworld/load_game_ready", new { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!reload.Succeeded()) throw new InvalidOperationException("Fixture restoration failed.");
            }
            finally { gate.Release(); }
        }
    }

    private static object SymbiantInteraction(Pawn target, IntVec3[] cells)
    {
        if (Find.Targeter.IsTargeting) throw new InvalidOperationException("Finish the active targeting action before this scenario.");
        var core = (IntVec3)AccessTools.Property(target.GetType(), "SelectionCoreCell").GetValue(target);
        var parameters = new TargetingParameters { mustBeSelectable = true, canTargetPawns = true,
            canTargetBuildings = false, canTargetItems = false, mapObjectTargetsMustBeAutoAttackable = false };
        bool coreListed = GenUI.ThingsUnderMouse(core.ToVector3Shifted(), 0f, parameters).Contains(target);
        var tooltipOwner = AccessTools.TypeByName("ZombieLand.Patches+MapInterface_MapInterfaceOnGUI_AfterMainTabs_Patch");
        var hoverGate = AccessTools.DeclaredMethod(tooltipOwner, "CanShowSymbiantCoreTooltip") ??
            throw new InvalidOperationException("Use Zombieland's current core-hover integration.");
        bool hoverAllowed = (bool)hoverGate.Invoke(null, new object[] { target, target.Map, core });
        var interfacePolicy = AccessTools.TypeByName("TotalFog.Presentation.InterfaceVisibility");
        var tooltipCell = (IntVec3)AccessTools.DeclaredMethod(interfacePolicy, "TooltipPosition")
            .Invoke(null, new object[] { target });
        bool labelAllowed = (bool)AccessTools.DeclaredMethod(interfacePolicy, "PawnLabelPrefix")
            .Invoke(null, new object[] { target });
        Find.Selector.ClearSelection();
        Find.Selector.Select(target, false, false);
        bool selected = Find.Selector.IsSelected(target);
        Find.Selector.ClearSelection();
        object[] manual;
        Find.Targeter.BeginTargeting(parameters, (LocalTargetInfo _) => { }, requiresCastedSelected: false);
        try
        {
            manual = cells.Select(cell => (object)new { cell = cell.ToString(), visible = Visibility.IsVisible(target.Map, cell),
                offered = GenUI.TargetsAt(cell.ToVector3Shifted(), parameters, true).Any(value => value.Thing == target) }).ToArray();
        }
        finally { Find.Targeter.StopTargeting(); }
        return new { core = core.ToString(), coreVisible = Visibility.IsVisible(target.Map, core),
            rootVisible = Visibility.IsVisible(target.Map, target.Position), coreListed, selected,
            hoverAllowed, tooltipCell = tooltipCell.ToString(), tooltipCellIsInvalid = tooltipCell == IntVec3.Invalid,
            labelAllowed, manual };
    }

    private static object SymbiantSightGeometry(Pawn target, bool fallback)
    {
        var result = new List<object>();
        void Inspect(object helper, Mesh original, string part)
        {
            if (helper == null) return;
            var type = helper.GetType();
            var mask = (bool[])AccessTools.Field(type, "mask").GetValue(helper);
            int bounded = (int)AccessTools.Field(type, "previousWidth").GetValue(helper) *
                (int)AccessTools.Field(type, "previousHeight").GetValue(helper);
            int shown = mask.Take(bounded).Count(value => value);
            if (shown == 0) { result.Add(new { part, vertices = 0, triangles = 0, hiddenCenters = 0, invalidUvs = 0 }); return; }
            bool full = (bool)AccessTools.Property(type, "CurrentOriginal").GetValue(helper);
            var mesh = full ? original : (Mesh)AccessTools.Field(type, "mesh").GetValue(helper);
            var position = (Vector3)AccessTools.Field(type, "previousPosition").GetValue(helper);
            var size = (Vector2)AccessTools.Field(type, "previousSize").GetValue(helper);
            var angle = (float)AccessTools.Field(type, "previousAngle").GetValue(helper);
            var matrix = full ? Matrix4x4.TRS(position, Quaternion.Euler(0f, angle, 0f),
                new Vector3(size.x / mesh.bounds.size.x, 1f, size.y / mesh.bounds.size.z)) :
                Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            int hidden = 0;
            float renderedArea = 0f;
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                var b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                var c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                var center = matrix.MultiplyPoint3x4((vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3f);
                renderedArea += Mathf.Abs((b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x)) * .5f;
                if (!Visibility.IsVisible(target.MapHeld, center.ToIntVec3())) hidden++;
            }
            int invalidUvs = mesh.uv.Count(uv => uv.x < -0.00001f || uv.y < -0.00001f || uv.x > 1.00001f || uv.y > 1.00001f);
            result.Add(new { part, vertices = vertices.Length, triangles = indices.Length / 3,
                hiddenCenters = hidden, invalidUvs, full, shownCells = shown, boundedCells = bounded,
                originalArea = size.x * size.y, renderedArea });
        }
        var targetType = target.GetType();
        if (!fallback)
            foreach (var patch in (System.Collections.IEnumerable)AccessTools.Field(targetType, "renderPatches").GetValue(target))
                Inspect(AccessTools.Field(patch.GetType(), "sightMesh").GetValue(patch),
                    (Mesh)AccessTools.Field(patch.GetType(), "mesh").GetValue(patch), "body");
        Inspect(AccessTools.Field(targetType, "selectionCoreSightMesh").GetValue(target),
            (Mesh)AccessTools.Field(targetType, "selectionCoreMesh").GetValue(target), "core");
        return result;
    }

    [Tool("totalfog/modded_pawn_presentation", Description = "Observe native draw phases, virtual DrawAt and overlays for existing modded pawns through hidden/revealed/hidden transitions. Moves one observer on a paused fixture, advances no ticks, restores its position and removes all diagnostic patches.")]
    public static async Task<object> ModdedPawnPresentation(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string pawnIds, int frames = 12)
    {
        var ids = pawnIds.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToArray();
        if (ids.Length < 1 || ids.Length > 16 || frames < 3 || frames > 60)
            throw new ArgumentException("Stage 1..16 exact ThingIDs and use 3..60 frames per transition.");
        await gate.WaitAsync(cancellationToken);
        var harmony = new Harmony("brrainz.totalfog.modded-pawn-probe");
        Pawn viewer = null;
        Pawn[] targets = null;
        IntVec3 old = IntVec3.Invalid;
        var rows = new List<object>();
        int startTick = -1;
        var methods = new List<string>();
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                var map = Find.CurrentMap ?? throw new InvalidOperationException("Load a staged fixture first.");
                if (!Find.TickManager.Paused) throw new InvalidOperationException("Pause before observing draw frames.");
                if (!map.GetComponent<MapVisibility>().Initialized || FogSettings.OnlyOutsideColony)
                    throw new InvalidOperationException("Use initialized colony fog with its bypass disabled.");
                targets = ids.Select(id => map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.ThingID == id)
                    ?? throw new InvalidOperationException("Fixture pawn is missing or dead: " + id)).ToArray();
                if (targets.Any(p => p.Faction == Faction.OfPlayer || p.Dead))
                    throw new InvalidOperationException("Stage living non-player pawns.");
                viewer = map.mapPawns.FreeColonistsSpawned.First(p => p.TryGetComp<CompFog>()?.FieldOfViewWatcher?.LastSightRange > 2);
                old = viewer.Position;
                startTick = Find.TickManager.TicksGame;
                lock (calls) { calls.Clear(); foreach (var pawn in targets) calls.Add(pawn, new Calls()); }
                harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)),
                    postfix: new HarmonyMethod(typeof(ModdedPawnScenarios), nameof(ObservePhase)));
                var patched = new HashSet<MethodInfo> { AccessTools.Method(typeof(Pawn), nameof(Pawn.DynamicDrawPhaseAt)) };
                foreach (var pawn in targets)
                    foreach (var name in new[] { nameof(Pawn.DynamicDrawPhaseAt), nameof(Thing.DrawAt), nameof(Thing.DrawGUIOverlay) })
                    {
                        var method = AccessTools.Method(pawn.GetType(), name);
                        // Inherited MethodInfos carry a derived ReflectedType;
                        // Mono/Harmony needs the declaring type's actual body.
                        method = AccessTools.DeclaredMethod(method.DeclaringType, name,
                            method.GetParameters().Select(p => p.ParameterType).ToArray());
                        if (!patched.Add(method)) continue;
                        harmony.Patch(method, postfix: new HarmonyMethod(typeof(ModdedPawnScenarios),
                            name == nameof(Pawn.DynamicDrawPhaseAt) ? nameof(ObservePhase) :
                                name == nameof(Thing.DrawAt) ? nameof(ObserveDrawAt) : nameof(ObserveOverlay)));
                        methods.Add(method.DeclaringType.FullName + "." + method.Name);
                    }
            }, cancellationToken);
            foreach (var pawn in targets)
            {
                var camera = await ctx.Tools.CallAsync("rimworld/jump_camera_to_cell", new { x = pawn.Position.x, z = pawn.Position.z }, cancellationToken: cancellationToken);
                var hover = await ctx.Tools.CallAsync("rimworld/set_hover_target", new { x = pawn.Position.x, z = pawn.Position.z, settleMs = 0, durationMs = 30000 }, cancellationToken: cancellationToken);
                if (!camera.Succeeded() || !hover.Succeeded()) throw new InvalidOperationException("Camera/hover staging failed.");
                await ctx.MainThread.InvokeAsync(Reset, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                var hidden = await ctx.MainThread.InvokeAsync(() => Snapshot(pawn), cancellationToken);
                await ctx.MainThread.InvokeAsync(() => { MoveViewer(viewer, pawn.Position); Reset(); }, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                var visible = await ctx.MainThread.InvokeAsync(() =>
                {
                    Find.Selector.Select(pawn, false, false);
                    return Snapshot(pawn);
                }, cancellationToken);
                await ctx.MainThread.InvokeAsync(() =>
                {
                    MoveViewer(viewer, old);
                    Reset();
                }, cancellationToken);
                await ctx.Game.FramesAsync(frames, cancellationToken);
                var hiddenAgain = await ctx.MainThread.InvokeAsync(() => Snapshot(pawn), cancellationToken);
                var drawAtType = AccessTools.Method(pawn.GetType(), nameof(Thing.DrawAt)).DeclaringType;
                bool customDrawAt = drawAtType != typeof(Pawn) && drawAtType != typeof(Thing);
                rows.Add(new { id = pawn.ThingID, kind = pawn.kindDef.defName, type = pawn.GetType().FullName,
                    hidden, visible, hiddenAgain, customDrawAt,
                    passed = hidden.Hidden && !hidden.InSight && hidden.Phases.All(c => c == 0) && hidden.DrawCalls == 0 && hidden.OverlayCalls == 0 &&
                        visible.InSight && !visible.Hidden && visible.DrawCalls > 0 &&
                        (!customDrawAt || visible.DrawAtCalls > 0) &&
                        (!pawn.def.drawGUIOverlay || visible.OverlayCalls > 0) &&
                        hiddenAgain.Hidden && !hiddenAgain.InSight && hiddenAgain.Phases.All(c => c == 0) && hiddenAgain.DrawCalls == 0 &&
                        hiddenAgain.OverlayCalls == 0 && !hiddenAgain.Selected &&
                        hidden.Registrations == 1 && visible.Registrations == 1 && hiddenAgain.Registrations == 1 });
            }
            return new { rows, methods, frames, startTick, tick = Find.TickManager.TicksGame, paused = Find.TickManager.Paused };
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    harmony.UnpatchAll(harmony.Id);
                    if (viewer?.Spawned == true && old.IsValid) MoveViewer(viewer, old);
                    lock (calls) calls.Clear();
                }, CancellationToken.None);
                await ctx.Tools.CallAsync("rimworld/clear_hover_target", new { }, cancellationToken: CancellationToken.None);
            }
            finally { gate.Release(); }
        }
    }

    private sealed class State
    {
        public bool InSight, Hidden, Selected;
        public int Registrations, DrawCalls, DrawAtCalls, OverlayCalls;
        public int[] Phases;
    }
    private static State Snapshot(Pawn pawn)
    {
        lock (calls)
        {
            var c = calls[pawn];
            return new State { InSight = pawn.Map.GetComponent<MapVisibility>().IsShown(Faction.OfPlayer, pawn.Position),
                Hidden = pawn.TryGetComp<CompFog>().Hiddenable.Hidden, Selected = Find.Selector.IsSelected(pawn),
                Registrations = pawn.Map.dynamicDrawManager.DrawThings.Count(t => t == pawn),
                Phases = (int[])c.Phases.Clone(), DrawAtCalls = c.DrawAt, OverlayCalls = c.Overlay,
                DrawCalls = c.Phases[(int)DrawPhase.Draw] + c.DrawAt };
        }
    }
    private static void MoveViewer(Pawn viewer, IntVec3 position)
    {
        viewer.Position = position;
        viewer.TryGetComp<CompFog>().CompTick();
    }
    private static void Reset()
    {
        lock (calls)
            foreach (var c in calls.Values) { Array.Clear(c.Phases, 0, c.Phases.Length); c.DrawAt = c.Overlay = 0; }
    }
    private static void ObservePhase(Pawn __instance, DrawPhase phase, bool __runOriginal)
    {
        if (!__runOriginal) return;
        lock (calls) if (calls.TryGetValue(__instance, out var c)) c.Phases[(int)phase]++;
    }
    private static void ObserveDrawAt(Thing __instance, bool __runOriginal)
    {
        if (!__runOriginal || __instance is not Pawn pawn) return;
        lock (calls) if (calls.TryGetValue(pawn, out var c)) c.DrawAt++;
    }
    private static void ObserveOverlay(Thing __instance, bool __runOriginal)
    {
        if (!__runOriginal || __instance is not Pawn pawn) return;
        lock (calls) if (calls.TryGetValue(pawn, out var c)) c.Overlay++;
    }
}
