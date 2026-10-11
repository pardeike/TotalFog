using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Cached, test-only fragments of the actual neighbouring walls.</summary>
internal sealed class WatchtowerWallJoins
{
    private const float SideInnerAtCenter = 1.34f;
    private const float SideCutSlope = .32f;
    private const float EndInnerExtent = 1.05f;
    private readonly CaptureLayer layer;
    internal readonly List<IntVec3> Directions = new();
    internal int DrawCalls => layer.subMeshes.Count;

    internal WatchtowerWallJoins(WatchtowerVisualPrototype tower)
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

    internal void Draw() => layer.DrawLayer();

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
    }

    private sealed class CaptureLayer : SectionLayer
    {
        internal CaptureLayer(Section section)
            : base(section) { }

        public override void Regenerate() { }
    }
}
