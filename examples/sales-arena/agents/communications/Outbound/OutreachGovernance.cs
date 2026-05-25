namespace SalesArena.Communications.Outbound;

/// <summary>
/// Refuses outreach when the prospect has not opted in (SA-01-07 security posture).
/// </summary>
public static class OutreachGovernance
{
    public static bool AllowsOutbound(ProspectContext prospect)
    {
        ArgumentNullException.ThrowIfNull(prospect);
        return prospect.OutreachOptIn;
    }
}
