namespace SalesArena.HotStove;

public sealed record HotStoveOptions
{
    /// <summary>Story-pinned: 48h cooldown per persona between trades.</summary>
    public TimeSpan TradeCooldown { get; init; } = TimeSpan.FromHours(48);

    /// <summary>
    /// Minimum A/B delta score required for an automatic promotion via
    /// <see cref="DefaultAbPromotionDecider"/>. Operators can plug a stricter
    /// or more permissive decider per deployment.
    /// </summary>
    public double DefaultAbPromotionFloor { get; init; } = 0.0;
}
