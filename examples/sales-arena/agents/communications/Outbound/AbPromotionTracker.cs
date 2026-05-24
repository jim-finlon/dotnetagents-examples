namespace SalesArena.Communications.Outbound;

/// <summary>
/// Tracks sends and replies per persona/channel/variant; promotes a winner after
/// &gt;= <see cref="MinimumSendsPerVariant"/> sends and a 95% two-proportion z-test.
/// </summary>
public sealed class AbPromotionTracker
{
    public const int MinimumSendsPerVariant = 20;

    private readonly Lock _lock = new();
    private readonly Dictionary<AbKey, AbLaneState> _lanes = new();

    public string? GetPromotedVariant(string personaId, string channel)
    {
        var key = new AbKey(personaId, channel);
        lock (_lock)
        {
            return _lanes.TryGetValue(key, out var lane) ? lane.PromotedVariantId : null;
        }
    }

    public string SelectVariantForSend(string personaId, string channel, IReadOnlyList<string> variantIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        ArgumentException.ThrowIfNullOrWhiteSpace(channel);
        ArgumentNullException.ThrowIfNull(variantIds);
        if (variantIds.Count == 0)
        {
            throw new ArgumentException("At least one variant id is required.", nameof(variantIds));
        }

        lock (_lock)
        {
            var lane = GetOrCreateLane(personaId, channel);
            if (lane.PromotedVariantId is { } promoted && variantIds.Contains(promoted, StringComparer.Ordinal))
            {
                return promoted;
            }

            var candidate = variantIds
                .Select(v => (Variant: v, Sends: lane.Stats.GetValueOrDefault(v)?.Sends ?? 0))
                .OrderBy(x => x.Sends)
                .ThenBy(x => x.Variant, StringComparer.Ordinal)
                .First()
                .Variant;
            return candidate;
        }
    }

    public void RecordSend(string personaId, string channel, string variantId)
    {
        lock (_lock)
        {
            var lane = GetOrCreateLane(personaId, channel);
            var stats = GetOrAddStats(lane, variantId);
            stats.Sends++;
            TryPromoteWinner(lane);
        }
    }

    public void RecordReply(string personaId, string channel, string variantId)
    {
        lock (_lock)
        {
            var lane = GetOrCreateLane(personaId, channel);
            var stats = GetOrAddStats(lane, variantId);
            stats.Replies++;
            TryPromoteWinner(lane);
        }
    }

    public VariantStatsSnapshot GetStats(string personaId, string channel, string variantId)
    {
        lock (_lock)
        {
            var lane = GetOrCreateLane(personaId, channel);
            var stats = lane.Stats.GetValueOrDefault(variantId) ?? new VariantStats();
            return new VariantStatsSnapshot(stats.Sends, stats.Replies, lane.PromotedVariantId);
        }
    }

    private AbLaneState GetOrCreateLane(string personaId, string channel)
    {
        var key = new AbKey(personaId, channel);
        if (!_lanes.TryGetValue(key, out var lane))
        {
            lane = new AbLaneState();
            _lanes[key] = lane;
        }

        return lane;
    }

    private static void TryPromoteWinner(AbLaneState lane)
    {
        if (lane.PromotedVariantId is not null)
        {
            return;
        }

        var eligible = lane.Stats
            .Where(kv => kv.Value.Sends >= MinimumSendsPerVariant)
            .Select(kv => (VariantId: kv.Key, kv.Value.Sends, kv.Value.Replies))
            .OrderByDescending(x => ReplyRate(x.Replies, x.Sends))
            .ThenBy(x => x.VariantId, StringComparer.Ordinal)
            .ToList();

        if (eligible.Count < 2)
        {
            return;
        }

        var leader = eligible[0];
        var runnerUp = eligible[1];
        if (!StatisticalSignificance.IsSignificantlyHigher(
                leader.Replies,
                leader.Sends,
                runnerUp.Replies,
                runnerUp.Sends))
        {
            return;
        }

        lane.PromotedVariantId = leader.VariantId;
    }

    private static VariantStats GetOrAddStats(AbLaneState lane, string variantId)
    {
        if (!lane.Stats.TryGetValue(variantId, out var stats))
        {
            stats = new VariantStats();
            lane.Stats[variantId] = stats;
        }

        return stats;
    }

    private static double ReplyRate(int replies, int sends) =>
        sends == 0 ? 0 : (double)replies / sends;

    private sealed class AbLaneState
    {
        public Dictionary<string, VariantStats> Stats { get; } = new(StringComparer.Ordinal);
        public string? PromotedVariantId { get; set; }
    }

    private sealed class VariantStats
    {
        public int Sends { get; set; }
        public int Replies { get; set; }
    }

    private readonly record struct AbKey(string PersonaId, string Channel);
}

public sealed record VariantStatsSnapshot(int Sends, int Replies, string? PromotedVariantId);
