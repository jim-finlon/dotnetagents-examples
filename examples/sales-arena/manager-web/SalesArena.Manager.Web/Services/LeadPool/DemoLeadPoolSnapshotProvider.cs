namespace SalesArena.Manager.Web.Services.LeadPool;

/// <summary>
/// Deterministic demo lead pool until orchestrator snapshots wire in (SA-03-04).
/// </summary>
public sealed class DemoLeadPoolSnapshotProvider : ILeadPoolSnapshotProvider
{
    private static readonly string[] Personas =
    [
        "romano", "moss", "aaronow", "levene", "williamson", "harris",
    ];

    private static readonly string[] Stages =
    [
        "Prospect", "Qualified", "Proposal", "Negotiation",
    ];

    public Task<IReadOnlyList<LeadPoolLead>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var now = DateTimeOffset.UtcNow;
        var leads = new List<LeadPoolLead>(capacity: 48);

        for (var i = 0; i < 48; i++)
        {
            var leadId = $"L-{1000 + i}";
            var persona = Personas[i % Personas.Length];
            var stage = Stages[i % Stages.Length];
            var ignored = i % 11 == 0 ? 3 + (i % 2) : i % 7;
            var hasReply = i % 5 != 0 && ignored < 3;
            var isWon = i == 2 || i == 17;
            var isLost = i == 5 || i == 29;
            var hoursAgo = i switch
            {
                < 6 => i * 2,
                < 12 => 30 + i,
                < 20 => 24 * 3 + i,
                < 30 => 24 * 10 + i,
                _ => 24 * 20 + i,
            };

            var lastTouch = now.AddHours(-hoursAgo);
            var score = 40 + (i * 7 % 55);
            var activity = BuildActivityLog(leadId, persona, lastTouch, ignored, hasReply, isWon, isLost);

            leads.Add(new LeadPoolLead(
                leadId,
                $"Acme {leadId}",
                persona,
                isWon ? "Closed Won" : isLost ? "Closed Lost" : stage,
                score,
                lastTouch,
                ignored,
                hasReply,
                isWon,
                isLost,
                activity));
        }

        return Task.FromResult<IReadOnlyList<LeadPoolLead>>(leads);
    }

    private static IReadOnlyList<LeadPoolActivityEntry> BuildActivityLog(
        string leadId,
        string persona,
        DateTimeOffset lastTouch,
        int ignoredTouchCount,
        bool hasReply,
        bool isWon,
        bool isLost)
    {
        var entries = new List<LeadPoolActivityEntry>
        {
            new(lastTouch, $"{persona} touched {leadId} (email)"),
        };

        if (hasReply)
        {
            entries.Add(new(lastTouch.AddMinutes(-45), "Prospect opened email"));
        }

        for (var t = 0; t < ignoredTouchCount; t++)
        {
            entries.Add(new(
                lastTouch.AddHours(-(t + 2)),
                $"{persona} follow-up #{t + 1} — no response"));
        }

        if (isWon)
        {
            entries.Add(new(lastTouch.AddHours(-1), "Deal closed — won"));
        }
        else if (isLost)
        {
            entries.Add(new(lastTouch.AddHours(-1), "Deal closed — lost"));
        }

        return entries
            .OrderByDescending(e => e.OccurredAtUtc)
            .ToList();
    }
}
