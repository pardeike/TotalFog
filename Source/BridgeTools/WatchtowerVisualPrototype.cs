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
    private Material wallMaterial;
    private Rect wallTextureRect;
    private WatchtowerWallJoins wallJoins;

    internal int WallJoinCount => wallJoins?.Directions.Count ?? 0;
    internal int CachedDrawCalls => 3 + (wallJoins?.DrawCalls ?? 0);
    internal string WallPattern => wallMaterial.mainTexture.name;

    internal bool UsesWallPattern(Thing wall)
    {
        var native = wall.Graphic.MatSingleFor(wall);
        return wallMaterial.mainTexture == native.mainTexture
            && wallMaterial.shader == native.shader
            && wallMaterial.color == native.color;
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        material = SolidColorMaterials.SimpleSolidColorMaterial(Color.white, true);
        var wallGraphic = ThingDefOf.Wall.graphicData.GraphicColoredFor(this);
        wallMaterial = wallGraphic is Graphic_Linked linked
            ? linked.SubGraphic.MatSingleFor(this)
            : wallGraphic.MatSingleFor(this);
        var tile =
            wallGraphic is Graphic_Linked
                ? MaterialAtlasPool.SubMaterialFromAtlas(
                    wallMaterial,
                    LinkDirections.Left | LinkDirections.Right
                )
                : wallMaterial;
        // Use the visible face of a straight wall, excluding its flat top and
        // outline. Bake the native link rectangle into UVs on our cached mesh.
        var face =
            wallGraphic is Graphic_Linked ? new Rect(.08f, .04f, .84f, .23f) : new Rect(0, 0, 1, 1);
        wallTextureRect = new Rect(
            tile.mainTextureOffset + Vector2.Scale(face.position, tile.mainTextureScale),
            Vector2.Scale(face.size, tile.mainTextureScale)
        );
        var drawing = new Drawing(
            Rotation,
            Stuff.stuffProps.color,
            Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony),
            wallTextureRect,
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
                wallTextureRect,
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
        Graphics.DrawMesh(body, drawLoc, Quaternion.identity, wallMaterial, 0, null, 1);
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
        private readonly Rect wallTextureRect;
        private readonly IReadOnlyList<IntVec3> wallDirections;
        private readonly Geometry body = new();
        private readonly Geometry front = new();
        private readonly Color outline = new(.10f, .095f, .085f);

        internal float WallJoinDepth { get; private set; }

        internal Drawing(
            Rot4 rotation,
            Color stuff,
            bool stone,
            Rect wallTextureRect,
            IReadOnlyList<IntVec3> wallDirections
        )
        {
            this.rotation = rotation;
            this.stuff = stuff;
            this.stone = stone;
            this.wallTextureRect = wallTextureRect;
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
            return (
                body.Build("Total Fog tower body", wallTextureRect),
                front.Build("Total Fog tower foreground", wallTextureRect)
            );
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
                body.WallPattern(
                    stone ? (depth < 0 ? .72f : .86f) : .69f,
                    3,
                    2,
                    SidePoint(side, -.94f, .05f),
                    SidePoint(side, .94f, .05f),
                    SidePoint(side, .91f, .95f),
                    SidePoint(side, -.91f, .95f)
                );
                if (!stone)
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
            body.WallPattern(
                1.12f,
                2,
                2,
                LowerDeck(Point(-inside, -inside, 1)),
                LowerDeck(Point(-inside, inside, 1)),
                LowerDeck(Point(inside, inside, 1)),
                LowerDeck(Point(inside, -inside, 1))
            );
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
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();
        private readonly List<int> wallTriangles = new();
        private float depth;

        internal float ReserveDepth()
        {
            float reserved = depth + .01f;
            depth += .02f;
            return reserved;
        }

        internal void Polygon(Color color, params Vector3[] points) =>
            Polygon(color, false, points);

        // Repeat the wall face within its atlas rectangle. UVs greater
        // than one would spill into neighbouring link tiles instead of repeating.
        internal void WallPattern(float shade, int columns, int rows, params Vector3[] points)
        {
            var color = new Color(shade, shade, shade);
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                Polygon(
                    color,
                    true,
                    Sample(column / (float)columns, row / (float)rows),
                    Sample((column + 1f) / columns, row / (float)rows),
                    Sample((column + 1f) / columns, (row + 1f) / rows),
                    Sample(column / (float)columns, (row + 1f) / rows)
                );
            }

            Vector3 Sample(float x, float y) =>
                Vector3.Lerp(
                    Vector3.Lerp(points[0], points[1], x),
                    Vector3.Lerp(points[3], points[2], x),
                    y
                );
        }

        private void Polygon(Color color, bool wallPattern, params Vector3[] points)
        {
            int start = vertices.Count;
            depth += .000015f;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];
                vertices.Add(new Vector3(point.x, depth, point.z));
                colors.Add(color);
                uvs.Add(new Vector2(i is 1 or 2 ? 1 : 0, i >= 2 ? 1 : 0));
            }
            var target = wallPattern ? wallTriangles : triangles;
            bool reverse = Vector3.Cross(points[1] - points[0], points[2] - points[0]).y < 0;
            for (int i = 1; i < points.Length - 1; i++)
            {
                target.Add(start);
                target.Add(start + (reverse ? i + 1 : i));
                target.Add(start + (reverse ? i : i + 1));
            }
        }

        internal void Line(Vector3 a, Vector3 b, float width, Color color)
        {
            var delta = b - a;
            var normal = new Vector3(-delta.z, 0, delta.x).normalized * width / 2;
            Polygon(color, a - normal, a + normal, b + normal, b - normal);
        }

        internal Mesh Build(string name, Rect wallTextureRect)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            foreach (int index in new HashSet<int>(wallTriangles))
                uvs[index] =
                    wallTextureRect.position + Vector2.Scale(uvs[index], wallTextureRect.size);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = wallTriangles.Count > 0 ? 2 : 1;
            mesh.SetTriangles(triangles, 0);
            if (wallTriangles.Count > 0)
                mesh.SetTriangles(wallTriangles, 1);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
