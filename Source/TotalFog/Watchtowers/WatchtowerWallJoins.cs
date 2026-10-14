using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using CaptureLayer = Verse.SectionLayer_ThingsGeneral;

namespace TotalFog.Watchtowers;

/// <summary>Cached fragments of native wall Print geometry.</summary>
internal sealed class WatchtowerWallJoins
{
    private const float SideInnerAtCenter = 1.34f;
    private const float SideCutSlope = .32f;
    private const float EndInnerExtent = 1.05f;
    private readonly CaptureLayer layer;
    private MaterialPropertyBlock fogTint;
    internal readonly List<IntVec3> Directions = new();
    internal int DrawCalls => layer.subMeshes.Count;
    internal bool HasGeometry => VertexCount() > 0;
    internal float MappingError { get; private set; }

    internal WatchtowerWallJoins(WatchtowerGraphic tower)
    {
        var section = tower.Map.mapDrawer.SectionAt(tower.Position);
        layer = new CaptureLayer(section);
        var center = tower.TrueCenter();
        try
        {
            foreach (var direction in GenAdj.CardinalDirections)
            {
                var cell = tower.Position + direction * 2;
                if (!cell.InBounds(tower.Map))
                    continue;
                var wall = cell.GetEdifice(tower.Map);
                if (
                    wall == null
                    || wall.def.size != new IntVec2(1, 1)
                    || (wall.def.graphicData?.linkFlags & LinkFlags.Wall) != LinkFlags.Wall
                )
                    continue;
                var capture = new CaptureLayer(section);
                try
                {
                    wall.Print(capture);
                    var outward = direction.ToVector3();
                    var shift = -outward * .65f;
                    var outer = new ClipPlane(-outward, 1.55f);
                    var inner =
                        direction.x != 0
                            ? new ClipPlane(
                                outward + new Vector3(0, 0, SideCutSlope),
                                -SideInnerAtCenter
                            )
                            : new ClipPlane(outward, -EndInnerExtent);
                    int before = VertexCount();
                    foreach (var source in capture.subMeshes)
                        for (int triangle = 0; triangle < source.tris.Count; triangle += 3)
                        {
                            var polygon = new List<Vertex>(3);
                            for (int corner = 0; corner < 3; corner++)
                            {
                                int index = source.tris[triangle + corner];
                                var position = source.verts[index] + shift - center;
                                position.y = source.verts[index].y - wall.TrueCenter().y;
                                polygon.Add(new Vertex(source, index, position));
                            }
                            polygon = Clip(Clip(polygon, outer), inner);
                            if (polygon.Count < 3)
                                continue;
                            Append(source, polygon, center);
                        }
                    if (VertexCount() > before)
                        Directions.Add(direction);
                }
                finally
                {
                    capture.Dispose();
                }
            }
        }
        catch
        {
            layer.Dispose();
            throw;
        }
    }

    // Separate artwork experiment. The source is a real straight native wall.
    // Copy its front band at native map scale, using translation and clipping
    // only. The accepted tower remains the default and its deck is unchanged.
    internal WatchtowerWallJoins(WatchtowerGraphic tower, Thing wall, Vector3[] face)
    {
        var section = tower.Map.mapDrawer.SectionAt(tower.Position);
        layer = new CaptureLayer(section);
        var capture = new CaptureLayer(section);
        var wallCenter = wall.TrueCenter();
        var center = tower.TrueCenter();
        const float bandBottom = -.46f;
        const float bandTop = -.23f;
        const float bandHeight = bandTop - bandBottom;
        float minZ = float.MaxValue;
        float maxZ = float.MinValue;
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (var point in face)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }
        try
        {
            wall.Print(capture);
            foreach (var source in capture.subMeshes)
                for (int triangle = 0; triangle < source.tris.Count; triangle += 3)
                {
                    var basis = new Vertex[3];
                    for (int corner = 0; corner < 3; corner++)
                    {
                        int index = source.tris[triangle + corner];
                        basis[corner] = new Vertex(source, index, source.verts[index] - wallCenter);
                    }
                    var band = Clip(
                        Clip(new List<Vertex>(basis), new ClipPlane(Vector3.forward, -bandBottom)),
                        new ClipPlane(Vector3.back, bandTop)
                    );
                    if (band.Count < 3)
                        continue;
                    for (float z = minZ; z < maxZ; z += bandHeight)
                    for (float x = Mathf.Floor(minX + .5f); x - .5f < maxX; x += 1f)
                    {
                        var shift = new Vector3(x, 0, z - bandBottom);
                        var polygon = new List<Vertex>(band.Count);
                        foreach (var vertex in band)
                            polygon.Add(vertex.Move(shift));
                        for (int edge = 0; edge < face.Length; edge++)
                        {
                            var a = face[edge];
                            var b = face[(edge + 1) % face.Length];
                            var normal = new Vector3(b.z - a.z, 0, a.x - b.x);
                            var plane = new ClipPlane(normal, -Vector3.Dot(normal, a));
                            var middle = new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2);
                            if (plane.Distance(middle) < 0)
                                plane = new ClipPlane(-normal, Vector3.Dot(normal, a));
                            polygon = Clip(polygon, plane);
                        }
                        if (polygon.Count < 3)
                            continue;
                        foreach (var vertex in polygon)
                            MappingError = Mathf.Max(MappingError, UvError(basis, vertex, shift));
                        Append(source, polygon, center);
                    }
                }
        }
        catch
        {
            layer.Dispose();
            throw;
        }
        finally
        {
            capture.Dispose();
        }
    }

    private void Append(LayerSubMesh source, List<Vertex> polygon, Vector3 center)
    {
        var target = layer.GetSubMesh(source.material);
        target.renderLayer = source.renderLayer;
        int start = target.verts.Count;
        foreach (var vertex in polygon)
        {
            target.verts.Add(
                new Vector3(
                    vertex.position.x + center.x,
                    vertex.position.y,
                    vertex.position.z + center.z
                )
            );
            target.uvs.Add(vertex.uv);
            target.uvsChannelTwo.Add(vertex.uvTwo);
            target.colors.Add(vertex.color);
            target.normals.Add(vertex.normal);
        }
        for (int corner = 1; corner < polygon.Count - 1; corner++)
        {
            target.tris.Add(start);
            target.tris.Add(start + corner);
            target.tris.Add(start + corner + 1);
        }
    }

    // Independently reconstruct the source triangle's UV at each copied point.
    // A scaled or sheared position with unchanged UVs fails this map-space check.
    private static float UvError(Vertex[] source, Vertex copy, Vector3 shift)
    {
        var a = source[1].position - source[0].position;
        var b = source[2].position - source[0].position;
        var p = copy.position - shift - source[0].position;
        float determinant = a.x * b.z - a.z * b.x;
        if (Mathf.Abs(determinant) < .000001f)
            return float.PositiveInfinity;
        float u = (p.x * b.z - p.z * b.x) / determinant;
        float v = (a.x * p.z - a.z * p.x) / determinant;
        var expected =
            source[0].uv + (source[1].uv - source[0].uv) * u + (source[2].uv - source[0].uv) * v;
        return Vector3.Distance(expected, copy.uv);
    }

    internal void Complete(float altitude)
    {
        foreach (var subMesh in layer.subMeshes)
        {
            for (int i = 0; i < subMesh.verts.Count; i++)
            {
                var vertex = subMesh.verts[i];
                vertex.y += altitude;
                subMesh.verts[i] = vertex;
            }
            subMesh.FinalizeMesh(MeshParts.All);
            subMesh.mesh.RecalculateBounds();
        }
    }

    internal void Draw(float brightness = 1)
    {
        if (brightness == 1)
        {
            layer.DrawLayer();
            return;
        }
        fogTint ??= new MaterialPropertyBlock();
        foreach (var subMesh in layer.subMeshes)
            if (!subMesh.disabled)
            {
                var color = subMesh.material.color;
                color.r *= brightness;
                color.g *= brightness;
                color.b *= brightness;
                fogTint.SetColor("_Color", color);
                Graphics.DrawMesh(
                    subMesh.mesh,
                    Vector3.zero,
                    Quaternion.identity,
                    subMesh.material,
                    subMesh.renderLayer,
                    null,
                    0,
                    fogTint
                );
            }
    }

    internal void Dispose() => layer.Dispose();

    internal static void GetCutEdge(IntVec3 direction, out Vector3 a, out Vector3 b)
    {
        if (direction.x != 0)
        {
            a = new Vector3(direction.x * (SideInnerAtCenter + SideCutSlope / 2), 0, -.50f);
            b = new Vector3(direction.x * (SideInnerAtCenter - SideCutSlope / 2), 0, .50f);
        }
        else
        {
            a = new Vector3(-.50f, 0, direction.z * EndInnerExtent);
            b = new Vector3(.50f, 0, direction.z * EndInnerExtent);
        }
    }

    private int VertexCount()
    {
        int count = 0;
        foreach (var subMesh in layer.subMeshes)
            count += subMesh.verts.Count;
        return count;
    }

    private static List<Vertex> Clip(List<Vertex> input, ClipPlane plane)
    {
        var output = new List<Vertex>(input.Count + 1);
        if (input.Count == 0)
            return output;
        var previous = input[input.Count - 1];
        float previousDistance = plane.Distance(previous.position);
        foreach (var current in input)
        {
            float currentDistance = plane.Distance(current.position);
            if ((previousDistance >= 0) != (currentDistance >= 0))
                output.Add(
                    Vertex.Lerp(
                        previous,
                        current,
                        previousDistance / (previousDistance - currentDistance)
                    )
                );
            if (currentDistance >= 0)
                output.Add(current);
            previous = current;
            previousDistance = currentDistance;
        }
        return output;
    }

    private readonly struct ClipPlane
    {
        private readonly Vector3 normal;
        private readonly float offset;

        internal ClipPlane(Vector3 normal, float offset)
        {
            this.normal = normal;
            this.offset = offset;
        }

        internal float Distance(Vector3 position) =>
            normal.x * position.x + normal.z * position.z + offset;
    }

    private readonly struct Vertex
    {
        internal readonly Vector3 position;
        internal readonly Vector3 uv;
        internal readonly Vector3 uvTwo;
        internal readonly Color32 color;
        internal readonly Vector3 normal;

        internal Vertex(LayerSubMesh source, int index, Vector3 position)
            : this(
                position,
                source.uvs.Count > index ? source.uvs[index] : Vector3.zero,
                source.uvsChannelTwo.Count > index ? source.uvsChannelTwo[index] : Vector3.zero,
                source.colors.Count > index ? source.colors[index] : (Color32)Color.white,
                source.normals.Count > index ? source.normals[index] : Vector3.up
            ) { }

        private Vertex(Vector3 position, Vector3 uv, Vector3 uvTwo, Color32 color, Vector3 normal)
        {
            this.position = position;
            this.uv = uv;
            this.uvTwo = uvTwo;
            this.color = color;
            this.normal = normal;
        }

        internal static Vertex Lerp(Vertex a, Vertex b, float t) =>
            new(
                Vector3.LerpUnclamped(a.position, b.position, t),
                Vector3.LerpUnclamped(a.uv, b.uv, t),
                Vector3.LerpUnclamped(a.uvTwo, b.uvTwo, t),
                Color32.Lerp(a.color, b.color, t),
                Vector3.LerpUnclamped(a.normal, b.normal, t)
            );

        internal Vertex Move(Vector3 shift) => new(position + shift, uv, uvTwo, color, normal);
    }
}
