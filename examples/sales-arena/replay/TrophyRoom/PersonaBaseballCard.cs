namespace SalesArena.Replay.TrophyRoom;

/// <summary>
/// Lifetime stats for one persona, aggregated across every contest they've
/// competed in. The Trophy Room baseball-card view.
/// </summary>
public sealed record PersonaBaseballCard(
    string Persona,
    int ContestsEntered,
    int CadillacWins,
    int SteakKnivesPlaces,
    int YouAreFiredFinishes,
    decimal LifetimeRevenueUsd,
    int LifetimeDealsWon,
    int LifetimeDealsLost,
    decimal SignatureCloseUsd,
    string? SignatureCloseLeadId,
    DateTimeOffset? SignatureCloseAtUtc,
    decimal BestContestRevenueUsd,
    string? BestContestId,
    DateTimeOffset? FirstContestAtUtc,
    DateTimeOffset? MostRecentContestAtUtc)
{
    /// <summary>Lifetime win rate (wins / decisions).</summary>
    public double LifetimeWinRate => (LifetimeDealsWon + LifetimeDealsLost) > 0
        ? (double)LifetimeDealsWon / (LifetimeDealsWon + LifetimeDealsLost)
        : 0.0;

    /// <summary>Cadillac rate (wins / contests entered).</summary>
    public double CadillacRate => ContestsEntered > 0
        ? (double)CadillacWins / ContestsEntered
        : 0.0;
}
