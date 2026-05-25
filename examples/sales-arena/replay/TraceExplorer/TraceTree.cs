namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// Span-tree projection of all events for one deal. Story ceb3ed81 (SA-04-02).
/// </summary>
/// <param name="DealId">The trace id grouping every span.</param>
/// <param name="Roots">Ordered (chronological by <see cref="DealSpan.StartUtc"/>) root spans.</param>
/// <param name="TotalSpanCount">Total spans included (after paging).</param>
/// <param name="TotalSpanCountUnpaged">Total spans available before paging (so the UI can show "load more").</param>
/// <param name="Causality">For each child span id, the parent span id that caused it. Excludes roots.</param>
public sealed record TraceTree(
    string DealId,
    IReadOnlyList<TraceTreeNode> Roots,
    int TotalSpanCount,
    int TotalSpanCountUnpaged,
    IReadOnlyDictionary<string, string> Causality);

/// <summary>One node in the span tree; a span plus its ordered children.</summary>
public sealed record TraceTreeNode(DealSpan Span, IReadOnlyList<TraceTreeNode> Children);
