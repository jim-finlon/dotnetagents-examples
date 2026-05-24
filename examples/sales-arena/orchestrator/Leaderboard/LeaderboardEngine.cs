using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Orchestrator.Leaderboard;

/// <summary>
/// Default <see cref="ILeaderboardEngine"/> implementation. Aggregates ledger
/// events into per-persona stats, applies the chosen scoring config, assigns
/// tiers, and fires <see cref="LeaderboardChanged"/> when the ranking shifts.
///
/// <para>Tie-breaking (deterministic for replay):</para>
/// <list type="number">
///   <item>Higher score wins.</item>
///   <item>Higher revenue wins.</item>
///   <item>Higher deals-won wins.</item>
///   <item>Persona name ascending (Ordinal).</item>
/// </list>
///
/// <para>Tier assignment for N personas:</para>
/// <list type="bullet">
///   <item>Position 1 → <see cref="LeaderboardTier.Cadillac"/></item>
///   <item>Positions 2..ceil(N/2) → <see cref="LeaderboardTier.SteakKnives"/></item>
///   <item>Remaining positions → <see cref="LeaderboardTier.YouAreFired"/></item>
/// </list>
/// </summary>
public sealed class LeaderboardEngine : ILeaderboardEngine
{
    private readonly IArenaLedger _ledger;

    // Per-config last computed leaderboard, for change detection on the next compute.
    private readonly Dictionary<string, Leaderboard> _previousByConfig =
        new(StringComparer.Ordinal);

    private readonly object _previousLock = new();

    public LeaderboardEngine(IArenaLedger ledger)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public event EventHandler<LeaderboardChangedEventArgs>? LeaderboardChanged;

    public async Task<Leaderboard> ComputeAsync(
        string contestId,
        IScoringConfig scoring,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        ArgumentNullException.ThrowIfNull(scoring);

        var stats = await AggregateStatsAsync(contestId, asOfUtc, cancellationToken).ConfigureAwait(false);
        var ranked = RankAndTier(stats, scoring);
        var leaderboard = new Leaderboard(contestId, scoring.Id, asOfUtc, ranked);

        Leaderboard? previous;
        lock (_previousLock)
        {
            _previousByConfig.TryGetValue(scoring.Id, out previous);
            _previousByConfig[scoring.Id] = leaderboard;
        }

        if (previous is not null)
        {
            var changes = ComputeChanges(previous, leaderboard);
            if (changes.Count > 0)
            {
                LeaderboardChanged?.Invoke(this, new LeaderboardChangedEventArgs(previous, leaderboard, changes));
            }
        }

        return leaderboard;
    }

    // ---- aggregation -----------------------------------------------------

    private async Task<IReadOnlyList<PersonaStats>> AggregateStatsAsync(
        string contestId,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken)
    {
        var revenue = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var wins = new Dictionary<string, int>(StringComparer.Ordinal);
        var losses = new Dictionary<string, int>(StringComparer.Ordinal);
        var touches = new Dictionary<string, int>(StringComparer.Ordinal);
        var leadsAssigned = new Dictionary<string, int>(StringComparer.Ordinal);
        var leadsResearched = new Dictionary<string, int>(StringComparer.Ordinal);
        var meetingsHeld = new Dictionary<string, int>(StringComparer.Ordinal);
        var assignedAtByLead = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        var closeDurations = new Dictionary<string, List<TimeSpan>>(StringComparer.Ordinal);

        var filter = new ArenaEventFilter(ContestId: contestId, ToUtc: asOfUtc);
        await foreach (var evt in _ledger.QueryAsync(filter, cancellationToken).ConfigureAwait(false))
        {
            switch (evt.Kind)
            {
                case ArenaEventKinds.LeadAssigned:
                {
                    if (string.IsNullOrEmpty(evt.Persona) || string.IsNullOrEmpty(evt.LeadId)) break;
                    Increment(leadsAssigned, evt.Persona);
                    assignedAtByLead[evt.LeadId] = evt.OccurredAtUtc;
                    break;
                }
                case ArenaEventKinds.LeadResearched:
                {
                    if (string.IsNullOrEmpty(evt.Persona)) break;
                    Increment(leadsResearched, evt.Persona);
                    break;
                }
                case ArenaEventKinds.TouchSent:
                {
                    if (string.IsNullOrEmpty(evt.Persona)) break;
                    Increment(touches, evt.Persona);
                    break;
                }
                case ArenaEventKinds.MeetingHeld:
                {
                    if (string.IsNullOrEmpty(evt.Persona)) break;
                    Increment(meetingsHeld, evt.Persona);
                    break;
                }
                case ArenaEventKinds.DealClosed:
                {
                    if (string.IsNullOrEmpty(evt.Persona)) break;
                    var payload = evt.GetPayload<DealClosedPayload>();
                    if (payload is null) break;

                    if (string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
                    {
                        Increment(wins, evt.Persona);
                        if (payload.ValueUsd is { } value)
                        {
                            revenue.TryGetValue(evt.Persona, out var current);
                            revenue[evt.Persona] = current + value;
                        }

                        if (!string.IsNullOrEmpty(evt.LeadId)
                            && assignedAtByLead.TryGetValue(evt.LeadId, out var assignedAt))
                        {
                            if (!closeDurations.TryGetValue(evt.Persona, out var list))
                            {
                                list = new List<TimeSpan>();
                                closeDurations[evt.Persona] = list;
                            }
                            list.Add(evt.OccurredAtUtc - assignedAt);
                        }
                    }
                    else if (string.Equals(payload.Outcome, "Lost", StringComparison.OrdinalIgnoreCase))
                    {
                        Increment(losses, evt.Persona);
                    }
                    break;
                }
            }
        }

        // Build the union of personas that appear in any bucket.
        var personas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in revenue.Keys) personas.Add(key);
        foreach (var key in wins.Keys) personas.Add(key);
        foreach (var key in losses.Keys) personas.Add(key);
        foreach (var key in touches.Keys) personas.Add(key);
        foreach (var key in leadsAssigned.Keys) personas.Add(key);
        foreach (var key in leadsResearched.Keys) personas.Add(key);
        foreach (var key in meetingsHeld.Keys) personas.Add(key);

        var stats = new List<PersonaStats>(personas.Count);
        foreach (var persona in personas)
        {
            TimeSpan? avgTtc = null;
            if (closeDurations.TryGetValue(persona, out var durations) && durations.Count > 0)
            {
                avgTtc = TimeSpan.FromTicks((long)durations.Average(d => d.Ticks));
            }

            stats.Add(new PersonaStats(
                Persona: persona,
                RevenueUsd: revenue.GetValueOrDefault(persona),
                DealsWon: wins.GetValueOrDefault(persona),
                DealsLost: losses.GetValueOrDefault(persona),
                TouchesSent: touches.GetValueOrDefault(persona),
                LeadsAssigned: leadsAssigned.GetValueOrDefault(persona),
                LeadsResearched: leadsResearched.GetValueOrDefault(persona),
                MeetingsHeld: meetingsHeld.GetValueOrDefault(persona),
                AverageTimeToClose: avgTtc));
        }
        return stats;
    }

    // ---- ranking + tier --------------------------------------------------

    private static IReadOnlyList<LeaderboardRow> RankAndTier(
        IReadOnlyList<PersonaStats> stats,
        IScoringConfig scoring)
    {
        if (stats.Count == 0) return Array.Empty<LeaderboardRow>();

        var scored = stats
            .Select(s => (Stats: s, Score: scoring.ComputeScore(s)))
            .OrderByDescending(t => t.Score)
            .ThenByDescending(t => t.Stats.RevenueUsd)
            .ThenByDescending(t => t.Stats.DealsWon)
            .ThenBy(t => t.Stats.Persona, StringComparer.Ordinal)
            .ToList();

        var rows = new List<LeaderboardRow>(scored.Count);
        var steakKnivesEnd = Math.Max(2, (int)Math.Ceiling(scored.Count / 2.0));
        for (var i = 0; i < scored.Count; i++)
        {
            var position = i + 1;
            var tier = position switch
            {
                1 => LeaderboardTier.Cadillac,
                _ when position <= steakKnivesEnd => LeaderboardTier.SteakKnives,
                _ => LeaderboardTier.YouAreFired,
            };

            rows.Add(new LeaderboardRow(
                Position: position,
                Tier: tier,
                Persona: scored[i].Stats.Persona,
                Score: scored[i].Score,
                RevenueUsd: scored[i].Stats.RevenueUsd,
                DealsWon: scored[i].Stats.DealsWon,
                DealsLost: scored[i].Stats.DealsLost,
                WinRate: scored[i].Stats.WinRate));
        }
        return rows;
    }

    private static IReadOnlyList<PersonaTierChange> ComputeChanges(Leaderboard previous, Leaderboard current)
    {
        var prevByPersona = previous.Entries.ToDictionary(e => e.Persona, StringComparer.Ordinal);
        var changes = new List<PersonaTierChange>();
        foreach (var row in current.Entries)
        {
            if (!prevByPersona.TryGetValue(row.Persona, out var prevRow))
            {
                changes.Add(new PersonaTierChange(
                    row.Persona, FromPosition: 0, ToPosition: row.Position,
                    FromTier: LeaderboardTier.YouAreFired, ToTier: row.Tier));
                continue;
            }
            if (prevRow.Position != row.Position || prevRow.Tier != row.Tier)
            {
                changes.Add(new PersonaTierChange(
                    row.Persona, prevRow.Position, row.Position, prevRow.Tier, row.Tier));
            }
        }
        return changes;
    }

    private static void Increment(Dictionary<string, int> bucket, string key)
    {
        bucket.TryGetValue(key, out var current);
        bucket[key] = current + 1;
    }
}
