using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TotalFog.Core;
using Verse;

namespace TotalFog;

internal static class PatchSafety
{
    internal static bool Install(string feature, Action install) =>
        FeatureInstallation.Try(feature, install, Log.Warning);

    internal static bool Patch(
        Harmony harmony,
        MethodBase original,
        HarmonyMethod prefix = null,
        HarmonyMethod postfix = null,
        HarmonyMethod transpiler = null,
        HarmonyMethod finalizer = null
    )
    {
        return Install(
            original == null
                ? "missing patch target"
                : original.DeclaringType.FullName + "." + original.Name,
            () => harmony.Patch(original, prefix, postfix, transpiler, finalizer)
        );
    }

    internal static bool Matches(
        string feature,
        IEnumerable<CodeInstruction> body,
        MethodInfo target,
        int expected
    )
    {
        int count = target == null ? 0 : body.Count(code => code.Calls(target));
        if (target != null && count == expected)
            return true;
        Warn(
            feature,
            "unsupported instruction shape; expected " + expected + " calls, found " + count
        );
        return false;
    }

    internal static void Warn(string feature, string reason) =>
        FeatureInstallation.Warn(feature, reason, Log.Warning);
}
