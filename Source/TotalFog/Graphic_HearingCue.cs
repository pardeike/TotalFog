// Modified by Andreas Pardeike for Total Fog, 2026-10-04: reference-assembly override compatibility.
using UnityEngine;
using Verse;

namespace TotalFog;

public class Graphic_HearingCue : Graphic_Mote
{
    public override bool ForcePropertyBlock => true;

    public override void DrawWorker(Vector3 loc, Rot4 rot, ThingDef thingDef, Thing thing, float extraRotation)
    {
        var moteSoundWave = (Mote_HearingCue)thing;
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