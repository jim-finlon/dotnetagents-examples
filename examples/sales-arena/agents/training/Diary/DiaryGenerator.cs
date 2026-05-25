using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Training.Diary;

/// <summary>
/// Default <see cref="IDiaryGenerator"/>. Pulls per-persona events for the
/// requested day from the Ledger, lets the configured <see cref="IDiaryWriter"/>
/// draft the body, runs the hallucination guard, computes word-count + cited
/// ids, prepends frontmatter, and persists via the <see cref="IDiaryStore"/>.
/// </summary>
public sealed class DiaryGenerator : IDiaryGenerator
{
    private readonly IArenaLedger _ledger;
    private readonly IDiaryWriter _writer;
    private readonly IDiaryStore? _store;
    private readonly TimeProvider _time;
    private readonly int _minCitations;

    public DiaryGenerator(
        IArenaLedger ledger,
        IDiaryWriter? writer = null,
        IDiaryStore? store = null,
        TimeProvider? time = null,
        int minCitations = 2)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _writer = writer ?? new StubDiaryWriter();
        _store = store;
        _time = time ?? TimeProvider.System;
        _minCitations = minCitations;
    }

    public async Task<DiaryEntry> GenerateAsync(DiaryGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DayEndUtc <= request.DayStartUtc)
            throw new ArgumentException("DayEndUtc must be strictly after DayStartUtc.", nameof(request));

        var events = await PullDayEventsAsync(request, cancellationToken).ConfigureAwait(false);
        var stats = ComputeStats(events);
        var ctx = new DiaryDayContext(
            Persona: request.Persona,
            Day: request.Day,
            LeaderboardPosition: request.LeaderboardPosition,
            TotalPositions: request.TotalPositions,
            DealsClosedToday: stats.Wins,
            DealsLostToday: stats.Losses,
            RevenueToday: stats.Revenue,
            Events: events);

        var body = await _writer.WriteEntryAsync(ctx, cancellationToken).ConfigureAwait(false);

        var guard = DiaryHallucinationGuard.Verify(body, events, _minCitations);
        if (!guard.IsOk)
        {
            throw new DiaryGuardException(
                $"Diary entry refused by hallucination guard: {guard.Reason}.",
                guard);
        }

        var generatedAt = _time.GetUtcNow();
        var markdown = AssembleMarkdown(request, ctx, body, generatedAt);
        var entry = new DiaryEntry(
            ContestId: request.ContestId,
            Persona: request.Persona,
            Day: request.Day,
            GeneratedAtUtc: generatedAt,
            LeaderboardPosition: request.LeaderboardPosition,
            WordCount: CountWords(body),
            CitedEventIds: guard.CitedOrOffending,
            Markdown: markdown);

        if (_store is not null)
        {
            await _store.SaveAsync(entry, cancellationToken).ConfigureAwait(false);
        }

        return entry;
    }

    // ---- internals ------------------------------------------------------

    private async Task<IReadOnlyList<ArenaEvent>> PullDayEventsAsync(DiaryGenerationRequest request, CancellationToken cancellationToken)
    {
        var filter = new ArenaEventFilter(
            ContestId: request.ContestId,
            Persona: request.Persona,
            FromUtc: request.DayStartUtc,
            ToUtc: request.DayEndUtc);

        var list = new List<ArenaEvent>();
        await foreach (var evt in _ledger.QueryAsync(filter, cancellationToken).ConfigureAwait(false))
        {
            list.Add(evt);
        }
        return list;
    }

    private static (int Wins, int Losses, decimal Revenue) ComputeStats(IReadOnlyList<ArenaEvent> events)
    {
        var wins = 0;
        var losses = 0;
        var revenue = 0m;
        foreach (var evt in events.Where(e => e.Kind == ArenaEventKinds.DealClosed))
        {
            var payload = evt.GetPayload<DealClosedPayload>();
            if (payload is null) continue;
            if (string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
            {
                wins++;
                revenue += payload.ValueUsd ?? 0m;
            }
            else if (string.Equals(payload.Outcome, "Lost", StringComparison.OrdinalIgnoreCase))
            {
                losses++;
            }
        }
        return (wins, losses, revenue);
    }

    private static string AssembleMarkdown(DiaryGenerationRequest request, DiaryDayContext ctx, string body, DateTimeOffset generatedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"contest: {request.ContestId}");
        sb.AppendLine($"persona: {request.Persona}");
        sb.AppendLine($"day: {request.Day}");
        sb.AppendLine($"generated_at_utc: {generatedAt.ToString("u", CultureInfo.InvariantCulture)}");
        sb.AppendLine($"leaderboard_position: {request.LeaderboardPosition}");
        sb.AppendLine($"deals_won: {ctx.DealsClosedToday}");
        sb.AppendLine($"deals_lost: {ctx.DealsLostToday}");
        sb.AppendLine($"revenue_usd: {ctx.RevenueToday.ToString(CultureInfo.InvariantCulture)}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {request.Persona} — Day {request.Day}");
        sb.AppendLine();
        sb.AppendLine(body);
        return sb.ToString();
    }

    private static int CountWords(string body) =>
        body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
