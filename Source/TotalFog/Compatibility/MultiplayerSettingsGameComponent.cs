using System.Collections.Generic;
using Verse;

namespace TotalFog.Compatibility;

/// <summary>Carry effective shared settings with MP snapshots, independently of local mod preferences.</summary>
public sealed class MultiplayerSettingsGameComponent : GameComponent
{
    private Dictionary<string, string> settings;

    public MultiplayerSettingsGameComponent(Game game) { }

    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
            settings = MultiplayerIntegration.Active
                ? MultiplayerIntegration.CaptureSettings()
                : null;
        Scribe_Collections.Look(
            ref settings,
            "totalFogMultiplayerSettings",
            LookMode.Value,
            LookMode.Value
        );
        // Restore during variable loading, before map initialization publishes
        // any coverage or discovery under a client's local preference values.
        if (Scribe.mode == LoadSaveMode.LoadingVars && settings != null)
            MultiplayerIntegration.RestoreSettings(settings);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
            settings = null;
    }
}
