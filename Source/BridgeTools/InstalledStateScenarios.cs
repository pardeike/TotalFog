using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimBridgeServer.Sdk;
using RimWorld;
using Verse;

namespace TotalFog.BridgeTools;

public sealed class InstalledStateScenarios
{
    private static readonly Harmony selectionTrace = new("brrainz.totalfog.bridge.selection-trace");
    private static object lastSelectionDrag;
    private static readonly List<object> selectionInput = new();
    private static bool hideConstructionSight;

    [Tool(
        "totalfog/installed_state",
        Description = "Inspect the actual Total Fog assembly, mod instance and current-map fog component. Optional initializer recheck captures a cached startup failure, or runs the initializer if it has never executed. For isolated diagnostic games."
    )]
    public static object InstalledState(
        bool recheckStaticInitializer = false,
        bool observeNextDrag = false,
        bool hideConstructionForDrag = false
    )
    {
        selectionTrace.UnpatchAll(selectionTrace.Id);
        hideConstructionSight = hideConstructionForDrag;
        if (observeNextDrag)
        {
            lastSelectionDrag = null;
            selectionInput.Clear();
            selectionTrace.Patch(
                AccessTools.Method(typeof(Selector), "HandleMapClicks"),
                prefix: new HarmonyMethod(
                    typeof(InstalledStateScenarios),
                    nameof(ObserveSelectionInput)
                )
            );
            selectionTrace.Patch(
                AccessTools.Method(typeof(Selector), "SelectInsideDragBox"),
                prefix: new HarmonyMethod(
                    typeof(InstalledStateScenarios),
                    nameof(ObserveSelectionDrag)
                ),
                finalizer: new HarmonyMethod(
                    typeof(InstalledStateScenarios),
                    nameof(RestoreSelectionDrag)
                )
            );
        }
        var assembly = AppDomain
            .CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "TotalFog");
        var starter = assembly?.GetType("TotalFog.TotalFogMod");
        var fog = Find.CurrentMap?.components.FirstOrDefault(c =>
            c.GetType().FullName == "TotalFog.MapVisibility"
        );
        var blueprintDef = DefDatabase<ThingDef>.GetNamedSilentFail(
            "Blueprint_TotalFog_Watchtower"
        );
        object mod = null;
        var errors = new List<object>();
        bool? initializerSucceeded = null;
        if (starter != null)
        {
            var getter = typeof(LoadedModManager)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(m =>
                    m.Name == "GetMod"
                    && m.IsGenericMethodDefinition
                    && m.GetParameters().Length == 0
                );
            mod = getter.MakeGenericMethod(starter).Invoke(null, null);
            if (recheckStaticInitializer)
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(starter.TypeHandle);
                    initializerSucceeded = true;
                }
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
            assemblyLoaded = assembly != null,
            assemblyLocation = assembly?.Location,
            starterTypePresent = starter != null,
            starterType = starter?.FullName,
            modInstancePresent = mod != null,
            fogType = fog?.GetType().FullName,
            originalNamespaceTypes = assembly
                ?.GetTypes()
                .Where(t =>
                    t.Namespace?.StartsWith("RimWorldRealFoW", StringComparison.Ordinal) == true
                )
                .Select(t => t.FullName)
                .ToArray(),
            mapFogComponentPresent = fog != null,
            fogInitialized = fog?.GetType().GetProperty("Initialized")?.GetValue(fog),
            recheckStaticInitializer,
            initializerSucceeded,
            initializerErrors = errors,
            selectionDrag = lastSelectionDrag,
            selectionInput = selectionInput.ToArray(),
            watchtowerBlueprint = blueprintDef == null
                ? null
                : new
                {
                    drawerType = blueprintDef.drawerType.ToString(),
                    blueprintDef.selectable,
                    blueprintDef.neverMultiSelect,
                    graphicClass = blueprintDef.graphicData?.graphicClass?.FullName,
                    instances = Find
                        .CurrentMap?.listerThings.AllThings.Where(t => t.def == blueprintDef)
                        .Select(t => new
                        {
                            t.ThingID,
                            position = t.Position.ToString(),
                            drawPosition = t.DrawPos.ToString(),
                            graphic = t.Graphic?.GetType().FullName,
                            drawColor = t.DrawColor.ToString(),
                            owner = t.Faction?.GetUniqueLoadID(),
                            registered = Find.CurrentMap.dynamicDrawManager.DrawThings.Contains(t),
                            canInspect = Presentation.ThingVisibility.CanInspect(t),
                            currentSight = Visibility.IsVisible(t),
                            fogVisible = Presentation.ThingVisibility.IsVisible(
                                t,
                                allowMemory: true
                            ),
                        })
                        .ToArray(),
                },
        };
    }

    private static void ObserveSelectionInput()
    {
        var input = UnityEngine.Event.current;
        if (
            selectionInput.Count >= 32
            || input.type
                is not (
                    UnityEngine.EventType.MouseDown
                    or UnityEngine.EventType.MouseUp
                    or UnityEngine.EventType.MouseDrag
                )
        )
            return;
        selectionInput.Add(
            new
            {
                type = input.type.ToString(),
                rawType = input.rawType.ToString(),
                input.clickCount,
                mouse = UI.MouseMapPosition().ToString(),
                Find.Selector.dragBox.active,
                start = Find.Selector.dragBox.start.ToString(),
                valid = Find.Selector.dragBox.IsValid,
            }
        );
    }

    private static void ObserveSelectionDrag(out Dictionary<int, int> __state)
    {
        __state = new Dictionary<int, int>();
        if (hideConstructionSight)
        {
            var map = Find.CurrentMap;
            var counts = map.GetVisibility().GetFactionShownCells(Faction.OfPlayer);
            foreach (
                var plan in map.listerThings.AllThings.Where(t =>
                    t.Faction == Faction.OfPlayer && (t.def.IsBlueprint || t.def.IsFrame)
                )
            )
            foreach (var cell in plan.OccupiedRect())
            {
                int index = map.cellIndices.CellToIndex(cell);
                if (!__state.ContainsKey(index))
                    __state[index] = counts[index];
                counts[index] = 0;
            }
        }
        lastSelectionDrag = new
        {
            rect = Find.Selector.dragBox.ScreenRect.ToString(),
            candidates = ThingSelectionUtility
                .MultiSelectableThingsInScreenRectDistinct(Find.Selector.dragBox.ScreenRect)
                .Take(32)
                .Select(t => new
                {
                    t.def.defName,
                    category = t.def.category.ToString(),
                    t.def.CountAsResource,
                    position = t.Position.ToString(),
                    canInspect = Presentation.ThingVisibility.CanInspect(t),
                    currentSight = Visibility.IsVisible(t),
                })
                .ToArray(),
        };
    }

    private static void RestoreSelectionDrag(Dictionary<int, int> __state)
    {
        if (__state != null)
        {
            var counts = Find.CurrentMap.GetVisibility().GetFactionShownCells(Faction.OfPlayer);
            foreach (var entry in __state)
                counts[entry.Key] = entry.Value;
        }
        selectionTrace.UnpatchAll(selectionTrace.Id);
    }
}
