namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Tunable parameters for the radio. Defaults are intentionally
/// non-spammy: opt-in mute defaults on; minimum inter-cue spacing.
/// </summary>
public sealed record SalesFloorRadioOptions
{
    /// <summary>Default: muted. Operators flip to enabled per contest.</summary>
    public bool StartMuted { get; init; } = true;

    /// <summary>
    /// Minimum gap between bell-driven narrator output and an ambient cue.
    /// Defaults to 30s so the radio never steps on a fresh bell.
    /// </summary>
    public TimeSpan MinSilenceAfterBell { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Minimum gap between two ambient cues regardless of the rate limiter.
    /// Defaults to 90s.
    /// </summary>
    public TimeSpan MinSpacingBetweenAmbient { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Inactivity threshold that triggers GenericFiller when no other cue
    /// kind qualifies. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan InactivityFillerThreshold { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// ContestProgress fires every N elapsed minutes of contest time. The
    /// radio tracks the last announcement and only fires once per bucket.
    /// </summary>
    public TimeSpan ContestProgressEvery { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Min consecutive outbound touches per persona before a PersonaMomentum
    /// cue is eligible. Story SA-08-09 acceptance: 3+.
    /// </summary>
    public int PersonaMomentumTouchThreshold { get; init; } = 3;

    /// <summary>
    /// Min silent-time on a lead before LeadAged is eligible.
    /// </summary>
    public TimeSpan LeadAgedThreshold { get; init; } = TimeSpan.FromHours(4);
}
