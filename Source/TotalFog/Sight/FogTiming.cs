namespace TotalFog;

/// <summary>Tick intervals shared by the periodic fog checks. Idle frames do no fog work;
/// these only bound how often stationary state is re-examined.</summary>
internal static class FogTiming
{
    /// <summary>Stationary sight sources re-check their range and surroundings this often.</summary>
    public const int SourceRefreshInterval = 30;

    /// <summary>Colonists emit hearing cues for nearby hidden movement this often.</summary>
    public const int HearingCueInterval = 100;

    /// <summary>Walls and doors re-evaluate whether they still block sight this often.</summary>
    public const int BlockerCheckInterval = 30;

    /// <summary>Trees re-evaluate their growth-based occlusion this often.</summary>
    public const int TreeOcclusionInterval = 60;

    /// <summary>Deferred letters and messages are re-checked for visibility this often.</summary>
    public const int NotificationCheckInterval = 30;
}
