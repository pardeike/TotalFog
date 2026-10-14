// Fog state and fading rewritten for Total Fog, 2026-10-04.
using System;
using RimWorld;
using TotalFog.Core;
using UnityEngine;
using Verse;

namespace TotalFog;

public class SectionLayerFog : SectionLayer
{
    public static bool PrefEnableFade = true;
    public static int PrefFadeSpeedMult = 20;
    public static byte PrefFogAlpha = 86;
    private readonly byte[] samples = new byte[19 * 19];
    private byte[] targetAlphas = [];
    private Color32[] colors = [];
    private bool fading;
    private int lastFadeTick;
    private MapVisibility fog;
    private int towerGeometryRevision = -1;
    private bool loweredForTowers;

    public SectionLayerFog(Section section)
        : base(section) => relevantChangeTypes = FogDefOf.RealFogOfWar | MapMeshFlagDefOf.FogOfWar;

    public override bool Visible =>
        DebugViewSettings.drawFog && !Presentation.ThingVisibility.Bypass(Map);

    // The existing nine-vertex topology is retained: it matches the vanilla
    // material and the independently tested shared-edge alpha contract.
    private static void MakeBaseGeometry(
        Section section,
        LayerSubMesh sm,
        AltitudeLayer altitudeLayer
    )
    {
        var cellRect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
        cellRect.ClipInsideMap(section.map);
        var y = altitudeLayer.AltitudeFor();
        sm.verts.Capacity = cellRect.Area * 9;
        for (var i = cellRect.minX; i <= cellRect.maxX; i++)
        {
            for (var j = cellRect.minZ; j <= cellRect.maxZ; j++)
            {
                sm.verts.Add(new Vector3(i, y, j));
                sm.verts.Add(new Vector3(i, y, j + 0.5f));
                sm.verts.Add(new Vector3(i, y, j + 1));
                sm.verts.Add(new Vector3(i + 0.5f, y, j + 1));
                sm.verts.Add(new Vector3(i + 1, y, j + 1));
                sm.verts.Add(new Vector3(i + 1, y, j + 0.5f));
                sm.verts.Add(new Vector3(i + 1, y, j));
                sm.verts.Add(new Vector3(i + 0.5f, y, j));
                sm.verts.Add(new Vector3(i + 0.5f, y, j + 0.5f));
            }
        }

        var num = cellRect.Area * 8 * 3;
        sm.tris.Capacity = num;
        var num2 = 0;
        while (sm.tris.Count < num)
        {
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 2);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 4);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 6);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 1);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 3);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 8);
            sm.tris.Add(num2 + 5);
            sm.tris.Add(num2 + 7);
            sm.tris.Add(num2 + 8);
            num2 += 9;
        }

        sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris);
    }

    public override void Regenerate()
    {
        if (Current.ProgramState != ProgramState.Playing)
            return;
        fog ??= Map.GetVisibility();
        if (!fog.Initialized)
            return;
        var mesh = GetSubMesh(MatBases.FogOfWar);
        bool first = mesh.mesh.vertexCount == 0;
        if (first)
        {
            mesh.mesh.MarkDynamic();
            MakeBaseGeometry(section, mesh, AltitudeLayer.FogOfWar);
            targetAlphas = new byte[mesh.mesh.vertexCount];
            colors = new Color32[targetAlphas.Length];
        }
        UpdateTowerFogHeight(mesh);
        bool wasFading = fading;
        var rect = section.CellRect;
        var counts = fog.GetFactionShownCells(Faction.OfPlayer);
        var known = fog.GetFactionKnownCells(Faction.OfPlayer);
        int width = Map.Size.x,
            height = Map.Size.z,
            stride = rect.Width + 2;
        // Each neighboring cell supplies up to nine vertices/cells. Read its
        // current fog state once, including the clipped section's border.
        for (int z = -1; z <= rect.Height; z++)
        for (int x = -1; x <= rect.Width; x++)
        {
            int sx = Math.Max(0, Math.Min(width - 1, rect.minX + x));
            int sz = Math.Max(0, Math.Min(height - 1, rect.minZ + z));
            int index = sz * width + sx;
            samples[(z + 1) * stride + x + 1] = FogAppearance.CellAlpha(
                Map.fogGrid.IsFogged(index),
                known != null && known[index],
                counts[index],
                PrefFogAlpha
            );
        }
        // The tower is one opaque building, not nine independently drawable
        // surfaces. Match its whole-building rendering within the solid base.
        // Ground sight, targeting and cells behind it retain the original grid.
        foreach (var tower in fog.TowerComponent.Towers)
        {
            var footprint = tower.OccupiedRect();
            if (!footprint.Overlaps(rect.ExpandedBy(1)))
                continue;
            byte alpha = byte.MaxValue;
            foreach (var cell in footprint.ClipInsideMap(Map))
            {
                int index = Map.cellIndices.CellToIndex(cell);
                alpha = Math.Min(
                    alpha,
                    FogAppearance.CellAlpha(
                        Map.fogGrid.IsFogged(index),
                        known != null && known[index],
                        counts[index],
                        PrefFogAlpha
                    )
                );
                if (alpha == 0)
                    break;
            }
            TowerGeometry.FillFogFootprint(
                samples,
                stride,
                rect.Height + 2,
                rect.minX - 1,
                rect.minZ - 1,
                tower.Position.x,
                tower.Position.z,
                Map.Size.x,
                Map.Size.z,
                alpha
            );
        }
        FogAppearance.FillSection(samples, rect.Width, rect.Height, targetAlphas);
        for (int vertex = 0; vertex < targetAlphas.Length; vertex++)
        {
            byte target = targetAlphas[vertex];
            if (first || !PrefEnableFade)
                colors[vertex] = new Color32(255, 255, 255, target);
            else if (colors[vertex].a != target)
                fading = true;
        }
        if (first || !PrefEnableFade)
        {
            fading = false;
            Upload(mesh);
        }
        if (first || !wasFading && fading)
            lastFadeTick = Find.TickManager.TicksGame;
    }

    private void UpdateTowerFogHeight(LayerSubMesh mesh)
    {
        var towers = fog.TowerComponent;
        if (towerGeometryRevision == towers.FogGeometryRevision)
            return;
        towerGeometryRevision = towers.FogGeometryRevision;
        bool overlaps = false;
        foreach (var tower in towers.Towers)
            if (
                new CellRect(tower.Position.x - 2, tower.Position.z - 2, 5, 6).Overlaps(
                    section.CellRect.ExpandedBy(1)
                )
            )
            {
                overlaps = true;
                break;
            }
        if (!overlaps && !loweredForTowers)
            return;
        loweredForTowers = false;
        float ordinary = AltitudeLayer.FogOfWar.AltitudeFor();
        float belowTower = AltitudeLayer.BuildingOnTop.AltitudeFor() - Altitudes.AltInc;
        for (int i = 0; i < mesh.verts.Count; i++)
        {
            var vertex = mesh.verts[i];
            vertex.y = ordinary;
            foreach (var tower in towers.Towers)
                if (
                    TowerGeometry.FogDrawingContains(
                        vertex.x,
                        vertex.z,
                        tower.Position.x,
                        tower.Position.z
                    )
                )
                {
                    vertex.y = belowTower;
                    loweredForTowers = true;
                    break;
                }
            mesh.verts[i] = vertex;
        }
        // Preserve triangles/colors; FinalizeMesh clears an existing mesh.
        mesh.mesh.SetVertices(mesh.verts);
        mesh.mesh.RecalculateBounds();
    }

    private void Upload(LayerSubMesh mesh)
    {
        bool opaque = false;
        foreach (var color in colors)
            if (color.a != 0)
            {
                opaque = true;
                break;
            }
        mesh.disabled = !opaque;
        if (opaque)
            mesh.mesh.colors32 = colors;
    }

    public override void DrawLayer()
    {
        int tick = Find.TickManager.TicksGame;
        bool paused = Find.TickManager.Paused;
        // Offscreen sections can retain an unfinished fade. Settle their
        // presentation while paused, when simulation ticks cannot advance it.
        if (fading && Visible && (paused || tick > lastFadeTick))
        {
            int speed = Math.Max(
                1,
                PrefFadeSpeedMult / Math.Max(1, (int)Find.TickManager.CurTimeSpeed)
            );
            bool changed = false;
            fading = false;
            for (int i = 0; i < colors.Length; i++)
            {
                byte alpha = FogAppearance.Advance(
                    colors[i].a,
                    targetAlphas[i],
                    tick - lastFadeTick,
                    speed,
                    paused
                );
                if (alpha != colors[i].a)
                {
                    colors[i].a = alpha;
                    changed = true;
                }
                if (alpha != targetAlphas[i])
                    fading = true;
            }
            lastFadeTick = tick;
            if (changed)
                Upload(GetSubMesh(MatBases.FogOfWar));
        }
        base.DrawLayer();
    }
}
