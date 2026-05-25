namespace SalesArena.Replay.Narrative;

/// <summary>
/// Story d7bcad55 (SA-04-04). Rewrites an SA-04-01 Markdown replay report as
/// dramatic prose with inline citations to ledger event ids.
/// </summary>
public interface INarrativeRewriter
{
    /// <summary>
    /// Rewrite <paramref name="reportMarkdown"/> as narrative prose. Every paragraph
    /// must cite at least one event id present in <paramref name="ledger"/>; if the
    /// hallucination guard fires, the result is <c>Rejected</c> with the findings
    /// list and no prose is returned to the caller.
    /// </summary>
    Task<NarrativeRewriteResult> RewriteAsync(
        string reportMarkdown,
        IReadOnlyList<LedgerEvent> ledger,
        CancellationToken cancellationToken = default);
}
