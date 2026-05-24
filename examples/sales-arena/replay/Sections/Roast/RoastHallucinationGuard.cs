using System.Text.RegularExpressions;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.Sections.Roast;

/// <summary>
/// Refuses roast output that cites event ids not present in the supplied
/// event set, or that has zero citations. The same shape SA-08-03 Persona
/// Diary uses — citation density + provenance check.
/// </summary>
public static class RoastHallucinationGuard
{
    private static readonly Regex CitationPattern = new(@"\[evt:(?<id>[^\]]+)\]", RegexOptions.Compiled);

    /// <summary>Verifies the roast cites only events that exist (or the special <c>none</c> sentinel).</summary>
    public static GuardResult Verify(string roast, IReadOnlyList<ArenaEvent> validEvents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roast);
        ArgumentNullException.ThrowIfNull(validEvents);

        var matches = CitationPattern.Matches(roast);
        if (matches.Count == 0)
        {
            return new GuardResult(false, "no citations found", Array.Empty<string>());
        }

        var validIds = validEvents.Select(e => e.Id.ToString()).ToHashSet(StringComparer.Ordinal);
        var bad = new List<string>();
        foreach (Match match in matches)
        {
            var id = match.Groups["id"].Value;
            if (string.Equals(id, "none", StringComparison.OrdinalIgnoreCase)) continue;
            if (!validIds.Contains(id))
            {
                bad.Add(id);
            }
        }

        if (bad.Count > 0)
        {
            return new GuardResult(false, "hallucinated event ids", bad);
        }

        return new GuardResult(true, "ok", Array.Empty<string>());
    }
}

/// <summary>Outcome of a hallucination-guard check.</summary>
/// <param name="IsOk">True when the roast is admissible.</param>
/// <param name="Reason">Stable reason phrase (e.g. "ok", "no citations found", "hallucinated event ids").</param>
/// <param name="OffendingCitations">Citation ids that don't map to any supplied event. Empty when <see cref="IsOk"/>.</param>
public sealed record GuardResult(bool IsOk, string Reason, IReadOnlyList<string> OffendingCitations);
