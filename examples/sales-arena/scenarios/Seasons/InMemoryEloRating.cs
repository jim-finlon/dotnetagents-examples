namespace SalesArena.Seasons;

/// <summary>
/// All-time leaderboard sentinel. Cross-season aggregate uses sum of every
/// persona's ratings across every recorded season divided by the count of
/// seasons they played in.
/// </summary>
public static class SeasonScopes
{
    public const string AllTime = "all-time";
}

/// <summary>
/// In-memory ELO store. Thread-safe for the contest-tick cadence
/// (single writer typical; readers see at-rest state).
/// </summary>
public sealed class InMemoryEloRating : IEloRating
{
    private readonly Dictionary<string, Dictionary<string, RatingCell>> _bySeason
        = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly int _kFactor;

    public InMemoryEloRating(int kFactor = EloCalculator.DefaultK)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(kFactor);
        _kFactor = kFactor;
    }

    public double GetRating(string seasonId, string persona)
    {
        ArgumentException.ThrowIfNullOrEmpty(seasonId);
        ArgumentException.ThrowIfNullOrEmpty(persona);

        lock (_lock)
        {
            if (string.Equals(seasonId, SeasonScopes.AllTime, StringComparison.OrdinalIgnoreCase))
            {
                return AllTimeRating(persona);
            }
            if (_bySeason.TryGetValue(seasonId, out var personas)
                && personas.TryGetValue(persona, out var cell))
            {
                return cell.Rating;
            }
            return EloCalculator.DefaultStartingRating;
        }
    }

    public (double NewRatingA, double NewRatingB) ApplyMatch(MatchRecord match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var seasonId = match.SeasonId ?? throw new ArgumentException("match must declare a SeasonId", nameof(match));

        lock (_lock)
        {
            if (!_bySeason.TryGetValue(seasonId, out var personas))
            {
                personas = new Dictionary<string, RatingCell>(StringComparer.Ordinal);
                _bySeason[seasonId] = personas;
            }

            var a = personas.TryGetValue(match.PersonaA, out var cellA) ? cellA : new RatingCell();
            var b = personas.TryGetValue(match.PersonaB, out var cellB) ? cellB : new RatingCell();

            var (newA, newB) = EloCalculator.Apply(a.Rating, b.Rating, match.Outcome, _kFactor);
            personas[match.PersonaA] = new RatingCell(newA, a.Matches + 1);
            personas[match.PersonaB] = new RatingCell(newB, b.Matches + 1);
            return (newA, newB);
        }
    }

    public IReadOnlyList<EloLeaderboardEntry> GetLeaderboard(
        string seasonId,
        Season? theme = null,
        IReadOnlyDictionary<string, TimeSpan>? hoursWorkedByPersona = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(seasonId);

        lock (_lock)
        {
            bool isAllTime = string.Equals(seasonId, SeasonScopes.AllTime, StringComparison.OrdinalIgnoreCase);
            IEnumerable<KeyValuePair<string, RatingCell>> source = isAllTime
                ? AllTimeAggregate()
                : (_bySeason.TryGetValue(seasonId, out var personas) ? personas : Enumerable.Empty<KeyValuePair<string, RatingCell>>());

            var entries = source
                .Select(kvp =>
                {
                    var raw = kvp.Value.Rating;
                    var display = raw;
                    if (theme is not null && !isAllTime)
                    {
                        display = ApplyBuffs(theme, kvp.Key, display);
                        display = ApplyBurnoutPenalty(theme, kvp.Key, display, hoursWorkedByPersona);
                    }
                    return new EloLeaderboardEntry(
                        Position: 0,
                        Persona: kvp.Key,
                        RawRating: raw,
                        DisplayRating: display,
                        MatchesPlayed: kvp.Value.Matches);
                })
                .OrderByDescending(e => e.DisplayRating)
                .ThenBy(e => e.Persona, StringComparer.Ordinal)
                .ToList();

            for (var i = 0; i < entries.Count; i++)
            {
                entries[i] = entries[i] with { Position = i + 1 };
            }
            return entries;
        }
    }

    private double AllTimeRating(string persona)
    {
        var values = new List<double>();
        foreach (var (_, personas) in _bySeason)
        {
            if (personas.TryGetValue(persona, out var cell))
            {
                values.Add(cell.Rating);
            }
        }
        if (values.Count == 0)
        {
            return EloCalculator.DefaultStartingRating;
        }
        return values.Average();
    }

    private IEnumerable<KeyValuePair<string, RatingCell>> AllTimeAggregate()
    {
        var combined = new Dictionary<string, (double Sum, int Count, int Matches)>(StringComparer.Ordinal);
        foreach (var (_, personas) in _bySeason)
        {
            foreach (var (persona, cell) in personas)
            {
                var (sum, count, matches) = combined.TryGetValue(persona, out var existing) ? existing : (0.0, 0, 0);
                combined[persona] = (sum + cell.Rating, count + 1, matches + cell.Matches);
            }
        }
        foreach (var (persona, agg) in combined)
        {
            yield return new KeyValuePair<string, RatingCell>(
                persona,
                new RatingCell(agg.Sum / agg.Count, agg.Matches));
        }
    }

    private static double ApplyBuffs(Season theme, string persona, double current)
    {
        foreach (var buff in theme.PersonaBuffs)
        {
            if (string.Equals(buff.Persona, persona, StringComparison.OrdinalIgnoreCase))
            {
                current += buff.FlatRatingBonus;
                break;
            }
        }
        return current;
    }

    private static double ApplyBurnoutPenalty(
        Season theme,
        string persona,
        double current,
        IReadOnlyDictionary<string, TimeSpan>? hoursWorkedByPersona)
    {
        if (theme.Weights.BurnoutPenaltyPerHourOverThreshold <= 0)
        {
            return current;
        }
        if (hoursWorkedByPersona is null || !hoursWorkedByPersona.TryGetValue(persona, out var worked))
        {
            return current;
        }
        var over = worked - theme.Weights.BurnoutPenaltyThreshold;
        if (over <= TimeSpan.Zero)
        {
            return current;
        }
        return current - (over.TotalHours * theme.Weights.BurnoutPenaltyPerHourOverThreshold);
    }

    private readonly record struct RatingCell(double Rating, int Matches)
    {
        public RatingCell() : this(EloCalculator.DefaultStartingRating, 0) { }
    }
}
