using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using RimBridgeServer.Sdk;

namespace TotalFog.BridgeTools;

/// <summary>Bounded diagnostics that observe failed resolution without supplying an assembly.</summary>
public sealed class AssemblyBindingScenarios
{
    private static readonly object gate = new();
    private static readonly List<object> resolutions = new();
    private static string prefix;
    private static int attempts;

    [Tool(
        "totalfog/assembly_binding_trace",
        Description = "Start/status/stop a bounded reflection-only assembly resolution trace, defaulting to UnityEngine.InputLegacyModule. The handler returns null and never resolves or changes an assembly. Stop after the loading reproduction."
    )]
    public static Task<object> Trace(
        IRimBridgeContext context,
        string action = "status",
        string assemblyPrefix = "UnityEngine.InputLegacyModule"
    ) =>
        context.MainThread.InvokeAsync<object>(() =>
        {
            lock (gate)
            {
                if (action == "start")
                {
                    if (prefix != null)
                        return new { success = false, message = "Stop the active trace first." };
                    if (string.IsNullOrWhiteSpace(assemblyPrefix))
                        return new
                        {
                            success = false,
                            message = "An assembly name prefix is required.",
                        };
                    resolutions.Clear();
                    attempts = 0;
                    prefix = assemblyPrefix;
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += Record;
                }
                else if (action == "stop")
                {
                    AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= Record;
                    prefix = null;
                }
                else if (action != "status")
                    return new { success = false, message = "Use start, status or stop." };
                return new
                {
                    success = true,
                    active = prefix != null,
                    attempts,
                    truncated = attempts > 8,
                    resolutions = resolutions.ToArray(),
                };
            }
        });

    private static Assembly Record(object sender, ResolveEventArgs args)
    {
        lock (gate)
        {
            if (prefix == null || !args.Name.StartsWith(prefix, StringComparison.Ordinal))
                return null;
            attempts++;
            if (resolutions.Count < 8)
                resolutions.Add(
                    new
                    {
                        assembly = args.Name,
                        requestingAssembly = args.RequestingAssembly?.FullName,
                        stack = Environment.StackTrace,
                    }
                );
        }
        return null;
    }
}
