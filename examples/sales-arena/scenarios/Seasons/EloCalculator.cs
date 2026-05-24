namespace SalesArena.Seasons;

/// <summary>
/// Pure ELO math (no state). Standard formulation with adjustable K-factor.
/// </summary>
public static class EloCalculator
{
    /// <summary>Default starting rating for any persona without prior history.</summary>
    public const double DefaultStartingRating = 1500.0;

    /// <summary>Default K-factor (chess-standard for amateurs).</summary>
    public const int DefaultK = 32;

    /// <summary>
    /// Expected score of A vs B given current ratings.
    /// </summary>
    public static double Expected(double ratingA, double ratingB)
    {
        return 1.0 / (1.0 + Math.Pow(10.0, (ratingB - ratingA) / 400.0));
    }

    /// <summary>
    /// Apply one head-to-head match. Returns the new ratings for both personas.
    /// K-factor controls volatility; defaults to 32.
    /// </summary>
    public static (double NewRatingA, double NewRatingB) Apply(
        double ratingA, double ratingB, MatchOutcome outcome, int k = DefaultK)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        var expectedA = Expected(ratingA, ratingB);
        var expectedB = 1.0 - expectedA;
        var (scoreA, scoreB) = outcome switch
        {
            MatchOutcome.AWins => (1.0, 0.0),
            MatchOutcome.BWins => (0.0, 1.0),
            MatchOutcome.Draw => (0.5, 0.5),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

        var newA = ratingA + k * (scoreA - expectedA);
        var newB = ratingB + k * (scoreB - expectedB);
        return (newA, newB);
    }
}
