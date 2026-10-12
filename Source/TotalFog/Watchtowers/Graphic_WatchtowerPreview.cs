using UnityEngine;
using Verse;

namespace TotalFog.Watchtowers;

/// <summary>Blueprints and placement ghosts use the same ladder/deck geometry as the building.</summary>
public sealed class Graphic_WatchtowerPreview : Graphic_Single
{
    private readonly Mesh[] bodies = new Mesh[4];
    private readonly Mesh[] foregrounds = new Mesh[4];
    private Material material;

    public override Graphic GetColoredVersion(
        Shader newShader,
        Color newColor,
        Color newColorTwo
    ) =>
        GraphicDatabase.Get<Graphic_WatchtowerPreview>(
            path,
            newShader,
            drawSize,
            newColor,
            newColorTwo,
            data
        );

    public override void DrawWorker(
        Vector3 loc,
        Rot4 rot,
        ThingDef thingDef,
        Thing thing,
        float extraRotation
    )
    {
        int index = rot.AsInt;
        if (bodies[index] == null)
            (bodies[index], foregrounds[index]) = WatchtowerGraphic.BuildPreview(rot, color);
        material ??= SolidColorMaterials.SimpleSolidColorMaterial(Color.white, true);
        Graphics.DrawMesh(bodies[index], loc, Quaternion.identity, material, 0);
        Graphics.DrawMesh(foregrounds[index], loc, Quaternion.identity, material, 0);
    }
}
