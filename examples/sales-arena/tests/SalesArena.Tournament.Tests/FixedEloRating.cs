using SalesArena.Seasons;

namespace SalesArena.Tournament.Tests;

/// <summary>Deterministic ELO stub for tournament tests.</summary>
internal sealed class FixedEloRating : IEloRating
{
    private readonly IReadOnlyDictionary<string, double> _ratings;

    public FixedEloRating(IReadOnlyDictionary<string, double> ratings)
    {
        _ratings = ratings;
    }

    public double GetRating(string seasonId, string persona)
        => _ratings.TryGetValue(persona, out var r) ? r : EloCalculator.DefaultStartingRating;

    public (double NewRatingA, double NewRatingB) ApplyMatch(MatchRecord match)
        => throw new NotSupportedException();

    public IReadOnlyList<EloLeaderboardEntry> GetLeaderboard(
        string seasonId, Season? theme = null,
        IReadOnlyDictionary<string, TimeSpan>? hoursWorkedByPersona = null)
        => throw new NotSupportedException();
}
