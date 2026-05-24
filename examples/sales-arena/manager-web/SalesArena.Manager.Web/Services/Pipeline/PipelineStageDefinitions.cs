namespace SalesArena.Manager.Web.Services.Pipeline;

/// <summary>
/// Canonical funnel stage order for the manager pipeline visualization (SA-08-05).
/// Aligns with <see cref="SalesArena.Crm.CrmStages"/> active path through Negotiating, with
/// terminal won deals shown as <see cref="Closed"/>.
/// </summary>
public static class PipelineStageDefinitions
{
    public const string Lead = "Lead";
    public const string Researched = "Researched";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string DemoBooked = "DemoBooked";
    public const string DemoHeld = "DemoHeld";
    public const string ProposalSent = "ProposalSent";
    public const string Negotiating = "Negotiating";
    public const string Closed = "Closed";

    public static readonly IReadOnlyList<string> FunnelStages =
    [
        Lead,
        Researched,
        Contacted,
        Qualified,
        DemoBooked,
        DemoHeld,
        ProposalSent,
        Negotiating,
        Closed,
    ];

    public static int IndexOf(string stage)
    {
        for (var i = 0; i < FunnelStages.Count; i++)
        {
            if (string.Equals(FunnelStages[i], stage, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
