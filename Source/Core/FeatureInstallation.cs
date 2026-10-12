using System;
using System.Collections.Generic;

namespace TotalFog.Core;

/// <summary>One unavailable feature must not abort unrelated installation.</summary>
public static class FeatureInstallation
{
    private static readonly HashSet<string> warned = new();

    public static bool Try(string feature, Action install, Action<string> warning)
    {
        try
        {
            install();
            return true;
        }
        catch (Exception exception)
        {
            Warn(feature, exception.GetBaseException().Message, warning);
            return false;
        }
    }

    public static void Warn(string feature, string reason, Action<string> warning)
    {
        if (warned.Add(feature))
            warning(
                "[Total Fog] "
                    + feature
                    + " unavailable: "
                    + reason.Replace('\n', ' ').Replace('\r', ' ')
            );
    }
}
