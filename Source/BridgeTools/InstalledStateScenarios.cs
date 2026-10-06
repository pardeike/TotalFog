using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using RimBridgeServer.Sdk;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class InstalledStateScenarios
{
    [Tool("totalfog/installed_state", Description = "Inspect the actual Total Fog assembly, mod instance and current-map fog component. Optional initializer recheck captures a cached startup failure, or runs the initializer if it has never executed. For isolated diagnostic games.")]
    public static object InstalledState(bool recheckStaticInitializer = false)
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "TotalFog");
        var starter = assembly?.GetType("TotalFog.TotalFogMod");
        var fog = Find.CurrentMap?.components
            .FirstOrDefault(c => c.GetType().FullName == "TotalFog.MapVisibility");
        object mod = null;
        var errors = new List<object>();
        bool? initializerSucceeded = null;
        if (starter != null)
        {
            var getter = typeof(LoadedModManager).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m => m.Name == "GetMod" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            mod = getter.MakeGenericMethod(starter).Invoke(null, null);
            if (recheckStaticInitializer)
            {
                try { RuntimeHelpers.RunClassConstructor(starter.TypeHandle); initializerSucceeded = true; }
                catch (Exception exception)
                {
                    initializerSucceeded = false;
                    for (var current = exception; current != null; current = current.InnerException)
                        errors.Add(new { type = current.GetType().FullName, current.Message });
                }
            }
        }
        return new
        {
            assemblyLoaded = assembly != null, assemblyLocation = assembly?.Location,
            starterTypePresent = starter != null, starterType = starter?.FullName, modInstancePresent = mod != null,
            fogType = fog?.GetType().FullName,
            originalNamespaceTypes = assembly?.GetTypes().Where(t => t.Namespace?.StartsWith("RimWorldRealFoW", StringComparison.Ordinal) == true)
                .Select(t => t.FullName).ToArray(),
            mapFogComponentPresent = fog != null,
            fogInitialized = fog?.GetType().GetProperty("Initialized")?.GetValue(fog),
            recheckStaticInitializer, initializerSucceeded, initializerErrors = errors
        };
    }
}
