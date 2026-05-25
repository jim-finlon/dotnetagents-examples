using SalesArena.Seasons;

namespace SalesArena.Tournament;

/// <summary>
/// Default single-elimination engine. Stateless across brackets — each
/// bracket carries its own progression in its <see cref="Bracket.Rounds"/>
/// record so callers can persist + resume freely.
/// </summary>
public sealed class SingleElimTournamentEngine : ITournamentEngine
{
    private readonly IEloRating _elo;
    private readonly TournamentOptions _options;
    private readonly TimeProvider _timeProvider;

    public SingleElimTournamentEngine(
        IEloRating elo,
        TournamentOptions? options = null,
        TimeProvider? timeProvider = null)
    {
        _elo = elo ?? throw new ArgumentNullException(nameof(elo));
        _options = options ?? new TournamentOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Bracket CreateBracket(
        IReadOnlyList<string> personas,
        string seasonId,
        int? leadsPerRound = null,
        DateTimeOffset? createdAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(personas);
        ArgumentException.ThrowIfNullOrEmpty(seasonId);

        if (personas.Count < 2)
        {
            throw new TournamentException(TournamentErrorCode.NotEnoughPersonas,
                $"a bracket needs at least 2 personas; got {personas.Count}");
        }
        if (personas.Count > _options.MaxPersonas)
        {
            throw new TournamentException(TournamentErrorCode.TooManyPersonas,
                $"bracket size {personas.Count} exceeds operator cap {_options.MaxPersonas}; override TournamentOptions.MaxPersonas to allow it");
        }
        if (personas.Distinct(StringComparer.Ordinal).Count() != personas.Count)
        {
            throw new TournamentException(TournamentErrorCode.DuplicatePersona,
                "bracket entrants must be distinct");
        }

        // Seed by ELO desc; tiebreak ordinal-asc so the same input + ratings
        // always produces the same seed order.
        var seeded = personas
            .Select(p =>
            {
                double rating;
                try { rating = _elo.GetRating(seasonId, p); }
                catch { rating = _options.DefaultRating; }
                return (Persona: p, Rating: rating);
            })
            .OrderByDescending(x => x.Rating)
            .ThenBy(x => x.Persona, StringComparer.Ordinal)
            .ToList();

        var size = NextPowerOfTwo(seeded.Count);
        var slots = new BracketSlot[size];
        // Standard single-elim seed-positioning: seeds placed at the bracket
        // positions returned by SeedPositions(size).
        var positions = SeedPositions(size);
        for (var i = 0; i < size; i++)
        {
            var slotIndex = positions[i];
            if (i < seeded.Count)
            {
                slots[slotIndex] = new BracketSlot(slotIndex, seeded[i].Persona, i + 1, IsBye: false);
            }
            else
            {
                slots[slotIndex] = BracketSlot.Bye(slotIndex);
            }
        }

        // Round 1: pair (0,1), (2,3), ...
        var matches = new List<BracketMatch>(size / 2);
        for (var i = 0; i < size; i += 2)
        {
            var a = slots[i];
            var b = slots[i + 1];
            string? winner = null;
            if (a.IsBye && b.IsBye)
            {
                // Should not happen with NextPowerOfTwo, but guard regardless.
                winner = null;
            }
            else if (a.IsBye)
            {
                winner = b.Persona;
            }
            else if (b.IsBye)
            {
                winner = a.Persona;
            }
            matches.Add(new BracketMatch(
                Position: i / 2,
                A: a,
                B: b,
                Winner: winner,
                CompletedAtUtc: winner is not null ? (createdAtUtc ?? _timeProvider.GetUtcNow()) : null));
        }

        var bracket = new Bracket(
            Id: Guid.NewGuid().ToString("N"),
            Personas: personas.ToArray(),
            LeadsPerRound: leadsPerRound ?? _options.DefaultLeadsPerRound,
            CreatedAtUtc: createdAtUtc ?? _timeProvider.GetUtcNow(),
            Rounds: new[] { new BracketRound(1, matches) },
            Status: matches.All(m => m.Winner is not null) ? AdvanceableStatus(matches) : BracketStatus.InProgress,
            Champion: null);

        // If every round-1 match was a bye-resolved walkover (rare; e.g. 2
        // entrants with one bye is impossible since min=2 → size=2 → 0 byes),
        // we still need to advance.
        bracket = MaybeAdvance(bracket);
        return bracket;
    }

    public Bracket ApplyRoundResult(Bracket bracket, RoundResult result)
    {
        ArgumentNullException.ThrowIfNull(bracket);
        ArgumentNullException.ThrowIfNull(result);
        if (bracket.Status == BracketStatus.Completed)
        {
            throw new TournamentException(TournamentErrorCode.BracketAlreadyComplete,
                $"bracket {bracket.Id} is already complete; champion = {bracket.Champion}");
        }
        if (result.BracketId != bracket.Id)
        {
            throw new TournamentException(TournamentErrorCode.UnknownMatch,
                $"result is scoped to bracket {result.BracketId} but applying to {bracket.Id}");
        }

        var rounds = bracket.Rounds.ToList();
        var roundIdx = result.RoundNumber - 1;
        if (roundIdx < 0 || roundIdx >= rounds.Count)
        {
            throw new TournamentException(TournamentErrorCode.UnknownRound,
                $"round {result.RoundNumber} is out of range for bracket {bracket.Id}");
        }
        var round = rounds[roundIdx];
        var match = round.Matches.SingleOrDefault(m => m.Position == result.MatchPosition)
            ?? throw new TournamentException(TournamentErrorCode.UnknownMatch,
                $"match position {result.MatchPosition} not found in round {result.RoundNumber}");
        if (match.Winner is not null)
        {
            throw new TournamentException(TournamentErrorCode.MatchAlreadyDecided,
                $"match at round {result.RoundNumber} position {result.MatchPosition} already has winner {match.Winner}");
        }
        if (result.Winner != match.A.Persona && result.Winner != match.B.Persona)
        {
            throw new TournamentException(TournamentErrorCode.WinnerNotInMatch,
                $"winner '{result.Winner}' is not a participant in this match");
        }

        var updated = match with { Winner = result.Winner, CompletedAtUtc = _timeProvider.GetUtcNow() };
        var updatedMatches = round.Matches.Select(m => m.Position == result.MatchPosition ? updated : m).ToArray();
        rounds[roundIdx] = round with { Matches = updatedMatches };

        var newBracket = bracket with { Rounds = rounds };
        return MaybeAdvance(newBracket);
    }

    public async Task<Bracket> RunToCompletionAsync(
        Bracket bracket,
        IRoundRunner runner,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bracket);
        ArgumentNullException.ThrowIfNull(runner);

        while (bracket.Status != BracketStatus.Completed)
        {
            var currentRound = bracket.Rounds[^1];
            var pending = currentRound.Matches.Where(m => m.Winner is null).ToArray();
            if (pending.Length == 0)
            {
                bracket = MaybeAdvance(bracket);
                continue;
            }
            foreach (var pendingMatch in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await runner.RunAsync(bracket.Id, currentRound.RoundNumber, pendingMatch, bracket.LeadsPerRound, cancellationToken).ConfigureAwait(false);
                bracket = ApplyRoundResult(bracket, result);
            }
        }
        return bracket;
    }

    private Bracket MaybeAdvance(Bracket bracket)
    {
        var current = bracket.Rounds[^1];
        if (current.Matches.Any(m => m.Winner is null))
        {
            return bracket with { Status = BracketStatus.InProgress };
        }
        var winners = current.Matches.Select(m => m.Winner).ToArray();
        if (winners.Length == 1)
        {
            // Champion crowned.
            return bracket with
            {
                Status = BracketStatus.Completed,
                Champion = winners[0],
            };
        }
        // Build next round.
        var nextMatches = new List<BracketMatch>(winners.Length / 2);
        for (var i = 0; i < winners.Length; i += 2)
        {
            var aPersona = winners[i];
            var bPersona = winners[i + 1];
            // Carry the seed of the higher seed forward.
            var aSeed = SeedOfWinner(bracket, current.RoundNumber, i, aPersona!);
            var bSeed = SeedOfWinner(bracket, current.RoundNumber, i + 1, bPersona!);
            var slotA = new BracketSlot(i, aPersona, aSeed, IsBye: false);
            var slotB = new BracketSlot(i + 1, bPersona, bSeed, IsBye: false);
            nextMatches.Add(new BracketMatch(i / 2, slotA, slotB, Winner: null, CompletedAtUtc: null));
        }
        var nextRound = new BracketRound(current.RoundNumber + 1, nextMatches);
        var rounds = bracket.Rounds.ToList();
        rounds.Add(nextRound);
        return bracket with
        {
            Rounds = rounds,
            Status = BracketStatus.InProgress,
        };
    }

    private static int SeedOfWinner(Bracket bracket, int roundNumber, int slotIndex, string persona)
    {
        var round = bracket.Rounds[roundNumber - 1];
        var matchIdx = slotIndex / 2;
        var match = round.Matches[matchIdx];
        if (match.A.Persona == persona)
        {
            return match.A.Seed;
        }
        if (match.B.Persona == persona)
        {
            return match.B.Seed;
        }
        return int.MaxValue;
    }

    private static BracketStatus AdvanceableStatus(IEnumerable<BracketMatch> matches)
    {
        return matches.All(m => m.Winner is not null) ? BracketStatus.InProgress : BracketStatus.InProgress;
    }

    /// <summary>Smallest power of two ≥ <paramref name="n"/>.</summary>
    public static int NextPowerOfTwo(int n)
    {
        if (n <= 1) return 1;
        var power = 1;
        while (power < n) power <<= 1;
        return power;
    }

    /// <summary>
    /// Standard single-elimination seed-positioning. Returns an array of
    /// length <paramref name="bracketSize"/> where index i is the slot
    /// position of seed i+1. Top seed plays the lowest-remaining seed; this
    /// is the same shape used by tennis + chess tournaments worldwide.
    /// </summary>
    public static int[] SeedPositions(int bracketSize)
    {
        // Recursive doubling: base case bracket of 2 → [0, 1].
        // Doubling: positions = [for each existing position p: 2*p, then for
        // each p: (2*size - 1 - 2*p)] where size = current count.
        if (bracketSize == 1) return new[] { 0 };
        var current = new[] { 0, 1 };
        var size = 2;
        while (size < bracketSize)
        {
            var next = new int[size * 2];
            for (var i = 0; i < size; i++)
            {
                next[i] = current[i] * 2;
                next[size * 2 - 1 - i] = current[i] * 2 + 1;
            }
            current = next;
            size *= 2;
        }
        return current;
    }
}
