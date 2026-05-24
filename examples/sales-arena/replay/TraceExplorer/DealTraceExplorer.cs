namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// Story ceb3ed81 (SA-04-02). Implementation of <see cref="IDealTraceExplorer"/>.
/// Pure assembly logic — pulls spans from an <see cref="IDealSpanSource"/>,
/// validates the parent/child link graph, builds the tree, and paginates.
/// </summary>
public sealed class DealTraceExplorer : IDealTraceExplorer
{
    private readonly IDealSpanSource _source;

    public DealTraceExplorer(IDealSpanSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async Task<TraceTree> GetTraceAsync(
        string dealId,
        TraceExplorerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dealId))
            throw new ArgumentException("dealId is required", nameof(dealId));

        var opts = options ?? new TraceExplorerOptions();
        if (opts.MaxSpans <= 0)
            throw new ArgumentException("MaxSpans must be positive", nameof(options));

        var all = await _source.GetSpansAsync(dealId, cancellationToken).ConfigureAwait(false);
        var spansForDeal = all
            .Where(s => string.Equals(s.TraceId, dealId, StringComparison.Ordinal))
            .ToArray();
        var totalUnpaged = spansForDeal.Length;

        // Paging: keep the chronologically-earliest MaxSpans so the operator can
        // start a drill-down from the deal open and click "load more" later.
        var paged = opts.SortChronologically
            ? spansForDeal.OrderBy(s => s.StartUtc).Take(opts.MaxSpans).ToArray()
            : spansForDeal.Take(opts.MaxSpans).ToArray();

        return Build(dealId, paged, totalUnpaged, opts.SortChronologically);
    }

    /// <summary>
    /// Pure tree assembly — useful for callers that already hold a span list
    /// (e.g. an HTTP handler that filtered upstream).
    /// </summary>
    public static TraceTree Build(
        string dealId,
        IReadOnlyList<DealSpan> spans,
        int totalUnpagedCount,
        bool sortChronologically = true)
    {
        if (string.IsNullOrWhiteSpace(dealId))
            throw new ArgumentException("dealId is required", nameof(dealId));
        ArgumentNullException.ThrowIfNull(spans);
        if (totalUnpagedCount < spans.Count)
            throw new ArgumentException("totalUnpagedCount must be >= spans.Count", nameof(totalUnpagedCount));

        var byId = new Dictionary<string, DealSpan>(StringComparer.Ordinal);
        foreach (var s in spans)
        {
            if (!string.Equals(s.TraceId, dealId, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"span '{s.SpanId}' has TraceId '{s.TraceId}' but expected '{dealId}'", nameof(spans));
            if (!byId.TryAdd(s.SpanId, s))
                throw new ArgumentException($"duplicate spanId '{s.SpanId}'", nameof(spans));
        }

        var causality = new Dictionary<string, string>(StringComparer.Ordinal);
        var childrenByParent = new Dictionary<string, List<DealSpan>>(StringComparer.Ordinal);
        var roots = new List<DealSpan>();
        foreach (var s in spans)
        {
            if (s.ParentSpanId is null)
            {
                roots.Add(s);
                continue;
            }
            if (!byId.ContainsKey(s.ParentSpanId))
            {
                // Parent fell outside the paged window — treat as root for rendering
                // but still record causality so the UI can show "parent on previous page".
                roots.Add(s);
                causality[s.SpanId] = s.ParentSpanId;
                continue;
            }
            causality[s.SpanId] = s.ParentSpanId;
            if (!childrenByParent.TryGetValue(s.ParentSpanId, out var list))
            {
                list = new List<DealSpan>();
                childrenByParent[s.ParentSpanId] = list;
            }
            list.Add(s);
        }

        if (sortChronologically)
        {
            roots = roots.OrderBy(r => r.StartUtc).ToList();
            foreach (var key in childrenByParent.Keys.ToList())
                childrenByParent[key] = childrenByParent[key].OrderBy(c => c.StartUtc).ToList();
        }

        var rootNodes = roots.Select(r => BuildNode(r, childrenByParent, sortChronologically)).ToArray();
        return new TraceTree(dealId, rootNodes, spans.Count, totalUnpagedCount, causality);
    }

    private static TraceTreeNode BuildNode(
        DealSpan span,
        IReadOnlyDictionary<string, List<DealSpan>> childrenByParent,
        bool sortChronologically)
    {
        if (!childrenByParent.TryGetValue(span.SpanId, out var direct))
            return new TraceTreeNode(span, Array.Empty<TraceTreeNode>());
        var ordered = sortChronologically ? direct.OrderBy(c => c.StartUtc).ToArray() : direct.ToArray();
        var children = ordered.Select(c => BuildNode(c, childrenByParent, sortChronologically)).ToArray();
        return new TraceTreeNode(span, children);
    }
}
