namespace SalesArena.Bench;

/// <summary>
/// Bench tunables. SA-08-17 acceptance pins: bench size 12, FIFO eviction
/// for unused benchers, ELO threshold over a rolling 3-contest window.
/// </summary>
public sealed record BenchOptions
{
    /// <summary>Maximum number of personas allowed on the reserve bench. Defaults to 12.</summary>
    public int MaxReserveSize { get; init; } = 12;

    /// <summary>
    /// ELO threshold below which an active persona becomes eligible for
    /// relegation. Defaults to <c>1450</c> (mid-amateur floor).
    /// </summary>
    public double EloFloor { get; init; } = 1450.0;

    /// <summary>
    /// Number of consecutive contests an active persona must spend below
    /// <see cref="EloFloor"/> before auto-relegation fires. Defaults to 3.
    /// </summary>
    public int ConsecutiveBelowFloorBeforeRelegation { get; init; } = 3;

    /// <summary>
    /// Maximum personas allowed on the active sales floor at any one time.
    /// </summary>
    public int MaxActiveSize { get; init; } = 6;
}
