using UnityEngine;
using Verse;

namespace RimWorldRealFoW;

public class Graphic_MoteSoundWave : Graphic_Mote
{
    protected override bool ForcePropertyBlock => true;

    public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
    {
        var moteSoundWave = (MoteSoundWave)thing;
        var alpha = moteSoundWave.Alpha;
        if (alpha <= 0f)
        {
            return;
        }

        propertyBlock.SetColor(ShaderPropertyIDs.ShockwaveColor, new Color(1f, 0.5f, 1f, alpha));
        propertyBlock.SetFloat(ShaderPropertyIDs.ShockwaveSpan, moteSoundWave.CalculatedShockwaveSpan());
        DrawMoteInternal(loc, rot, thingDef, thing, 0);
    }

    public override string ToString()
    {
        return string.Concat("MoteSplash(path=", path, ", shader=", Shader, ", color=", color,
            ", colorTwo=unsupported)");
    }
}