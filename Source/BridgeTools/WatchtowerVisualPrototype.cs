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
    internal const float PlatformProjection = .85f;
    private Mesh body;
    private Mesh foreground;
    private Material material;

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        material = SolidColorMaterials.SimpleSolidColorMaterial(Color.white, true);
        var drawing = new Drawing(
            Rotation,
            Stuff.stuffProps.color,
            Stuff.stuffProps.categories.Contains(StuffCategoryDefOf.Stony)
        );
        (body, foreground) = drawing.Build();
    }

    public override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        drawLoc.y = AltitudeLayer.BuildingOnTop.AltitudeFor();
        Graphics.DrawMesh(body, drawLoc, Quaternion.identity, material, 0);
        drawLoc.y = AltitudeLayer.PawnState.AltitudeFor();
        Graphics.DrawMesh(foreground, drawLoc, Quaternion.identity, material, 0);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        base.DeSpawn(mode);
        UnityEngine.Object.Destroy(body);
        UnityEngine.Object.Destroy(foreground);
        body = foreground = null;
    }

    private sealed class Drawing
    {
        private readonly Rot4 rotation;
        private readonly Color stuff;
        private readonly bool stone;
        private readonly Geometry body = new();
        private readonly Geometry front = new();
        private readonly Color outline = new(.16f, .14f, .12f);

        internal Drawing(Rot4 rotation, Color stuff, bool stone)
        {
            this.rotation = rotation;
            this.stuff = stuff;
            this.stone = stone;
        }

        internal (Mesh, Mesh) Build()
        {
            Supports();
            Floor();
            Railings();
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
            // Closed timber faces also read as part of a perimeter barrier.
            // The footprint/pathfinding decision remains a separate prototype.
            if (!stone)
            {
                for (int side = 0; side < 4; side++)
                {
                    if (SidePoint(side, 0, 0).z > 0)
                        continue;
                    body.Polygon(
                        outline,
                        SidePoint(side, -1.50f, 0),
                        SidePoint(side, 1.50f, 0),
                        SidePoint(side, 1.30f, 1),
                        SidePoint(side, -1.30f, 1)
                    );
                    body.Polygon(
                        Shade(.69f),
                        SidePoint(side, -1.44f, .05f),
                        SidePoint(side, 1.44f, .05f),
                        SidePoint(side, 1.25f, .95f),
                        SidePoint(side, -1.25f, .95f)
                    );
                    for (int plank = -4; plank <= 4; plank++)
                    {
                        float t = plank / 5f;
                        body.Line(
                            SidePoint(side, 1.44f * t, .05f),
                            SidePoint(side, 1.25f * t, .95f),
                            .026f,
                            Shade(.47f)
                        );
                    }
                }
            }
            if (stone)
            {
                for (int side = 0; side < 4; side++)
                {
                    var a = SidePoint(side, -1.50f, 0);
                    var b = SidePoint(side, 1.50f, 0);
                    var c = SidePoint(side, 1.30f, 1);
                    var d = SidePoint(side, -1.30f, 1);
                    if ((a.z + b.z) / 2 > 0)
                        continue;
                    body.Polygon(outline, a, b, c, d);
                    body.Polygon(
                        Shade(side % 2 == 0 ? .72f : .86f),
                        a + new Vector3(.035f, 0, .04f),
                        b + new Vector3(-.035f, 0, .04f),
                        c + new Vector3(-.035f, 0, -.04f),
                        d + new Vector3(.035f, 0, -.04f)
                    );
                    for (int row = 1; row <= 3; row++)
                    {
                        float h = row / 4f;
                        body.Line(
                            SidePoint(side, -1.22f, h),
                            SidePoint(side, 1.22f, h),
                            .035f,
                            Shade(.56f)
                        );
                        for (int joint = -2; joint <= 2; joint++)
                        {
                            float x = joint * .52f + (row % 2 == 0 ? .26f : 0);
                            body.Line(
                                SidePoint(side, x, h - .24f),
                                SidePoint(side, x, h),
                                .025f,
                                Shade(.56f)
                            );
                        }
                    }
                }
            }
            else
            {
                // Rear posts first, then front braces. The floor covers their tops.
                foreach (float z in new[] { 1.40f, -1.40f })
                foreach (float x in new[] { -1.40f, 1.40f })
                    Beam(body, Point(x, z, 0), Point(x, z, 1), .20f, Shade(.70f));
                for (int side = 0; side < 4; side++)
                {
                    var a = SidePoint(side, -1.12f, .08f);
                    var b = SidePoint(side, 1.12f, .88f);
                    if (SidePoint(side, 0, 0).z > 0)
                        continue;
                    Beam(body, a, b, .12f, Shade(.78f));
                    Beam(
                        body,
                        SidePoint(side, 1.12f, .08f),
                        SidePoint(side, -1.12f, .88f),
                        .12f,
                        Shade(.82f)
                    );
                }
            }
        }

        private Vector3 SidePoint(int side, float along, float height) =>
            side switch
            {
                0 => Point(along, -1.45f, height),
                1 => Point(1.45f, along, height),
                2 => Point(along, 1.45f, height),
                _ => Point(-1.45f, along, height),
            };

        private void Floor()
        {
            const float edge = 1.50f;
            body.Polygon(
                outline,
                Point(-edge, -edge, 1),
                Point(-edge, edge, 1),
                Point(edge, edge, 1),
                Point(edge, -edge, 1)
            );
            const float inside = 1.42f;
            body.Polygon(
                Shade(1.12f),
                Point(-inside, -inside, 1),
                Point(-inside, inside, 1),
                Point(inside, inside, 1),
                Point(inside, -inside, 1)
            );
            if (stone)
            {
                for (int row = -2; row <= 2; row++)
                {
                    float z = row * .44f;
                    body.Line(Point(-inside, z, 1), Point(inside, z, 1), .025f, Shade(.84f));
                    for (int col = -2; col <= 2; col++)
                    {
                        float x = col * .55f + (row % 2 == 0 ? .25f : 0);
                        body.Line(Point(x, z, 1), Point(x, z + .44f, 1), .023f, Shade(.84f));
                    }
                }
            }
            else
            {
                for (int plank = -4; plank <= 4; plank++)
                {
                    float x = plank * .245f;
                    body.Line(Point(x, -inside, 1), Point(x, inside, 1), .023f, Shade(.75f));
                    body.Line(
                        Point(x + .09f, -.84f + plank * .11f, 1),
                        Point(x + .09f, .26f + plank * .11f, 1),
                        .012f,
                        Shade(.96f)
                    );
                }
            }
            for (int side = 0; side < 4; side++)
                Beam(
                    body,
                    SidePoint(side, -1.49f, 1),
                    SidePoint(side, 1.49f, 1),
                    .13f,
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
                    Rail(side, -1.47f, -.37f);
                    Rail(side, .37f, 1.47f);
                }
                else
                    Rail(side, -1.47f, 1.47f);
            }
        }

        private void Rail(int side, float from, float to)
        {
            var target = SidePoint(side, 0, 0).z < 0 ? front : body;
            var railColor = Shade(stone ? 1.04f : .98f);
            Beam(
                target,
                SidePoint(side, from, 1.46f),
                SidePoint(side, to, 1.46f),
                stone ? .16f : .11f,
                railColor
            );
            if (stone)
                Beam(
                    target,
                    SidePoint(side, from, 1.19f),
                    SidePoint(side, to, 1.19f),
                    .17f,
                    Shade(.88f)
                );
            else
                Beam(
                    target,
                    SidePoint(side, from, 1.20f),
                    SidePoint(side, to, 1.20f),
                    .07f,
                    Shade(.88f)
                );
            foreach (float x in new[] { from, (from + to) / 2, to })
                Beam(
                    target,
                    SidePoint(side, x, 1),
                    SidePoint(side, x, 1.49f),
                    stone ? .17f : .12f,
                    Shade(1.06f)
                );
        }

        private void Ladder()
        {
            var target = Point(0, -1.45f, 0).z < 0 ? front : body;
            // Ladders remain timber even on a masonry tower in this first sketch.
            var color = stone ? new Color(.48f, .35f, .22f) : Shade(.86f);
            foreach (float x in new[] { -.27f, .27f })
                Beam(target, Point(x, -1.65f, 0), Point(x, -1.45f, 1.12f), .08f, color);
            for (int rung = 0; rung < 8; rung++)
            {
                float t = (rung + .5f) / 8;
                float z = Mathf.Lerp(-1.65f, -1.45f, t);
                Beam(
                    target,
                    Point(-.28f, z, t * 1.12f),
                    Point(.28f, z, t * 1.12f),
                    .065f,
                    color * 1.12f
                );
            }
        }

        private void Beam(Geometry geometry, Vector3 a, Vector3 b, float width, Color color)
        {
            geometry.Line(a, b, width + .065f, outline);
            geometry.Line(a, b, width, color);
        }
    }

    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<Color> colors = new();
        private readonly List<int> triangles = new();
        private int polygons;

        internal void Polygon(Color color, params Vector3[] points)
        {
            int start = vertices.Count;
            float depth = ++polygons * .000015f;
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
