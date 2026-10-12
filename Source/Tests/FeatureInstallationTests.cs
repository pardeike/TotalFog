using System;
using System.Collections.Generic;
using TotalFog.Core;
using Xunit;

namespace TotalFog.Tests;

public sealed class FeatureInstallationTests
{
    [Fact]
    public void Missing_target_does_not_prevent_later_gates_and_warns_once()
    {
        var installed = new List<string>();
        var warnings = new List<string>();
        string missing = "missing-target-" + Guid.NewGuid();
        void Install(string name, Action target) =>
            FeatureInstallation.Try(name, target, warnings.Add);
        Install("letters", () => installed.Add("letters"));
        Install(
            missing,
            () => throw new MissingMethodException("Target no longer exists\nshape changed")
        );
        Install("audio", () => installed.Add("audio"));
        Install(missing, () => throw new MissingMethodException("Target no longer exists"));
        Install("combat", () => installed.Add("combat"));
        Assert.Equal(new[] { "letters", "audio", "combat" }, installed);
        Assert.Single(warnings);
        Assert.Contains("Target no longer exists", warnings[0]);
        Assert.DoesNotContain("\n", warnings[0]);
    }

    [Fact]
    public void Successful_install_returns_true_without_warning()
    {
        bool installed = false;
        Assert.True(
            FeatureInstallation.Try("success", () => installed = true, _ => throw new Exception())
        );
        Assert.True(installed);
    }
}
