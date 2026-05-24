using SalesArena.Replay;

namespace SalesArena.Manager.Web.Services.Replay;

public sealed record ReplayContestSummary(
    string ContestId,
    string DisplayName,
    DateTimeOffset EndedAtUtc,
    string? WinningPersona,
    int DealCount);

public sealed record ReplayDealEvent(
    DateTimeOffset OccurredAtUtc,
    string Kind,
    string Summary);

public sealed record ReplayDealFocus(
    string LeadId,
    string? Persona,
    IReadOnlyList<ReplayDealEvent> Timeline,
    IReadOnlyList<ReplayHighlight> Highlights);

public static class ReplayBrowserQuery
{
    public static IReadOnlyList<ReplayHighlight> HighlightsForLead(
        IReadOnlyList<ReplayHighlight> highlights,
        string leadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);
        return highlights
            .Where(h => string.Equals(h.LeadId, leadId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public static ReplaySection? SectionForLead(
        IReadOnlyList<ReplaySection> sections,
        string leadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);
        return sections.FirstOrDefault(s =>
            s.Markdown.Contains(leadId, StringComparison.OrdinalIgnoreCase));
    }
}
