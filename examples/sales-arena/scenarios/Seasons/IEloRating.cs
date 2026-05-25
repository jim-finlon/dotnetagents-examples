namespace SalesArena.Seasons;

/// <summary>
/// Tracks per-season ELO ratings + computes leaderboards. Implementations
/// MUST keep ratings monotonically queryable (a missing persona returns
/// <see cref="EloCalculator.DefaultStartingRating"/>).
/// </summary>
public interface IEloRating
{
    /// <summary>Current rating for a persona within a season (or default).</summary>
    double GetRating(string seasonId, string persona);

    /// <summary>
    /// Record a match and update both personas' ratings inside the season.
    /// </summary>
    /// <returns>Updated ratings for A and B (post-apply).</returns>
    (double NewRatingA, double NewRatingB) ApplyMatch(MatchRecord match);

    /// <summary>
    /// Render the season's leaderboard. Optionally apply theme buffs and
    /// burnout penalties supplied by the caller (the rating store stays
    /// pure; theme effects are visible in the rendered view only).
    /// </summary>
    /// <param name="seasonId">Season scope; "all-time" for cross-season aggregate.</param>
    /// <param name="theme">Optional season theme used for buff application; ignored for all-time.</param>
    /// <param name="hoursWorkedByPersona">Per-persona hours-worked for burnout penalty (Office-Space season).</param>
    IReadOnlyList<EloLeaderboardEntry> GetLeaderboard(
        string seasonId,
        Season? theme = null,
        IReadOnlyDictionary<string, TimeSpan>? hoursWorkedByPersona = null);
}

public sealed record EloLeaderboardEntry(
    int Position,
    string Persona,
    double RawRating,
    double DisplayRating,
    int MatchesPlayed);
