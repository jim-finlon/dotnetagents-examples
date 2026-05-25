namespace SalesArena.Crm;

/// <summary>
/// The 11-stage CRM lead lifecycle.
///
/// <para>Active stages: <see cref="Lead"/>, <see cref="Researched"/>,
/// <see cref="Contacted"/>, <see cref="Qualified"/>, <see cref="DemoBooked"/>,
/// <see cref="DemoHeld"/>, <see cref="ProposalSent"/>, <see cref="Negotiating"/>.</para>
///
/// <para>Terminal absorbing: <see cref="ClosedWon"/>, <see cref="ClosedLost"/>.</para>
///
/// <para>Re-engageable terminal: <see cref="Nurture"/> can transition back to
/// <see cref="Contacted"/> or <see cref="Researched"/> when the prospect
/// re-engages (signal-driven).</para>
/// </summary>
public static class CrmStages
{
    public const string Lead = "Lead";
    public const string Researched = "Researched";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string DemoBooked = "DemoBooked";
    public const string DemoHeld = "DemoHeld";
    public const string ProposalSent = "ProposalSent";
    public const string Negotiating = "Negotiating";
    public const string ClosedWon = "ClosedWon";
    public const string ClosedLost = "ClosedLost";
    public const string Nurture = "Nurture";

    /// <summary>All stage names in canonical order.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Lead, Researched, Contacted, Qualified, DemoBooked, DemoHeld,
        ProposalSent, Negotiating, ClosedWon, ClosedLost, Nurture,
    };

    /// <summary>Terminal absorbing stages — no outgoing transitions.</summary>
    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ClosedWon, ClosedLost,
    };

    /// <summary>Stages from which a prospect can no longer be unilaterally claimed by an agent — only via human override.</summary>
    public static bool IsTerminal(string stage) => Terminal.Contains(stage);
}
