// Modified by Andreas Pardeike for Total Fog, 2026-10-04: reference-assembly override compatibility.
using RimWorld;
using UnityEngine;
using Verse;

namespace TotalFog;

public class Mote_HearingCue : Mote
{
    public int ObserverFactionId { get; private set; }
    private float targetSize;

    private float velocity;

    public override bool EndOfLife => AgeSecs >= targetSize / velocity;

    public override float Alpha
    {
        get
        {
            _ = Mathf.Clamp01(AgeSecs * 10f);
            var num = 1f;
            var num2 = Mathf.Clamp01(1f - (AgeSecs / (targetSize / velocity)));
            return num * num2 * CalculatedIntensity();
        }
    }

    public void Initialize(Vector3 position, float size, float incomingVelocity) =>
        Initialize(position, size, incomingVelocity, Faction.OfPlayer);

    public void Initialize(
        Vector3 position,
        float size,
        float incomingVelocity,
        Faction observerFaction
    )
    {
        ObserverFactionId = observerFaction?.loadID ?? 0;
        exactPosition = position;
        targetSize = size;
        velocity = incomingVelocity;
        Scale = 0f;
    }

    public override void TimeInterval(float deltaTime)
    {
        base.TimeInterval(deltaTime);
        if (Destroyed)
        {
            return;
        }

        var scale = AgeSecs * velocity;
        Scale = scale;
    }

    private float CalculatedIntensity()
    {
        return Mathf.Sqrt(targetSize) / 10f;
    }

    public float CalculatedShockwaveSpan()
    {
        return Mathf.Min(Mathf.Sqrt(targetSize) * 0.8f, ExactScale.x) / ExactScale.x;
    }
}
