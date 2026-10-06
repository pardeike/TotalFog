using System.Collections.Generic;
using RimWorld;
using Unity.Collections;
using Verse;
namespace TotalFog.Presentation;

internal static class DynamicVisibility
{
    // The vanilla cull job has completed. These flags are subsequently shared by
    // EnsureInitialized, ParallelPreDraw, Draw and shadow rendering for this frame.
    // Static objects keep native rendering once observed. Mobile entities need
    // current sight. Preserve the existing observer exception for player pawns/flyers.
    public static void ComputeCulledThings_Postfix(NativeArray<DynamicDrawManager.ThingCullDetails> details, List<Thing> ___drawThings)
    {
        for (int i = 0; i < details.Length; i++)
        {
            var entry = details[i];
            if (!entry.shouldDraw && !entry.shouldDrawShadow) continue;
            var thing = ___drawThings[i];
            // Walking pawns already have an interpolated cull cell. Flyers'
            // DrawPos getter updates their grid position, so request it only
            // here, outside the engine's thing-grid/section iteration.
            bool visible = CustomRenderVisibility.TryQuery(thing, out bool customVisible) ? customVisible :
                ThingVisibility.IsVisible(thing, allowMemory: true,
                    renderedCell: thing is Pawn ? entry.cell :
                        thing is PawnFlyer flyer ? flyer.DrawPos.ToIntVec3() : null);
            if (visible) continue;
            entry.shouldDraw = false; entry.shouldDrawShadow = false;
            details[i] = entry;
        }
    }
}
