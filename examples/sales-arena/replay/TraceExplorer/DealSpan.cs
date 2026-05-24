namespace SalesArena.Replay.TraceExplorer;

/// <summary>
/// One OTEL-style span for a deal-lifecycle event. Story ceb3ed81 (SA-04-02).
/// </summary>
/// <remarks>
/// Span emission helpers in the SA-01 agents (CRM, Communications, Calendar,
/// Meeting, Proposal) will produce these records. Until SA-01 ships, the test
/// suite feeds synthetic <see cref="DealSpan"/>s into the explorer to validate
/// hierarchy + causality + renderer output.
/// </remarks>
public sealed record DealSpan
{
    /// <summary>Stable id for this span. Required.</summary>
    public required string SpanId { get; init; }

    /// <summary>Trace-level id (the deal id). Required.</summary>
    public required string TraceId { get; init; }

    /// <summary>Parent span id; null for the root span.</summary>
    public string? ParentSpanId { get; init; }

    /// <summary>Span kind — e.g. <c>crm.transition</c>, <c>touch.email</c>, <c>meeting.held</c>, <c>proposal.sent</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>Short human-readable label, e.g. "qualified-to-engaged".</summary>
    public required string Label { get; init; }

    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset EndUtc { get; init; }

    /// <summary>Span attributes. Must be redacted — no secrets or PII tokens.</summary>
    public IReadOnlyDictionary<string, string> Attributes { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    public TimeSpan Duration => EndUtc - StartUtc;
}
