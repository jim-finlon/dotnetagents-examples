namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// Story ceb3ed81 (SA-04-02). Loads every span emitted for a deal and projects
/// them into a <see cref="TraceTree"/> that the replay UI can render.
/// </summary>
public interface IDealTraceExplorer
{
    /// <summary>
    /// Build a span tree for one deal. Caller passes paging via
    /// <paramref name="options"/>; renderer surfaces "load more" when
    /// the unpaged total exceeds the paged total.
    /// </summary>
    Task<TraceTree> GetTraceAsync(
        string dealId,
        TraceExplorerOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Story ceb3ed81 (SA-04-02). Source of <see cref="DealSpan"/> records.
/// SA-01 agents emit spans via DotNetAgents.Observability; an adapter wraps
/// them as this contract. Tests use an in-memory fake.
/// </summary>
public interface IDealSpanSource
{
    Task<IReadOnlyList<DealSpan>> GetSpansAsync(string dealId, CancellationToken cancellationToken = default);
}

/// <summary>Optional paging + filtering knobs.</summary>
public sealed record TraceExplorerOptions
{
    /// <summary>Maximum number of spans included in the projection (default 200 — paged).</summary>
    public int MaxSpans { get; init; } = 200;

    /// <summary>If true, sort children by <see cref="DealSpan.StartUtc"/> ascending. Default true.</summary>
    public bool SortChronologically { get; init; } = true;
}
