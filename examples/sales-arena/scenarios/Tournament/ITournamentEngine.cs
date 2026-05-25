namespace SalesArena.Tournament;

/// <summary>
/// Single-elim bracket engine. Pure scheduling + advancement. Actual
/// mini-contest execution is the <see cref="IRoundRunner"/>'s job.
/// </summary>
public interface ITournamentEngine
{
    /// <summary>
    /// Build a fresh bracket for the given personas. Seeding pulls ratings
    /// from the configured <c>seasonId</c> via the supplied ELO source.
    /// First-round byes are assigned when N is not a power of two.
    /// </summary>
    Bracket CreateBracket(
        IReadOnlyList<string> personas,
        string seasonId,
        int? leadsPerRound = null,
        DateTimeOffset? createdAtUtc = null);

    /// <summary>
    /// Apply one round result to an in-flight bracket and return the new state.
    /// </summary>
    Bracket ApplyRoundResult(Bracket bracket, RoundResult result);

    /// <summary>
    /// Run every pending match in the current round, advance, and repeat
    /// until a champion is crowned. Useful for tests + headless replays.
    /// </summary>
    Task<Bracket> RunToCompletionAsync(
        Bracket bracket,
        IRoundRunner runner,
        CancellationToken cancellationToken = default);
}
