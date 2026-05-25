namespace SalesArena.Tournament;

/// <summary>
/// Test/replay runner: caller supplies the deterministic match outcome
/// function. The runner does no I/O.
/// </summary>
public sealed class StubRoundRunner : IRoundRunner
{
    private readonly Func<string, string, string> _pickWinner;
    private readonly string? _reason;

    public StubRoundRunner(Func<string, string, string> pickWinner, string? reason = null)
    {
        _pickWinner = pickWinner ?? throw new ArgumentNullException(nameof(pickWinner));
        _reason = reason;
    }

    public Task<RoundResult> RunAsync(
        string bracketId,
        int roundNumber,
        BracketMatch match,
        int leadsPerRound,
        CancellationToken cancellationToken = default)
    {
        if (match.A.Persona is null || match.B.Persona is null)
        {
            throw new InvalidOperationException("StubRoundRunner cannot run a match containing a bye slot");
        }
        var winner = _pickWinner(match.A.Persona, match.B.Persona);
        if (winner != match.A.Persona && winner != match.B.Persona)
        {
            throw new InvalidOperationException(
                $"pickWinner returned '{winner}' which is neither participant ('{match.A.Persona}' or '{match.B.Persona}')");
        }
        var loser = winner == match.A.Persona ? match.B.Persona : match.A.Persona;
        return Task.FromResult(new RoundResult(bracketId, roundNumber, match.Position, winner, loser, _reason));
    }
}
