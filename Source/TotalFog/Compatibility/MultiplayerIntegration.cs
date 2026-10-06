using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;

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

    internal static bool Active => isActive();

    internal static void Install()
    {
        var api = AccessTools.TypeByName("Multiplayer.API.MP");
        if (api == null)
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
