namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// Canonical event-kind discriminators. Every <see cref="ArenaEvent"/> carries
/// one of these as its <see cref="ArenaEvent.Kind"/>. Stored as-is in the
/// ledger so downstream consumers (Leaderboard SA-02-04, Replay SA-04-01)
/// can branch without a string-matching dance.
/// </summary>
public static class ArenaEventKinds
{
    /// <summary>A lead was assigned to a persona pod by the orchestrator.</summary>
    public const string LeadAssigned = "LeadAssigned";

    /// <summary>The persona completed research on a lead (firmographics + signals).</summary>
    public const string LeadResearched = "LeadResearched";

    /// <summary>An outbound touch was sent (email, SMS, LinkedIn DM, chat).</summary>
    public const string TouchSent = "TouchSent";

    /// <summary>An inbound message was received and classified.</summary>
    public const string InboundReceived = "InboundReceived";

    /// <summary>A meeting was booked on the persona's calendar.</summary>
    public const string MeetingBooked = "MeetingBooked";

    /// <summary>A meeting actually happened (post-meeting summary attached).</summary>
    public const string MeetingHeld = "MeetingHeld";

    /// <summary>A proposal was sent to the prospect.</summary>
    public const string ProposalSent = "ProposalSent";

    /// <summary>An objection was raised by the prospect (with the response the persona used).</summary>
    public const string Objection = "Objection";

    /// <summary>A deal closed — won or lost.</summary>
    public const string DealClosed = "DealClosed";

    /// <summary>Premium leads dripped to a top-tier persona by the Glengarry policy.</summary>
    public const string GlengarryLeadDripped = "GlengarryLeadDripped";

    /// <summary>Leads revoked from a bottom-tier persona (returned to the pool).</summary>
    public const string LeadsRevoked = "LeadsRevoked";

    /// <summary>A theatrical bell event — operator-visible, narrator-fired.</summary>
    public const string BellRung = "BellRung";

    /// <summary>Snapshot of the live leaderboard, written periodically for replay reconstruction.</summary>
    public const string LeaderboardSnapshot = "LeaderboardSnapshot";

    /// <summary>A contest phase change (init / start / pause / resume / end).</summary>
    public const string ContestPhaseChanged = "ContestPhaseChanged";

    /// <summary>An operator drafted a persona into a contest pod slot.</summary>
    public const string DraftPickMade = "DraftPickMade";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        LeadAssigned, LeadResearched, TouchSent, InboundReceived, MeetingBooked,
        MeetingHeld, ProposalSent, Objection, DealClosed, GlengarryLeadDripped,
        LeadsRevoked, BellRung, LeaderboardSnapshot, ContestPhaseChanged, DraftPickMade,
    };
}
