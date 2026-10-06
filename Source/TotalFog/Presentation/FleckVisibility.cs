using System.Threading;
using Verse;

namespace TotalFog.Presentation;

/// <summary>Current sight for particles, without changing their simulation or lifetime.</summary>
internal static class FleckVisibility
{
    // Flecks have no assigned owner map. The engine draws one map's manager
    // at a time and joins all particle workers before returning. Publish the
    // batch's owner once so static and worker-drawn thrown flecks share it.
    private static MapVisibility drawingFog;

    internal static void BeginDrawing(FleckManager __instance, out MapVisibility __state)
    {
        __state = Volatile.Read(ref drawingFog);
        Volatile.Write(ref drawingFog, __instance.parent.GetVisibility());
    }

    // A finalizer restores nested calls and also clears the owner on failure.
    // Returning void preserves the engine's original exception.
    internal static void EndDrawing(MapVisibility __state) =>
        Volatile.Write(ref drawingFog, __state);

    internal static bool DrawPrefix(ref FleckStatic __instance)
    {
        var fog = Volatile.Read(ref drawingFog);
        // Direct foreign drawing outside the map manager retains its behavior.
        // ExactPosition includes moving, attached and arcing draw offsets;
        // Draw's subsequent altitude assignment only changes the Y component.
        return fog == null
            || CellVisibility.IsCurrent(fog.map, __instance.DrawPos.ToIntVec3(), fog);
    }
}
