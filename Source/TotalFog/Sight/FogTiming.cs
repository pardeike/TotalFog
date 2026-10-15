namespace TotalFog;

/// <summary>Minimum tick intervals between periodic fog checks. A check runs on its owner's
/// next normal or long tick once the interval has passed, so the ticker type can stretch the
/// real cadence. Things that never tick update only on spawn, comp signals and explicit
/// refreshes.</summary>
internal static class FogTiming
{
    /// <summary>Sight sources that have not moved re-check their range and surroundings at
    /// most this often. A source that moved updates on its next tick.</summary>
    public const int SourceRefreshInterval = 30;

    /// <summary>Pawn sight sources emit hearing cues for nearby hidden movement at most this
    /// often.</summary>
    public const int HearingCueInterval = 100;

    /// <summary>Ticking sight blockers, such as doors, re-check whether they block sight at
    /// most this often. Walls do not tick.</summary>
    public const int BlockerCheckInterval = 30;

    /// <summary>Trees re-check their growth-based occlusion at most this often. Plants use the
    /// long ticker, so in practice this happens every 2,000 ticks.</summary>
    public const int TreeOcclusionInterval = 60;

    /// <summary>Deferred letters and messages are re-checked for visibility at most this
    /// often.</summary>
    public const int NotificationCheckInterval = 30;
}
