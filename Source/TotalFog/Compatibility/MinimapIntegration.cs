using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace TotalFog.Compatibility;

internal static class MinimapIntegration
{
    private static FieldInfo dirtyCells;

    internal static void Install(Harmony harmony)
    {
        if (!ModsConfig.IsActive("dubwise.dubsmintminimap"))
            return;
        var type = AccessTools.TypeByName("DubsMintMinimap.MainTabWindow_MiniMap");
        if (type == null)
        {
            Log.Warning("[Total Fog] Minimap integration: expected type is unavailable.");
            return;
        }
        dirtyCells = AccessTools.Field(type, "dirtyGirls");
        int patched = 0;
        foreach (var method in AccessTools.GetDeclaredMethods(type))
        {
            if (method.Name != "Fogged" || method.ReturnType != typeof(bool))
                continue;
            var parameters = method.GetParameters();
            string handler =
                parameters.Length == 1 && parameters[0].ParameterType == typeof(Thing)
                    ? nameof(ThingPostfix)
                : parameters.Length == 2
                && parameters[0].ParameterType == typeof(IntVec3)
                && parameters[1].ParameterType == typeof(Map)
                    ? nameof(CellPostfix)
                : null;
            if (handler == null)
                continue;
            try
            {
                harmony.Patch(
                    method,
                    postfix: new HarmonyMethod(typeof(MinimapIntegration), handler)
                );
                patched++;
            }
            catch (Exception exception)
            {
                Log.Warning("[Total Fog] Minimap integration: " + exception.Message);
            }
        }
        if (patched == 0)
            Log.Warning(
                "[Total Fog] Minimap integration: no supported Fogged overloads were found."
            );
    }

    internal static void Reveal(int index)
    {
        if (
            dirtyCells?.IsStatic == true
            && dirtyCells.GetValue(null) is bool[] dirty
            && (uint)index < dirty.Length
        )
            dirty[index] = true;
    }

    public static void ThingPostfix(ref bool __result, Thing __0)
    {
        if (!__result && __0 != null)
            __result = !Presentation.ThingVisibility.IsVisible(__0);
    }

    public static void CellPostfix(ref bool __result, IntVec3 __0, Map __1)
    {
        if (__result || __1 == null || Presentation.ThingVisibility.Bypass(__1))
            return;
        __result =
            !__0.InBounds(__1) || !__1.GetVisibility().knownCells[__1.cellIndices.CellToIndex(__0)];
    }
}
