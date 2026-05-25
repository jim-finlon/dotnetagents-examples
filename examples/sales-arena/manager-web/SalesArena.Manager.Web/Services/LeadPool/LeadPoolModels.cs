namespace SalesArena.Manager.Web.Services.LeadPool;

public enum LeadPoolFilterChip
{
    All,
    Hot,
    Warm,
    Stuck,
    Nurture,
    Won,
    Lost,
}

public enum LeadPoolSortKey
{
    LeadScore,
    LastTouch,
    Persona,
    Stage,
}

public sealed record LeadPoolActivityEntry(
    DateTimeOffset OccurredAtUtc,
    string Summary);

public sealed record LeadPoolLead(
    string LeadId,
    string Company,
    string Persona,
    string Stage,
    int LeadScore,
    DateTimeOffset LastTouchUtc,
    int IgnoredTouchCount,
    bool HasReply,
    bool IsClosedWon,
    bool IsClosedLost,
    IReadOnlyList<LeadPoolActivityEntry> ActivityLog)
{
    public LeadPoolFilterChip ResolveFilterChip(DateTimeOffset nowUtc)
    {
        if (IsClosedWon)
        {
            return LeadPoolFilterChip.Won;
        }

        if (IsClosedLost)
        {
            return LeadPoolFilterChip.Lost;
        }

        var age = nowUtc - LastTouchUtc;
        if (IgnoredTouchCount >= 3)
        {
            return LeadPoolFilterChip.Stuck;
        }

        if (age > TimeSpan.FromDays(14))
        {
            return LeadPoolFilterChip.Nurture;
        }

        if (age <= TimeSpan.FromHours(24))
        {
            return LeadPoolFilterChip.Hot;
        }

        if (!HasReply)
        {
            return LeadPoolFilterChip.Warm;
        }

        return LeadPoolFilterChip.Hot;
    }
}

public static class LeadPoolQuery
{
    public static IReadOnlyList<LeadPoolLead> ApplyFilter(
        IReadOnlyList<LeadPoolLead> leads,
        LeadPoolFilterChip filter,
        DateTimeOffset nowUtc)
    {
        if (filter == LeadPoolFilterChip.All)
        {
            return leads;
        }

        return leads
            .Where(l => l.ResolveFilterChip(nowUtc) == filter)
            .ToList();
    }

    public static IReadOnlyList<LeadPoolLead> ApplySort(
        IReadOnlyList<LeadPoolLead> leads,
        LeadPoolSortKey sortKey,
        bool descending)
    {
        IEnumerable<LeadPoolLead> ordered = sortKey switch
        {
            LeadPoolSortKey.LeadScore => leads.OrderBy(l => l.LeadScore),
            LeadPoolSortKey.LastTouch => leads.OrderBy(l => l.LastTouchUtc),
            LeadPoolSortKey.Persona => leads.OrderBy(l => l.Persona, StringComparer.OrdinalIgnoreCase),
            LeadPoolSortKey.Stage => leads.OrderBy(l => l.Stage, StringComparer.OrdinalIgnoreCase),
            _ => leads.OrderBy(l => l.LeadId, StringComparer.Ordinal),
        };

        if (descending)
        {
            ordered = ordered.Reverse();
        }

        return ordered.ToList();
    }
}
