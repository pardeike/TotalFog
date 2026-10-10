using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>Optional MPAPI binding. No Multiplayer dependency or work in the sight hot path.</summary>
internal static class MultiplayerIntegration
{
    private static readonly FieldInfo[] settingsFields = typeof(FogSettings)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType.IsPrimitive && !field.IsLiteral && !field.IsInitOnly)
        .OrderBy(field => field.Name, StringComparer.Ordinal)
        .ToArray();
    private static readonly FieldInfo[] savedFields = settingsFields
        .Concat(
            new[]
            {
                typeof(FogSettings).GetField(
                    "treesBlockSightValue",
                    BindingFlags.NonPublic | BindingFlags.Static
                ),
            }
        )
        .ToArray();
    private static Func<bool> isActive = () => false;
    private static Action watchBegin;
    private static Action watchEnd;
    private static Action<object, object>[] watches = Array.Empty<Action<object, object>>();
    private static Action<Map, Faction, bool> pushFaction;
    private static Func<Map, Faction> popFaction;
    private static Func<Map, int?> mapTicks;

    internal static bool Active => isActive();

    // Tick callbacks already run under MP's map context. Deadline creation can
    // also run from world commands or interface work, so use the owning map.
    internal static int TicksFor(Map map) =>
        mapTicks != null && map != null && Active
            ? mapTicks(map) ?? Find.TickManager.TicksGame
            : Find.TickManager.TicksGame;

    internal static void Install()
    {
        var api = AccessTools.TypeByName("Multiplayer.API.MP");
        if (api == null || api.GetField("enabled")?.GetValue(null) is not true)
            return;
        var register = api.GetMethod("RegisterSyncField", new[] { typeof(FieldInfo) });
        if (register == null)
            throw new MissingMethodException(api.FullName, "RegisterSyncField");
        var fieldApi = register.ReturnType;
        var watch = fieldApi.GetMethod("Watch");
        var buffer = fieldApi.GetMethod("SetBufferChanges");
        var fields = settingsFields;
        var bound = new Action<object, object>[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            var handler = register.Invoke(null, new object[] { fields[i] });
            buffer.Invoke(handler, null);
            bound[i] =
                (Action<object, object>)
                    Delegate.CreateDelegate(typeof(Action<object, object>), handler, watch);
        }
        watchBegin = (Action)Delegate.CreateDelegate(typeof(Action), api.GetMethod("WatchBegin"));
        watchEnd = (Action)Delegate.CreateDelegate(typeof(Action), api.GetMethod("WatchEnd"));
        isActive =
            (Func<bool>)
                Delegate.CreateDelegate(
                    typeof(Func<bool>),
                    api.GetProperty("IsInMultiplayer").GetGetMethod()
                );
        watches = bound;
        var clockGetter =
            AccessTools
                .TypeByName("Multiplayer.Client.Extensions")
                ?.GetMethod("AsyncTime", new[] { typeof(Map) })
            ?? throw new MissingMethodException("Multiplayer map clock is unavailable.");
        var clockField =
            clockGetter.ReturnType.GetField("mapTicks")
            ?? throw new MissingFieldException(clockGetter.ReturnType.FullName, "mapTicks");
        var mapParameter = Expression.Parameter(typeof(Map), "map");
        var clock = Expression.Variable(clockGetter.ReturnType, "clock");
        // Bind once. No reflected field reads, boxing or per-map cache lifetime
        // to maintain when commands create/remove maps or sessions restart.
        mapTicks = Expression
            .Lambda<Func<Map, int?>>(
                Expression.Block(
                    new[] { clock },
                    Expression.Assign(clock, Expression.Call(clockGetter, mapParameter)),
                    Expression.Condition(
                        Expression.Equal(clock, Expression.Constant(null, clockGetter.ReturnType)),
                        Expression.Constant(null, typeof(int?)),
                        Expression.Convert(Expression.Field(clock, clockField), typeof(int?))
                    )
                ),
                mapParameter
            )
            .Compile();
        // Deferred notifications retain their recipient even when their map
        // ticks under another player's faction. Use MP's own data context.
        var factions = AccessTools.TypeByName("Multiplayer.Client.Factions.FactionExtensions");
        if (factions != null)
        {
            pushFaction =
                (Action<Map, Faction, bool>)
                    Delegate.CreateDelegate(
                        typeof(Action<Map, Faction, bool>),
                        factions.GetMethod(
                            "PushFaction",
                            new[] { typeof(Map), typeof(Faction), typeof(bool) }
                        )
                    );
            popFaction =
                (Func<Map, Faction>)
                    Delegate.CreateDelegate(
                        typeof(Func<Map, Faction>),
                        factions.GetMethod("PopFaction", new[] { typeof(Map) })
                    );
        }
    }

    internal static bool BeginFactionContext(Map map, Faction faction)
    {
        if (!Active || faction == null || faction == Faction.OfPlayer)
            return false;
        if (pushFaction == null || popFaction == null)
            throw new MissingMethodException("Multiplayer faction context is unavailable.");
        pushFaction(map, faction, true);
        return true;
    }

    internal static void EndFactionContext(Map map, bool pushed)
    {
        if (pushed)
            popFaction(map);
    }

    internal static bool BeginSettingsWatch()
    {
        if (!Active)
            return false;
        watchBegin();
        try
        {
            foreach (var watch in watches)
                watch(null, null);
            return true;
        }
        catch
        {
            watchEnd();
            throw;
        }
    }

    internal static void EndSettingsWatch(bool watching)
    {
        if (watching)
            watchEnd();
    }

    internal static Dictionary<string, string> CaptureSettings()
    {
        var values = new Dictionary<string, string>(savedFields.Length);
        foreach (var field in savedFields)
        {
            object value = field.GetValue(null);
            string encoded =
                value is float single ? single.ToString("R", CultureInfo.InvariantCulture)
                : value is double number ? number.ToString("R", CultureInfo.InvariantCulture)
                : Convert.ToString(value, CultureInfo.InvariantCulture);
            values.Add(field.Name, encoded);
        }
        return values;
    }

    internal static void RestoreSettings(Dictionary<string, string> values)
    {
        foreach (var field in savedFields)
            if (values.TryGetValue(field.Name, out var encoded))
                field.SetValue(
                    null,
                    Convert.ChangeType(encoded, field.FieldType, CultureInfo.InvariantCulture)
                );
    }
}
