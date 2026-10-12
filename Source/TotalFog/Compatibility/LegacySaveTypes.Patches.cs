using HarmonyLib;
using Verse;

namespace TotalFog.Compatibility;

internal static partial class LegacySaveTypes
{
    internal static void Install(Harmony harmony)
    {
        var handler = new HarmonyMethod(typeof(LegacySaveTypes), nameof(ResolvePostfix));
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(BackCompatibility),
                nameof(BackCompatibility.GetBackCompatibleType)
            ),
            postfix: handler
        );
        PatchSafety.Patch(
            harmony,
            AccessTools.Method(
                typeof(BackCompatibility),
                nameof(BackCompatibility.GetBackCompatibleTypeDirect)
            ),
            postfix: handler
        );
    }
}
