namespace SalesArena.Tournament;

/// <summary>
/// Outcome of one bracket match. Returned by <see cref="IRoundRunner"/>.
/// Tournament does not allow draws — the runner must pick a winner (the
/// orchestrator decides tiebreak policy: ELO, revenue, head-to-head, coin).
/// </summary>
public sealed record RoundResult(
    string BracketId,
    int RoundNumber,
    int MatchPosition,
    string Winner,
    string Loser,
    string? Reason);

/// <summary>
/// Pluggable runner for a single bracket match. Production implementations
/// drive a real mini-contest; tests + offline replays use the stub.
/// </summary>
public interface IRoundRunner
{
    Task<RoundResult> RunAsync(
        string bracketId,
        int roundNumber,
        BracketMatch match,
        int leadsPerRound,
        CancellationToken cancellationToken = default);
}
