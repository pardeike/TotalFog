using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace RimWorldRealFoW;

[HarmonyPatch]
internal static class InteractionBubblesPatch
{
    private static MethodBase target;

    [UsedImplicitly]
    private static bool Prepare()
    {
        var mod = LoadedModManager.RunningMods.FirstOrDefault(m => m.Name == "Interaction Bubbles");
        if (mod == null)
        {
            return false;
        }

        var type = mod.assemblies.loadedAssemblies
            .FirstOrDefault(a => a.GetName().Name == "Bubbles")?
            .GetType("Bubbles.Core.Bubbler");

        if (type == null)
        {
            RealFoWModStarter.LogMessage("Interaction bubble not installed. Ignore");

            return false;
        }

        target = AccessTools.DeclaredMethod(type, "DrawBubble");

        if (target != null)
        {
            return true;
        }

        Log.Warning("RFoW: Can't patch Interaction bubbles's Bubbler.DrawBubble");

        return false;
    }

    [UsedImplicitly]
    private static MethodBase TargetMethod()
    {
        return target;
    }
}