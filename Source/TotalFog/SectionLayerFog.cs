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

    public SectionLayerFog(Section section)
        : base(section) => relevantChangeTypes = FogDefOf.RealFogOfWar | MapMeshFlagDefOf.FogOfWar;

    public override bool Visible =>
        DebugViewSettings.drawFog && !Presentation.ThingVisibility.Bypass(Map);

    // The existing nine-vertex topology is retained: it matches the vanilla
    // material and the independently tested shared-edge alpha contract.
    private static void makeBaseGeometry(
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
            makeBaseGeometry(section, mesh, AltitudeLayer.FogOfWar);
            targetAlphas = new byte[mesh.mesh.vertexCount];
            colors = new Color32[targetAlphas.Length];
        }
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
        if (fading && Visible && tick > lastFadeTick)
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
                    speed
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
