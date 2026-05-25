namespace SalesArena.Communications.Inbound;

/// <summary>
/// Fuzzy-matches inbound sender hints to CRM / lead-pack prospect records.
/// </summary>
public sealed class CrmCorrelator
{
    private readonly IReadOnlyList<CrmProspectIndexEntry> _index;

    public CrmCorrelator(IEnumerable<CrmProspectIndexEntry> index)
    {
        ArgumentNullException.ThrowIfNull(index);
        _index = index.ToList();
    }

    public CrmCorrelationResult Correlate(InboundMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var scores = new List<(string LeadId, double Score)>();
        foreach (var entry in _index)
        {
            var score = ScoreMatch(message, entry);
            if (score > 0)
            {
                scores.Add((entry.LeadId, score));
            }
        }

        if (scores.Count == 0)
        {
            return new CrmCorrelationResult(CrmCorrelationStatus.Miss, null, 0, []);
        }

        scores.Sort((a, b) => b.Score.CompareTo(a.Score));
        var top = scores[0];
        var ties = scores
            .Where(s => Math.Abs(s.Score - top.Score) < 0.01)
            .Select(s => s.LeadId)
            .ToList();

        if (ties.Count > 1)
        {
            return new CrmCorrelationResult(CrmCorrelationStatus.Ambiguous, null, top.Score, ties);
        }

        return new CrmCorrelationResult(CrmCorrelationStatus.Matched, top.LeadId, top.Score, [top.LeadId]);
    }

    private static double ScoreMatch(InboundMessage message, CrmProspectIndexEntry entry)
    {
        var score = 0.0;
        if (!string.IsNullOrWhiteSpace(message.FromEmail)
            && !string.IsNullOrWhiteSpace(entry.Email)
            && string.Equals(message.FromEmail, entry.Email, StringComparison.OrdinalIgnoreCase))
        {
            score += 1.0;
        }

        if (!string.IsNullOrWhiteSpace(message.CompanyHint)
            && ContainsFuzzy(message.CompanyHint, entry.Company))
        {
            score += 0.45;
        }

        if (!string.IsNullOrWhiteSpace(message.FromName))
        {
            var name = message.FromName;
            if (!string.IsNullOrWhiteSpace(entry.FirstName) && ContainsFuzzy(name, entry.FirstName))
            {
                score += 0.25;
            }

            if (!string.IsNullOrWhiteSpace(entry.LastName) && ContainsFuzzy(name, entry.LastName))
            {
                score += 0.25;
            }
        }

        return Math.Min(score, 1.0);
    }

    private static bool ContainsFuzzy(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase)
        || needle.Contains(haystack, StringComparison.OrdinalIgnoreCase);
}
