namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Score by total revenue closed. The default for most contests — Mitch &amp;
/// Murray want to see whose ringing the loudest bell.
/// </summary>
public sealed class RevenueScoring : IScoringConfig
{
    public string Id => ScoringConfigIds.ByRevenue;
    public string Name => "Revenue ($)";

    public double ComputeScore(PersonaStats stats) => (double)stats.RevenueUsd;
}

/// <summary>
/// Score by raw count of won deals. Levene's preferred view — pure volume.
/// </summary>
public sealed class DealCountScoring : IScoringConfig
{
    public string Id => ScoringConfigIds.ByDealCount;
    public string Name => "Deals Won";

    public double ComputeScore(PersonaStats stats) => stats.DealsWon;
}

/// <summary>
/// Score by win rate (wins / decisions). Roma's preferred view — quality over
/// volume. Personas with &lt; <see cref="MinDecisionsForRanking"/> decisions
/// are pinned to score 0 so a 1-for-1 doesn't beat the actual closer.
/// </summary>
public sealed class ConversionScoring : IScoringConfig
{
    /// <summary>Minimum decisions (wins + losses) required to qualify for ranking.</summary>
    public int MinDecisionsForRanking { get; }

    public ConversionScoring(int minDecisionsForRanking = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minDecisionsForRanking);
        MinDecisionsForRanking = minDecisionsForRanking;
    }

    public string Id => ScoringConfigIds.ByConversion;
    public string Name => "Conversion (%)";

    public double ComputeScore(PersonaStats stats)
    {
        var decisions = stats.DealsWon + stats.DealsLost;
        if (decisions < MinDecisionsForRanking) return 0.0;
        return stats.WinRate;
    }
}

/// <summary>
/// Deterministic composite score. Weighted blend of revenue, win-rate, and
/// speed. Premium customers can swap in hosted model routing without changing
/// the public leaderboard contract.
/// </summary>
public sealed class CompositeScoring : IScoringConfig
{
    public double RevenueWeight { get; }
    public double WinRateWeight { get; }
    public double SpeedWeight { get; }

    public CompositeScoring(double revenueWeight = 0.50, double winRateWeight = 0.30, double speedWeight = 0.20)
    {
        if (revenueWeight < 0 || winRateWeight < 0 || speedWeight < 0)
            throw new ArgumentException("Weights must be non-negative.");
        if (Math.Abs(revenueWeight + winRateWeight + speedWeight - 1.0) > 1e-6)
            throw new ArgumentException("Weights must sum to 1.0.");
        RevenueWeight = revenueWeight;
        WinRateWeight = winRateWeight;
        SpeedWeight = speedWeight;
    }

    public string Id => ScoringConfigIds.ByComposite;
    public string Name => "Composite Score";

    public double ComputeScore(PersonaStats stats)
    {
        // Normalize revenue against a reference $100k so the metric scales.
        var revenueScore = Math.Min((double)stats.RevenueUsd / 100_000.0, 1.0);

        // Speed: faster average close → higher score, capped at 1.0.
        // Reference: 7-day target. Sub-day close ≈ 1.0; > 7-day → < 0.5.
        double speedScore;
        if (stats.AverageTimeToClose is null || stats.DealsWon == 0)
        {
            speedScore = 0.0;
        }
        else
        {
            var hours = stats.AverageTimeToClose.Value.TotalHours;
            speedScore = Math.Clamp(1.0 - (hours / (24.0 * 14.0)), 0.0, 1.0);
        }

        return (revenueScore * RevenueWeight)
             + (stats.WinRate * WinRateWeight)
             + (speedScore * SpeedWeight);
    }
}
