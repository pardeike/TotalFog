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
public class WatchtowerVisualPrototype : Building
{
    internal const float PlatformProjection = 1.20f;

    // The native pawn draw origin is inside its body sprite, not at its feet.
    // This artwork fit puts the body base over the lowered deck's standing point.
    internal const float OccupantDrawProjection = PlatformProjection + .45f;
    private Mesh body;
    private Mesh foreground;
    private Material material;
    private WatchtowerWallJoins wallJoins;
    private WatchtowerWallJoins nativeFront;
    internal Thing NativeFrontSource { get; set; }
    internal bool SilhouetteOutlines { get; set; }

    internal int WallJoinCount => wallJoins?.Directions.Count ?? 0;
    internal int CachedDrawCalls => 2 + (wallJoins?.DrawCalls ?? 0) + (nativeFront?.DrawCalls ?? 0);
    internal bool HasNativeFront => nativeFront?.HasGeometry ?? false;
    internal float NativeFrontMappingError => nativeFront?.MappingError ?? 0;

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
        WatchtowerWallJoins nextFront = null;
        Mesh nextBody = null;
        Mesh nextForeground = null;
        try
        {
            var drawing = new Drawing(
                Rotation,
                Stuff.stuffProps.color,
                Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony),
                next.Directions,
                NativeFrontSource != null,
                SilhouetteOutlines
            );
            (nextBody, nextForeground) = drawing.Build();
            if (NativeFrontSource != null)
            {
                nextFront = new WatchtowerWallJoins(this, NativeFrontSource, drawing.FrontFace);
                nextFront.Complete(
                    AltitudeLayer.BuildingOnTop.AltitudeFor() + drawing.FrontFaceDepth
                );
            }
            next.Complete(AltitudeLayer.BuildingOnTop.AltitudeFor() + drawing.WallJoinDepth);
            UnityEngine.Object.Destroy(body);
            UnityEngine.Object.Destroy(foreground);
            wallJoins?.Dispose();
            nativeFront?.Dispose();
            body = nextBody;
            foreground = nextForeground;
            wallJoins = next;
            nativeFront = nextFront;
        }
        catch
        {
            UnityEngine.Object.Destroy(nextBody);
            UnityEngine.Object.Destroy(nextForeground);
            next.Dispose();
            nextFront?.Dispose();
            throw;
        }
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        drawLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        Graphics.DrawMesh(body, drawLoc, Quaternion.identity, material, 0);
        nativeFront?.Draw();
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
        nativeFront?.Dispose();
        nativeFront = null;
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
        private readonly bool nativeFront;
        private readonly bool silhouetteOutlines;
        private readonly Geometry body = new();
        private readonly Geometry front = new();
        private readonly Color outline = new(.10f, .095f, .085f);

        internal float WallJoinDepth { get; private set; }
        internal float FrontFaceDepth { get; private set; }
        internal Vector3[] FrontFace { get; private set; }

        internal Drawing(
            Rot4 rotation,
            Color stuff,
            bool stone,
            IReadOnlyList<IntVec3> wallDirections,
            bool nativeFront = false,
            bool silhouetteOutlines = false
        )
        {
            this.rotation = rotation;
            this.stuff = stuff;
            this.stone = stone;
            this.wallDirections = wallDirections;
            this.nativeFront = nativeFront;
            this.silhouetteOutlines = silhouetteOutlines;
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
                    body.Line(
                        a,
                        b,
                        silhouetteOutlines ? .035f : .06f,
                        silhouetteOutlines ? Shade(.58f) : outline
                    );
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
                var faceColor = Shade(stone ? (depth < 0 ? .72f : .86f) : .69f);
                // The reference buildings use broad shaded faces and soft
                // bevels, with the darkest stroke reserved for the silhouette.
                float faceBrightness =
                    depth < 0 ? 1.03f
                    : depth > 0 ? .77f
                    : SidePoint(side, 0, 0).x < 0 ? .92f
                    : .84f;
                var face = new[]
                {
                    SidePoint(side, -1, 0),
                    SidePoint(side, 1, 0),
                    SidePoint(side, 1, 1),
                    SidePoint(side, -1, 1),
                };
                if (silhouetteOutlines)
                {
                    body.GradientQuad(
                        Shade(faceBrightness * .83f),
                        Shade(faceBrightness),
                        face[0],
                        face[1],
                        face[2],
                        face[3]
                    );
                    for (int edge = 0; edge < face.Length; edge++)
                        body.Line(
                            face[edge],
                            face[(edge + 1) % face.Length],
                            .035f,
                            Shade(faceBrightness * .76f)
                        );
                }
                else
                {
                    body.Polygon(outline, face);
                    body.Polygon(
                        faceColor,
                        SidePoint(side, -.94f, .05f),
                        SidePoint(side, .94f, .05f),
                        SidePoint(side, .91f, .95f),
                        SidePoint(side, -.91f, .95f)
                    );
                }
                bool nativeFace = nativeFront && depth < 0;
                if (nativeFace)
                {
                    FrontFace = new[]
                    {
                        SidePoint(side, -.94f, .05f),
                        SidePoint(side, .94f, .05f),
                        SidePoint(side, .91f, .95f),
                        SidePoint(side, -.91f, .95f),
                    };
                    FrontFaceDepth = body.ReserveDepth();
                }
                if (stone && !nativeFace)
                {
                    for (int row = 1; row <= 2; row++)
                    {
                        float h = row / 3f;
                        body.Line(
                            SidePoint(side, -.95f, h),
                            SidePoint(side, .95f, h),
                            silhouetteOutlines ? .028f : .035f,
                            Shade(silhouetteOutlines ? faceBrightness * .81f : .56f)
                        );
                    }
                    for (int row = 0; row < 3; row++)
                    {
                        float x = row % 2 == 0 ? -.22f : .22f;
                        body.Line(
                            SidePoint(side, x, row / 3f + .05f),
                            SidePoint(side, x, (row + 1) / 3f - .05f),
                            silhouetteOutlines ? .028f : .035f,
                            Shade(silhouetteOutlines ? faceBrightness * .81f : .56f)
                        );
                    }
                }
                else if (!stone)
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
            if (silhouetteOutlines)
            {
                // Union of the four projected support faces. Internal face
                // boundaries stay thin; the heavy contour follows the outside.
                var contour = new[]
                {
                    new Vector3(-BaseHalfExtent, 0, -BaseHalfExtent),
                    new Vector3(BaseHalfExtent, 0, -BaseHalfExtent),
                    new Vector3(BaseHalfExtent, 0, BaseHalfExtent),
                    new Vector3(DeckHalfExtent, 0, DeckHalfExtent + PlatformProjection),
                    new Vector3(-DeckHalfExtent, 0, DeckHalfExtent + PlatformProjection),
                    new Vector3(-BaseHalfExtent, 0, BaseHalfExtent),
                };
                for (int edge = 0; edge < contour.Length; edge++)
                    Boundary(contour[edge], contour[(edge + 1) % contour.Length]);
            }
        }

        private void Boundary(Vector3 a, Vector3 b)
        {
            foreach (var direction in wallDirections)
            {
                bool side = direction.x != 0;
                if (
                    side
                        ? direction.x * a.x <= 0 || direction.x * b.x <= 0
                        : direction.z * a.z <= 0 || direction.z * b.z <= 0
                )
                    continue;
                float start = side ? a.z : a.x;
                float end = side ? b.z : b.x;
                float span = end - start;
                if (Mathf.Abs(span) < .0001f)
                    continue;
                float t0 = (-.5f - start) / span;
                float t1 = (.5f - start) / span;
                float from = Mathf.Max(0, Mathf.Min(t0, t1));
                float to = Mathf.Min(1, Mathf.Max(t0, t1));
                if (from >= to)
                    continue;
                var joinA = Vector3.Lerp(a, b, from);
                var joinB = Vector3.Lerp(a, b, to);
                if (from > 0)
                    body.Line(a, joinA, .07f, outline);
                body.Line(joinA, joinB, .035f, Shade(.58f));
                if (to < 1)
                    body.Line(joinB, b, .07f, outline);
                return;
            }
            body.Line(a, b, .07f, outline);
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
                silhouetteOutlines ? Shade(.76f) : outline,
                LowerDeck(Point(-edge, -edge, 1)),
                LowerDeck(Point(-edge, edge, 1)),
                LowerDeck(Point(edge, edge, 1)),
                LowerDeck(Point(edge, -edge, 1))
            );
            float inside = DeckHalfExtent - (silhouetteOutlines ? .035f : .07f);
            if (silhouetteOutlines)
                // Lighting stays fixed in map space when the ladder rotates.
                body.GradientQuad(
                    Shade(1.03f),
                    Shade(1.15f),
                    LowerDeck(new Vector3(-inside, 0, PlatformProjection - inside)),
                    LowerDeck(new Vector3(inside, 0, PlatformProjection - inside)),
                    LowerDeck(new Vector3(inside, 0, PlatformProjection + inside)),
                    LowerDeck(new Vector3(-inside, 0, PlatformProjection + inside))
                );
            else
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
                railColor,
                silhouette: SidePoint(side, 0, 0).z > 0
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
            // A climber is in front of the rungs. The platform railing remains
            // in the foreground mesh; the ladder belongs below the native pawn.
            var target = body;
            // Ladders remain timber even on a masonry tower in this first sketch.
            var color = stone ? new Color(.48f, .35f, .22f) : Shade(.86f);
            foreach (float x in new[] { -.27f, .27f })
                Beam(
                    target,
                    Point(x, -1.65f, 0),
                    LowerDeck(Point(x, -RailHalfExtent, 1.04f)),
                    .09f,
                    color,
                    silhouette: true
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

        private void Beam(
            Geometry geometry,
            Vector3 a,
            Vector3 b,
            float width,
            Color color,
            bool silhouette = false
        )
        {
            geometry.Line(
                a,
                b,
                width + (silhouetteOutlines && !silhouette ? .035f : .085f),
                silhouetteOutlines && !silhouette ? Color.Lerp(outline, color, .55f) : outline
            );
            geometry.Line(a, b, width, color);
            if (silhouetteOutlines)
            {
                var delta = b - a;
                var lightEdge = new Vector3(-delta.z, 0, delta.x).normalized;
                if (lightEdge.z < 0 || (Mathf.Abs(lightEdge.z) < .0001f && lightEdge.x > 0))
                    lightEdge = -lightEdge;
                var shift = lightEdge * width * .28f;
                geometry.Line(
                    a + shift,
                    b + shift,
                    width * .20f,
                    Color.Lerp(color, Color.white, .14f)
                );
            }
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

        internal void GradientQuad(
            Color lower,
            Color upper,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d
        )
        {
            Polygon(lower, a, b, c, d);
            colors[colors.Count - 2] = upper;
            colors[colors.Count - 1] = upper;
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
