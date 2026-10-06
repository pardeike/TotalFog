using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Paused native rendering cost, kept separate from simulation/TPS acceptance.</summary>
public sealed class SymbiantRenderCostScenarios
{
    private static readonly SemaphoreSlim gate = new(1, 1);

    [Tool("totalfog/symbiant_render_cost", Description = "Create a 400/4000-cell dense or sparse hostless render-only Symbiant; mature it, time actual native gate/draw/clip/preparation calls on hidden, partial and off-camera views, then alternate clipped and unclipped-cell-API controls with colony bypass enabled. Records native frames/cells/resources. Restores the optional delegate/settings/debug profile and reloads the unchanged named save. Paused diagnostic, not TPS acceptance.")]
    public static async Task<object> RenderCost(IRimBridgeContext ctx, CancellationToken cancellationToken,
        string saveName, int count = 400, bool sparse = false, int frames = 60, int pairs = 3, int x = 40, int z = 40)
    {
        if (count is not (400 or 4000) || frames < 30 || frames > 120 || pairs < 1 || pairs > 7)
            throw new ArgumentException("Use 400/4000 cells, 30..120 frames and 1..7 pairs.");
        await gate.WaitAsync(cancellationToken);
        int oldRange = FogSettings.BaseViewRange;
        bool oldBypass = FogSettings.OnlyOutsideColony;
        Type type = null;
        FieldInfo cellApi = null;
        object originalCellApi = null;
        string oldProfile = null;
        int oldMax = 0;
        Map map = null;
        Pawn target = null, viewer = null;
        var rows = new List<object>();
        var width = count == 400 ? 20 : 80;
        var height = count / width;
        var stride = sparse ? 2 : 1;
        var bodyWidth = (width - 1) * stride + 1;
        var bodyHeight = (height - 1) * stride + 1;
        try
        {
            await ctx.MainThread.InvokeAsync(() =>
            {
                map = Find.CurrentMap ?? throw new InvalidOperationException("Load the named fixture first.");
                if (!Find.TickManager.Paused || !map.IsPlayerHome || !map.GetVisibility().Initialized)
                    throw new InvalidOperationException("Use a paused initialized home map.");
                if (!new IntVec3(x, 0, z).InBounds(map) || !new IntVec3(x + bodyWidth - 1, 0, z + bodyHeight - 1).InBounds(map))
                    throw new InvalidOperationException("The complete render shape must fit inside the map.");
                type = AccessTools.TypeByName("ZombieLand.ZombieSymbiant") ?? throw new InvalidOperationException("Zombieland is missing.");
                oldProfile = (string)AccessTools.Property(type, "DebugPerfProfile").GetValue(null);
                oldMax = (int)AccessTools.Property(type, "DebugMaxCellsOverride").GetValue(null);
                cellApi = AccessTools.Field(AccessTools.TypeByName("ZombieLand.TotalFogSupport"), "isCellVisible");
                originalCellApi = cellApi?.GetValue(null) ?? throw new InvalidOperationException("The optional cell API is not bound.");
                AccessTools.Method(type, "SetDebugMaxCellsOverride").Invoke(null, new object[] { count });
                FogSettings.BaseViewRange = 5;
                FogSettings.OnlyOutsideColony = true;
                var area = new CellRect(x - 8, z - 8, bodyWidth + 16, bodyHeight + 16);
                area.ClipInsideMap(map);
                foreach (var cell in area) map.fogGrid.Unfog(cell);
                viewer = map.mapPawns.FreeColonistsSpawned.First(p => p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null);
                foreach (var observer in map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer &&
                    p.TryGetComp<CompFog>()?.FieldOfViewWatcher != null).ToArray())
                    Move(observer, new IntVec3(map.Size.x - 15, 0, map.Size.z - 15));
            }, cancellationToken);
            string cells = string.Join(";", Enumerable.Range(0, count).Select(i => (i % width * stride) + "," + (i / width * stride)));
            var created = await ctx.Tools.CallAsync("zombieland/symbiant_render_blob", new
                { cells, x, z, select = false, jump = false, perfProfile = "renderOnly" }, cancellationToken: cancellationToken);
            if (!created.Succeeded()) throw new InvalidOperationException("Render-shape creation failed.");
            await ctx.MainThread.InvokeAsync(() =>
            {
                target = map.mapPawns.AllPawnsSpawned.Single(p => p.GetType() == type);
                if ((int)AccessTools.Property(type, "CellCount").GetValue(target) != count)
                    throw new InvalidOperationException("The native shape was truncated.");
                Find.Selector.ClearSelection();
            }, cancellationToken);
            var framed = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new
                { x, z, width = bodyWidth, height = bodyHeight, rootSize = 60f }, cancellationToken: cancellationToken);
            if (!framed.Succeeded()) throw new InvalidOperationException("Body framing failed.");
            await ctx.Game.StepTicksAsync(61, cancellationToken: cancellationToken);
            var methods = new MethodBase[] { AccessTools.DeclaredMethod(type, "HasVisibleRenderPart"),
                AccessTools.DeclaredMethod(type, "DrawAt"), AccessTools.DeclaredMethod(type, "TryPrepareMetaballRendering"),
                AccessTools.DeclaredMethod(type.GetNestedType("SightMesh", BindingFlags.NonPublic), "Prepare"),
                AccessTools.DeclaredMethod(type, "EnsureSymbiantDefaults") };

            foreach (var mode in new[] { "hidden-cold", "partial", "hidden-warm", "off-camera" })
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    FogSettings.OnlyOutsideColony = false;
                    // Maturation ticks can move observers back towards the colony,
                    // which the large sparse footprint encloses. Reset every active
                    // pawn sight source after maturation, immediately before each row.
                    foreach (var source in map.GetVisibility().fowWatchers.ToArray())
                        if (source.LastSightRange >= 0 && source.parent is Pawn observer)
                            Move(observer, new IntVec3(map.Size.x - 15, 0, map.Size.z - 15));
                    Move(viewer, mode is "partial" or "off-camera" ? target.Position : new IntVec3(map.Size.x - 15, 0, map.Size.z - 15));
                    if (mode == "hidden-cold") AccessTools.Method(type, "ReleaseRenderResources").Invoke(target, new object[] { false });
                    map.GetVisibility().MapComponentTick();
                }, cancellationToken);
                var camera = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", mode == "off-camera"
                    ? new { x = 210, z = 210, width = 15, height = 8, rootSize = 12f }
                    : new { x, z, width = bodyWidth, height = bodyHeight, rootSize = 60f }, cancellationToken: cancellationToken);
                if (!camera.Succeeded()) throw new InvalidOperationException("Sample camera framing failed.");
                await ctx.Game.FramesAsync(6, cancellationToken);
                await ctx.MainThread.InvokeAsync(() =>
                {
                    if (mode is "hidden-cold" or "hidden-warm" &&
                        ((IEnumerable<IntVec3>)AccessTools.Property(type, "AbsoluteCells").GetValue(target)).Any(cell => Visibility.IsVisible(map, cell)))
                        throw new InvalidOperationException("The hidden row still contains current body sight.");
                    if (mode == "hidden-cold" && (int)AccessTools.Property(type, "RenderPatchCount").GetValue(target) != 0)
                        throw new InvalidOperationException("The released hidden body rebuilt before measurement.");
                }, cancellationToken);
                rows.Add(new { mode, clipped = true, state = await State(),
                    measurement = await PerformanceScenarios.PausedRenderProfile(ctx, cancellationToken, frames, methods) });
            }
            await ctx.MainThread.InvokeAsync(() => FogSettings.OnlyOutsideColony = true, cancellationToken);
            var bypassCamera = await ctx.Tools.CallAsync("rimworld/frame_cell_rect", new
                { x, z, width = bodyWidth, height = bodyHeight, rootSize = 60f }, cancellationToken: cancellationToken);
            if (!bypassCamera.Succeeded()) throw new InvalidOperationException("Control framing failed.");
            for (int pair = 0; pair < pairs; pair++)
                foreach (bool clipped in pair % 2 == 0 ? new[] { false, true } : new[] { true, false })
                {
                    await ctx.MainThread.InvokeAsync(() =>
                    {
                        cellApi.SetValue(null, clipped ? originalCellApi : null);
                        bool bound = (bool)AccessTools.Property(AccessTools.TypeByName("ZombieLand.TotalFogSupport"), "HasCellVisibilityFilter").GetValue(null);
                        if (bound != clipped) throw new InvalidOperationException("The optional cell-API control did not take effect.");
                    }, cancellationToken);
                    await ctx.Game.FramesAsync(6, cancellationToken);
                    rows.Add(new { mode = "bypass-control", pair, clipped, state = await State(),
                        measurement = await PerformanceScenarios.PausedRenderProfile(ctx, cancellationToken, frames, methods) });
                }
            return new { count, sparse, bodyWidth, bodyHeight, rows, diagnosticOnly = true,
                timing = "Inclusive native Stopwatch elapsed; includes probe/scheduling overhead; GPU completion is not measured." };

            Task<object> State() => ctx.MainThread.InvokeAsync<object>(() => new
            {
                tick = Find.TickManager.TicksGame, cells = AccessTools.Property(type, "CellCount").GetValue(target),
                patches = AccessTools.Property(type, "RenderPatchCount").GetValue(target),
                elements = AccessTools.Property(type, "RenderMetaballElementCount").GetValue(target),
                gpuMask = AccessTools.Property(type, "RenderUsesGpuMetaballMask").GetValue(target),
                registrations = map.dynamicDrawManager.DrawThings.Count(thing => thing == target),
                bodyCurrentCells = ((IEnumerable<IntVec3>)AccessTools.Property(type, "AbsoluteCells").GetValue(target)).Count(cell => Visibility.IsVisible(map, cell)),
                nativeCameraContainsBody = Find.CameraDriver.CurrentViewRect.Overlaps(target.OccupiedDrawRect()),
                camera = Find.CameraDriver.CurrentViewRect.ToString(), rootCurrent = Visibility.IsVisible(map, target.Position)
            }, cancellationToken);
        }
        finally
        {
            try
            {
                await ctx.MainThread.InvokeAsync(() =>
                {
                    if (originalCellApi != null) cellApi.SetValue(null, originalCellApi);
                    if (type != null && oldProfile != null)
                    {
                        AccessTools.Method(type, "SetDebugPerfProfile").Invoke(null, new object[] { oldProfile });
                        AccessTools.Method(type, "SetDebugMaxCellsOverride").Invoke(null, new object[] { oldMax });
                    }
                    FogSettings.BaseViewRange = oldRange;
                    FogSettings.OnlyOutsideColony = oldBypass;
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
                }, CancellationToken.None);
                var restored = await ctx.Tools.CallAsync("rimworld/load_game_ready", new
                    { saveName, readiness = "visual", pauseIfNeeded = true }, cancellationToken: CancellationToken.None);
                if (!restored.Succeeded()) throw new InvalidOperationException("Cost fixture restoration failed.");
            }
            finally { gate.Release(); }
        }
    }

    private static void Move(Pawn pawn, IntVec3 position)
    {
        pawn.Position = position;
        pawn.TryGetComp<CompFog>().CompTick();
        pawn.TryGetComp<CompFog>().FieldOfViewWatcher.UpdateFoV(true);
    }
}
