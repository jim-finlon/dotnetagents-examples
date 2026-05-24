using System.Text.RegularExpressions;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Training.Diary;

/// <summary>
/// Refuses diary output that cites event ids not present in the supplied
/// event set, or that doesn't cite at least 2 events (per AC: "references
/// 2+ specific events").
///
/// <para>Same shape as the Replay's RoastHallucinationGuard (SA-08-07) —
/// kept separate here to avoid cross-project coupling. If a third hallucination
/// guard surfaces in another module, extract a shared <c>EventCitationGuard</c>
/// in SA-01-08 (Training Agent) where both live close together.</para>
/// </summary>
public static class DiaryHallucinationGuard
{
    private static readonly Regex CitationPattern = new(@"\[evt:(?<id>[^\]]+)\]", RegexOptions.Compiled);

    /// <summary>
    /// Verifies the diary body. Requires <paramref name="minCitations"/> or
    /// more distinct citations, all referencing event ids in
    /// <paramref name="validEvents"/>. The special <c>[evt:none]</c> sentinel
    /// is allowed once for empty-events graceful days.
    /// </summary>
    public static GuardResult Verify(string body, IReadOnlyList<ArenaEvent> validEvents, int minCitations = 2)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentNullException.ThrowIfNull(validEvents);
        ArgumentOutOfRangeException.ThrowIfNegative(minCitations);

        var matches = CitationPattern.Matches(body);
        if (matches.Count == 0)
        {
            return new GuardResult(false, "no citations found", Array.Empty<string>());
        }

        var validIds = validEvents.Select(e => e.Id.ToString()).ToHashSet(StringComparer.Ordinal);
        var bad = new List<string>();
        var noneSentinelCount = 0;
        var distinctCited = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value;
            if (string.Equals(id, "none", StringComparison.OrdinalIgnoreCase))
            {
                noneSentinelCount++;
                continue;
            }
            if (!validIds.Contains(id))
            {
                bad.Add(id);
            }
            else
            {
                distinctCited.Add(id);
            }
        }

        if (bad.Count > 0)
        {
            return new GuardResult(false, "hallucinated event ids", bad);
        }

        // For events-less days the "none" sentinel substitutes; the entry is
        // admitted as long as the body acknowledges the empty day.
        var citationCount = distinctCited.Count + (validEvents.Count == 0 ? noneSentinelCount : 0);
        if (citationCount < minCitations)
        {
            return new GuardResult(
                false,
                $"insufficient citations (have {citationCount}, need {minCitations})",
                Array.Empty<string>());
        }

        return new GuardResult(true, "ok", distinctCited.ToList());
    }
}

/// <summary>Outcome of a diary hallucination check.</summary>
public sealed record GuardResult(bool IsOk, string Reason, IReadOnlyList<string> CitedOrOffending);
