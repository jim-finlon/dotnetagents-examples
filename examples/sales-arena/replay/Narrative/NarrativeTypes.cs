namespace SalesArena.Replay.Narrative;

/// <summary>Story d7bcad55 (SA-04-04). One event in the contest ledger.</summary>
/// <param name="EventId">Stable id used in citations.</param>
/// <param name="OccurredAtUtc">When the event happened.</param>
/// <param name="Kind">Event kind, e.g. <c>touch.email</c>, <c>meeting.held</c>.</param>
/// <param name="Summary">Short factual one-liner the rewriter is allowed to quote.</param>
/// <param name="Persona">Optional persona name (Roma / Levene / Moss / etc.).</param>
public sealed record LedgerEvent(
    string EventId,
    DateTimeOffset OccurredAtUtc,
    string Kind,
    string Summary,
    string? Persona = null);

/// <summary>Story d7bcad55 (SA-04-04). The narrative-rewritten replay.</summary>
/// <param name="Prose">Operator-editable Markdown — paragraphs separated by blank lines.</param>
/// <param name="Citations">Inline citations linking prose paragraphs to ledger event ids.</param>
public sealed record NarrativeReport(string Prose, IReadOnlyList<NarrativeCitation> Citations);

/// <summary>One citation tying a paragraph to a ledger event.</summary>
/// <param name="ParagraphIndex">Zero-based index of the paragraph in <see cref="NarrativeReport.Prose"/>.</param>
/// <param name="EventId">Ledger event id referenced.</param>
/// <param name="Anchor">The substring from the prose that triggered the citation (e.g. <c>[evt-17]</c>).</param>
public sealed record NarrativeCitation(int ParagraphIndex, string EventId, string Anchor);

/// <summary>Story d7bcad55 (SA-04-04). Stable result envelope.</summary>
public sealed record NarrativeRewriteResult(
    bool Success,
    NarrativeReport? Report,
    string? Error,
    string? ErrorCode,
    string? Guidance,
    IReadOnlyList<string> HallucinationFindings)
{
    public static NarrativeRewriteResult Recorded(NarrativeReport report) =>
        new(true, report, null, null, null, Array.Empty<string>());

    public static NarrativeRewriteResult Rejected(string error, string errorCode, string guidance, IReadOnlyList<string>? findings = null) =>
        new(false, null, error, errorCode, guidance, findings ?? Array.Empty<string>());
}

/// <summary>Story d7bcad55 (SA-04-04). LLM adapter seam for unit testing.</summary>
public interface INarrativeLlmAdapter
{
    /// <summary>
    /// Send the canonical prompt + the structured input (markdown report + ledger json)
    /// to a local LLM and return the rewritten prose Markdown.
    /// </summary>
    Task<string> RewriteAsync(string prompt, string reportMarkdown, IReadOnlyList<LedgerEvent> ledger, CancellationToken cancellationToken = default);
}
