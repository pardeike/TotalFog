using HarmonyLib;
using Verse;

namespace TotalFog.Compatibility;

internal static partial class LegacySaveTypes
{
    internal static void Install(Harmony harmony)
    {
        var handler = new HarmonyMethod(typeof(LegacySaveTypes), nameof(ResolvePostfix));
        harmony.Patch(
            AccessTools.Method(
                typeof(BackCompatibility),
                nameof(BackCompatibility.GetBackCompatibleType)
            ),
            postfix: handler
        );
        harmony.Patch(
            AccessTools.Method(
                typeof(BackCompatibility),
                nameof(BackCompatibility.GetBackCompatibleTypeDirect)
            ),
            postfix: handler
        );
    }
}
