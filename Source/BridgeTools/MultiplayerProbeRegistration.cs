using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace TotalFog.BridgeTools;

/// <summary>Append opt-in companion commands without renumbering native saved commands.</summary>
internal static class MultiplayerProbeRegistration
{
    internal static object Register(MethodInfo method, out Func<object, object[], bool> submit)
    {
        if (Current.Game != null)
            throw new InvalidOperationException(
                "Register at the main menu before loading/hosting."
            );
        var api =
            AccessTools.TypeByName("Multiplayer.API.MP")
            ?? throw new InvalidOperationException("Load Multiplayer first.");
        var registry = AccessTools.TypeByName("Multiplayer.Client.Sync");
        var handlers = (IList)AccessTools.Field(registry, "handlers").GetValue(null);
        var previousIds = handlers.Cast<object>().Select(Id).ToArray();
        var register = api.GetMethods()
            .Single(m =>
                m.Name == "RegisterSyncMethod"
                && m.GetParameters().Length == 2
                && m.GetParameters()[0].ParameterType == typeof(MethodInfo)
            );
        var handler = register.Invoke(null, new object[] { method, null });
        // Companion discovery is later than ordinary mod registration. Native
        // finalization sorts by version, preserving prior IDs when we append.
        AccessTools.Field(handler.GetType(), "version").SetValue(handler, int.MaxValue);
        AccessTools.Method(registry, "PostInitHandlers").Invoke(null, null);
        if (!previousIds.SequenceEqual(handlers.Cast<object>().Take(previousIds.Length).Select(Id)))
            throw new InvalidOperationException(
                "Native registration changed existing command IDs; stop this process."
            );
        submit =
            (Func<object, object[], bool>)
                Delegate.CreateDelegate(
                    typeof(Func<object, object[], bool>),
                    handler,
                    handler.GetType().GetMethod("DoSync")
                );
        return handler;
    }

    internal static int Id(object handler) =>
        (int)AccessTools.Field(handler.GetType(), "syncId").GetValue(handler);
}
