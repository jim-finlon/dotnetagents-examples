using System.Text.RegularExpressions;

namespace SalesArena.Replay.Narrative;

/// <summary>
/// Story d7bcad55 (SA-04-04). Scans rewritten prose against the ledger and
/// flags prose that:
/// <list type="bullet">
///   <item>Cites an event id not present in the ledger.</item>
///   <item>References a persona name not present in the ledger.</item>
///   <item>Contains zero citations (every paragraph must cite at least one event).</item>
/// </list>
/// Pure synchronous check — no LLM call. Tests feed it stub LLM outputs.
/// </summary>
public sealed class HallucinationGuard
{
    /// <summary>Inline citation pattern matched in prose: <c>[event-id]</c>, <c>[evt-17]</c>, etc.</summary>
    public static readonly Regex CitationPattern = new(@"\[([a-z0-9][a-z0-9._-]*)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Scan prose against the ledger and return a structured finding list (empty when clean).</summary>
    public IReadOnlyList<string> Scan(string prose, IReadOnlyList<LedgerEvent> ledger)
    {
        ArgumentNullException.ThrowIfNull(prose);
        ArgumentNullException.ThrowIfNull(ledger);

        var findings = new List<string>();
        var ledgerEventIds = new HashSet<string>(ledger.Select(e => e.EventId), StringComparer.OrdinalIgnoreCase);
        var ledgerPersonas = new HashSet<string>(
            ledger.Where(e => !string.IsNullOrWhiteSpace(e.Persona)).Select(e => e.Persona!),
            StringComparer.OrdinalIgnoreCase);

        var paragraphs = SplitParagraphs(prose);
        if (paragraphs.Count == 0)
        {
            findings.Add("HALLUCINATION:EMPTY_PROSE — narrative rewriter returned no paragraphs.");
            return findings;
        }

        for (var i = 0; i < paragraphs.Count; i++)
        {
            var paragraph = paragraphs[i];
            var matches = CitationPattern.Matches(paragraph);
            if (matches.Count == 0)
            {
                findings.Add($"HALLUCINATION:NO_CITATION — paragraph {i} has no [event-id] citation.");
                continue;
            }
            foreach (Match m in matches)
            {
                var citedId = m.Groups[1].Value;
                if (!ledgerEventIds.Contains(citedId))
                {
                    findings.Add($"HALLUCINATION:UNKNOWN_EVENT_ID — paragraph {i} cites '{citedId}' which is not in the ledger.");
                }
            }
        }

        // Persona-name guard: any capitalized two-letter+ token that looks like a persona name
        // but isn't in the ledger personas is flagged. Heuristic — known-persona list keeps it bounded.
        if (ledgerPersonas.Count > 0)
        {
            var knownPersonaSet = new HashSet<string>(ledgerPersonas, StringComparer.OrdinalIgnoreCase);
            // Add the 6 SA-05-01 baseline personas as default-allowed so the rewriter can use them
            // even when the ledger sample only happens to mention a subset.
            foreach (var name in BaselinePersonas)
                knownPersonaSet.Add(name);

            foreach (Match nameMatch in PersonaCandidatePattern.Matches(prose))
            {
                var candidate = nameMatch.Value;
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                if (knownPersonaSet.Contains(candidate)) continue;
                if (CommonWords.Contains(candidate)) continue;
                findings.Add($"HALLUCINATION:UNKNOWN_PERSONA — prose mentions '{candidate}' which is not a known persona or ledger entity.");
            }
        }

        return findings;
    }

    /// <summary>Split prose into paragraphs (one or more blank lines = paragraph break).</summary>
    public static IReadOnlyList<string> SplitParagraphs(string prose)
    {
        if (string.IsNullOrWhiteSpace(prose)) return Array.Empty<string>();
        var parts = Regex.Split(prose.Trim(), @"\r?\n\s*\r?\n");
        return parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray();
    }

    private static readonly string[] BaselinePersonas =
        { "Roma", "Levene", "Moss", "Aaronow", "Williamson", "Mitch", "Murray" };

    private static readonly Regex PersonaCandidatePattern =
        new(@"\b[A-Z][a-z]{2,}\b", RegexOptions.Compiled);

    // Words that look like proper nouns but are common to dramatic prose; keeps the
    // heuristic from over-flagging legitimate dramatic narration. Operators can expand
    // this list when they curate the rewriter's lexicon.
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "The", "A", "An", "And", "But", "Or", "So", "Then", "Now", "Hour", "Hours",
        "Day", "Days", "Week", "Weeks", "Month", "Year", "January", "February",
        "March", "April", "May", "June", "July", "August", "September", "October",
        "November", "December", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday",
        "Saturday", "Sunday", "Email", "Phone", "LinkedIn", "Zoom", "Markdown",
        "Arena", "Contest", "Replay", "Ledger", "Persona", "Personas",
    };
}
