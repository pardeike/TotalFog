using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>
/// Test-only artwork. Native building and pawn objects stay on the map; no
/// occupation, pathfinding, sight or combat rule is inferred from this drawing.
/// </summary>
public sealed class WatchtowerVisualPrototype : Building
{
    internal const float PlatformProjection = 1.20f;
    private Mesh body;
    private Mesh foreground;
    private Material material;
    private WatchtowerWallJoins wallJoins;

    internal int WallJoinCount => wallJoins?.Directions.Count ?? 0;
    internal int CachedDrawCalls => 2 + (wallJoins?.DrawCalls ?? 0);

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        material = SolidColorMaterials.SimpleSolidColorMaterial(Color.white, true);
        var drawing = new Drawing(
            Rotation,
            Stuff.stuffProps.color,
            Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony),
            Array.Empty<IntVec3>()
        );
        (body, foreground) = drawing.Build();
    }

    // The artwork fixture calls this after its walls have spawned. Production
    // neighbour invalidation belongs to the later gameplay implementation.
    internal void RebuildWallJoins()
    {
        var next = new WatchtowerWallJoins(this);
        Mesh nextBody = null;
        Mesh nextForeground = null;
        try
        {
            var drawing = new Drawing(
                Rotation,
                Stuff.stuffProps.color,
                Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony),
                next.Directions
            );
            (nextBody, nextForeground) = drawing.Build();
            next.Complete(AltitudeLayer.BuildingOnTop.AltitudeFor() + drawing.WallJoinDepth);
            UnityEngine.Object.Destroy(body);
            UnityEngine.Object.Destroy(foreground);
            wallJoins?.Dispose();
            body = nextBody;
            foreground = nextForeground;
            wallJoins = next;
        }
        catch
        {
            UnityEngine.Object.Destroy(nextBody);
            UnityEngine.Object.Destroy(nextForeground);
            next.Dispose();
            throw;
        }
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        drawLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        Graphics.DrawMesh(body, drawLoc, Quaternion.identity, material, 0);
        wallJoins?.Draw();
        drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
        Graphics.DrawMesh(foreground, drawLoc, Quaternion.identity, material, 0);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);
        UnityEngine.Object.Destroy(body);
        UnityEngine.Object.Destroy(foreground);
        body = foreground = null;
        wallJoins?.Dispose();
        wallJoins = null;
    }

    private sealed class Drawing
    {
        private const float BaseHalfExtent = 1.50f;
        private const float DeckHalfExtent = .70f;
        private const float RailHalfExtent = .65f;
        private const float DeckShiftTowardViewer = .18f;
        private readonly Rot4 rotation;
        private readonly Color stuff;
        private readonly bool stone;
        private readonly IReadOnlyList<IntVec3> wallDirections;
        private readonly Geometry body = new();
        private readonly Geometry front = new();
        private readonly Color outline = new(.10f, .095f, .085f);

        internal float WallJoinDepth { get; private set; }

        internal Drawing(
            Rot4 rotation,
            Color stuff,
            bool stone,
            IReadOnlyList<IntVec3> wallDirections
        )
        {
            this.rotation = rotation;
            this.stuff = stuff;
            this.stone = stone;
            this.wallDirections = wallDirections;
        }

        internal (Mesh, Mesh) Build()
        {
            Supports();
            bool rearLadder = Point(0, -1, 0).z > 0;
            if (rearLadder)
                Ladder();
            if (wallDirections.Count > 0)
            {
                WallJoinDepth = body.ReserveDepth();
                foreach (var direction in wallDirections)
                {
                    WatchtowerWallJoins.GetCutEdge(direction, out var a, out var b);
                    body.Line(a, b, .06f, outline);
                }
            }
            Floor();
            Railings();
            if (!rearLadder)
                Ladder();
            return (body.Build("Total Fog tower body"), front.Build("Total Fog tower foreground"));
        }

        // Elevation projects towards map north in every rotation. Rotating the
        // sprite as one flat image would rotate the apparent gravity as well.
        private Vector3 Point(float x, float z, float elevation)
        {
            var p = rotation.AsInt switch
            {
                0 => new Vector3(-x, 0, -z),
                1 => new Vector3(-z, 0, x),
                2 => new Vector3(x, 0, z),
                _ => new Vector3(z, 0, -x),
            };
            p.z += elevation * PlatformProjection;
            return p;
        }

        private Color Shade(float brightness) =>
            new(
                Mathf.Clamp01(stuff.r * brightness),
                Mathf.Clamp01(stuff.g * brightness),
                Mathf.Clamp01(stuff.b * brightness)
            );

        private void Supports()
        {
            // Back faces first. The lowered deck leaves a thin rear face
            // visible; the 3x3 base still meets perimeter walls.
            for (int pass = 0; pass < 3; pass++)
            for (int side = 0; side < 4; side++)
            {
                float depth = SidePoint(side, 0, 0).z;
                int facePass =
                    depth > 0 ? 0
                    : depth < 0 ? 2
                    : 1;
                if (facePass != pass)
                    continue;
                body.Polygon(
                    outline,
                    SidePoint(side, -1, 0),
                    SidePoint(side, 1, 0),
                    SidePoint(side, 1, 1),
                    SidePoint(side, -1, 1)
                );
                body.Polygon(
                    Shade(stone ? (depth < 0 ? .72f : .86f) : .69f),
                    SidePoint(side, -.94f, .05f),
                    SidePoint(side, .94f, .05f),
                    SidePoint(side, .91f, .95f),
                    SidePoint(side, -.91f, .95f)
                );
                if (stone)
                {
                    for (int row = 1; row <= 2; row++)
                    {
                        float h = row / 3f;
                        body.Line(
                            SidePoint(side, -.95f, h),
                            SidePoint(side, .95f, h),
                            .035f,
                            Shade(.56f)
                        );
                    }
                    for (int row = 0; row < 3; row++)
                    {
                        float x = row % 2 == 0 ? -.22f : .22f;
                        body.Line(
                            SidePoint(side, x, row / 3f + .05f),
                            SidePoint(side, x, (row + 1) / 3f - .05f),
                            .035f,
                            Shade(.56f)
                        );
                    }
                }
                else
                {
                    Beam(
                        body,
                        SidePoint(side, -.86f, .08f),
                        SidePoint(side, .86f, .90f),
                        .12f,
                        Shade(.78f)
                    );
                    Beam(
                        body,
                        SidePoint(side, .86f, .08f),
                        SidePoint(side, -.86f, .90f),
                        .12f,
                        Shade(.82f)
                    );
                    foreach (float corner in new[] { -1f, 1f })
                        Beam(
                            body,
                            SidePoint(side, corner, 0),
                            SidePoint(side, corner, 1),
                            .14f,
                            Shade(.78f)
                        );
                }
            }
        }

        private Vector3 SidePoint(int side, float along, float height)
        {
            float extent = Mathf.Lerp(BaseHalfExtent, DeckHalfExtent, Mathf.Clamp01(height));
            return EdgePoint(side, along * extent, extent, height);
        }

        private Vector3 EdgePoint(int side, float along, float extent, float height) =>
            side switch
            {
                0 => Point(along, -extent, height),
                1 => Point(extent, along, height),
                2 => Point(along, extent, height),
                _ => Point(-extent, along, height),
            };

        private Vector3 RailPoint(int side, float along, float height) =>
            LowerDeck(EdgePoint(side, along * RailHalfExtent, RailHalfExtent, height));

        // Move only the upper artwork towards the viewer, in map space for
        // every rotation. The pawn projection and wall connections stay fixed.
        private static Vector3 LowerDeck(Vector3 point, float progress = 1)
        {
            point.z -= DeckShiftTowardViewer * progress;
            return point;
        }

        private void Floor()
        {
            const float edge = DeckHalfExtent;
            body.Polygon(
                outline,
                LowerDeck(Point(-edge, -edge, 1)),
                LowerDeck(Point(-edge, edge, 1)),
                LowerDeck(Point(edge, edge, 1)),
                LowerDeck(Point(edge, -edge, 1))
            );
            const float inside = DeckHalfExtent - .07f;
            body.Polygon(
                Shade(1.12f),
                LowerDeck(Point(-inside, -inside, 1)),
                LowerDeck(Point(-inside, inside, 1)),
                LowerDeck(Point(inside, inside, 1)),
                LowerDeck(Point(inside, -inside, 1))
            );
            if (stone)
            {
                body.Line(
                    LowerDeck(Point(-inside, 0, 1)),
                    LowerDeck(Point(inside, 0, 1)),
                    .035f,
                    Shade(.80f)
                );
                body.Line(
                    LowerDeck(Point(0, -inside, 1)),
                    LowerDeck(Point(0, inside, 1)),
                    .035f,
                    Shade(.80f)
                );
            }
            else
            {
                foreach (float x in new[] { -.21f, .21f })
                {
                    body.Line(
                        LowerDeck(Point(x, -inside, 1)),
                        LowerDeck(Point(x, inside, 1)),
                        .035f,
                        Shade(.70f)
                    );
                }
            }
            for (int side = 0; side < 4; side++)
                Beam(
                    body,
                    LowerDeck(SidePoint(side, -.98f, 1)),
                    LowerDeck(SidePoint(side, .98f, 1)),
                    .11f,
                    Shade(.87f)
                );
        }

        private void Railings()
        {
            for (int side = 0; side < 4; side++)
            {
                // The ladder is centered on local south. Leave a real gap.
                if (side == 0)
                {
                    Rail(side, -.97f, -.48f);
                    Rail(side, .48f, .97f);
                }
                else
                    Rail(side, -.97f, .97f);
            }
        }

        private void Rail(int side, float from, float to)
        {
            var target = SidePoint(side, 0, 0).z < 0 ? front : body;
            var railColor = Shade(stone ? 1.04f : .98f);
            Beam(
                target,
                RailPoint(side, from, 1.28f),
                RailPoint(side, to, 1.28f),
                stone ? .13f : .10f,
                railColor
            );
            if (stone)
                Beam(
                    target,
                    RailPoint(side, from, 1.12f),
                    RailPoint(side, to, 1.12f),
                    .11f,
                    Shade(.88f)
                );
            else
                Beam(
                    target,
                    RailPoint(side, from, 1.12f),
                    RailPoint(side, to, 1.12f),
                    .065f,
                    Shade(.88f)
                );
            foreach (float x in new[] { from, to })
                Beam(
                    target,
                    RailPoint(side, x, 1),
                    RailPoint(side, x, 1.31f),
                    stone ? .13f : .10f,
                    Shade(1.06f)
                );
        }

        private void Ladder()
        {
            var target = Point(0, -1.65f, 0).z < 0 ? front : body;
            // Ladders remain timber even on a masonry tower in this first sketch.
            var color = stone ? new Color(.48f, .35f, .22f) : Shade(.86f);
            foreach (float x in new[] { -.27f, .27f })
                Beam(
                    target,
                    Point(x, -1.65f, 0),
                    LowerDeck(Point(x, -RailHalfExtent, 1.04f)),
                    .09f,
                    color
                );
            for (int rung = 0; rung < 6; rung++)
            {
                float t = (rung + .5f) / 6;
                float z = Mathf.Lerp(-1.65f, -RailHalfExtent, t);
                Beam(
                    target,
                    LowerDeck(Point(-.28f, z, t * 1.04f), t),
                    LowerDeck(Point(.28f, z, t * 1.04f), t),
                    .08f,
                    color * 1.12f
                );
            }
        }

        private void Beam(Geometry geometry, Vector3 a, Vector3 b, float width, Color color)
        {
            geometry.Line(a, b, width + .085f, outline);
            geometry.Line(a, b, width, color);
        }
    }

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Color> colors = new();
        private readonly List<int> triangles = new();
        private float depth;

        internal float ReserveDepth()
        {
            float reserved = depth + .01f;
            depth += .02f;
            return reserved;
        }

        internal void Polygon(Color color, params Vector3[] points)
        {
            int start = vertices.Count;
            depth += .000015f;
            foreach (var point in points)
            {
                vertices.Add(new Vector3(point.x, depth, point.z));
                colors.Add(color);
            }
            bool reverse = Vector3.Cross(points[1] - points[0], points[2] - points[0]).y < 0;
            for (int i = 1; i < points.Length - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + (reverse ? i + 1 : i));
                triangles.Add(start + (reverse ? i : i + 1));
            }
        }

        internal void Line(Vector3 a, Vector3 b, float width, Color color)
        {
            var delta = b - a;
            var normal = new Vector3(-delta.z, 0, delta.x).normalized * width / 2;
            Polygon(color, a - normal, a + normal, b + normal, b - normal);
        }

        internal Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
