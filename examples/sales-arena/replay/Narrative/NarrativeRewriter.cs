using System.Reflection;

namespace SalesArena.Replay.Narrative;

/// <summary>
/// Story d7bcad55 (SA-04-04). Pipes <see cref="INarrativeLlmAdapter"/> output
/// through <see cref="HallucinationGuard"/> and extracts inline citations into
/// the structured <see cref="NarrativeReport.Citations"/> list.
/// </summary>
public sealed class NarrativeRewriter : INarrativeRewriter
{
    private readonly INarrativeLlmAdapter _llm;
    private readonly HallucinationGuard _guard;
    private readonly string _prompt;

    public NarrativeRewriter(INarrativeLlmAdapter llm, HallucinationGuard? guard = null, string? promptOverride = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _guard = guard ?? new HallucinationGuard();
        _prompt = promptOverride ?? LoadDefaultPrompt();
    }

    public async Task<NarrativeRewriteResult> RewriteAsync(
        string reportMarkdown,
        IReadOnlyList<LedgerEvent> ledger,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportMarkdown))
        {
            return NarrativeRewriteResult.Rejected(
                "report markdown is required",
                "MISSING_REPORT",
                "Provide the SA-04-01 Markdown report text to be rewritten.");
        }
        ArgumentNullException.ThrowIfNull(ledger);
        if (ledger.Count == 0)
        {
            return NarrativeRewriteResult.Rejected(
                "ledger is empty",
                "MISSING_LEDGER",
                "Provide at least one LedgerEvent so the rewriter has something to cite.");
        }

        var prose = await _llm.RewriteAsync(_prompt, reportMarkdown, ledger, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(prose))
        {
            return NarrativeRewriteResult.Rejected(
                "LLM adapter returned empty prose",
                "EMPTY_LLM_RESPONSE",
                "The configured INarrativeLlmAdapter did not return a usable Markdown rewrite.");
        }

        var findings = _guard.Scan(prose, ledger);
        if (findings.Count > 0)
        {
            return NarrativeRewriteResult.Rejected(
                $"hallucination guard flagged {findings.Count} issue(s)",
                "HALLUCINATION_GUARD_FAILED",
                "Inspect the findings list; common fixes are tightening the prompt's citation rules or expanding the ledger pre-pass.",
                findings);
        }

        var citations = ExtractCitations(prose, ledger);
        return NarrativeRewriteResult.Recorded(new NarrativeReport(prose, citations));
    }

    private static IReadOnlyList<NarrativeCitation> ExtractCitations(string prose, IReadOnlyList<LedgerEvent> ledger)
    {
        var ledgerIds = new HashSet<string>(ledger.Select(e => e.EventId), StringComparer.OrdinalIgnoreCase);
        var paragraphs = HallucinationGuard.SplitParagraphs(prose);
        var citations = new List<NarrativeCitation>();
        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in HallucinationGuard.CitationPattern.Matches(paragraph))
            {
                var id = match.Groups[1].Value;
                if (!ledgerIds.Contains(id)) continue; // guard already would have caught this; defense in depth
                if (!seen.Add(id)) continue;
                citations.Add(new NarrativeCitation(i, id, match.Value));
            }
        }
        return citations;
    }

    private static string LoadDefaultPrompt()
    {
        // The shipped prompt-template file lives next to the assembly under
        // templates/narrative-rewriter.prompt.md (see AC). Tests use the inline
        // fallback string to keep them deterministic and independent of disk layout.
        var asmDir = Path.GetDirectoryName(typeof(NarrativeRewriter).Assembly.Location);
        if (!string.IsNullOrEmpty(asmDir))
        {
            var path = Path.Combine(asmDir, "templates", "narrative-rewriter.prompt.md");
            if (File.Exists(path))
            {
                try { return File.ReadAllText(path); }
                catch (IOException) { /* fall through to inline fallback */ }
            }
        }
        return InlineFallbackPrompt;
    }

    /// <summary>
    /// Inline copy of the prompt template; the file at
    /// <c>templates/narrative-rewriter.prompt.md</c> is the operator-editable source.
    /// </summary>
    private const string InlineFallbackPrompt = """
        You are rewriting a structured replay report as dramatic narrative prose for
        operators to share. Strict rules:

        1. Every paragraph must cite at least one ledger event id using the form `[event-id]`.
        2. You may only mention persona names, event kinds, and counts that appear in the
           ledger or the structured report. Do not invent new entities, dates, or quotes.
        3. Keep paragraphs short (1-3 sentences). Operators will edit before sharing.
        4. Output Markdown only — no HTML, no script.
        """;
}
