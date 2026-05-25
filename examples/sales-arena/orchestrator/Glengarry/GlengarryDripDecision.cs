namespace SalesArena.Orchestrator.Glengarry;

/// <summary>
/// Per-call outcome of the Glengarry drip runner. Surfaces what changed (or
/// didn't, and why) for observability + tests. The runner records this as
/// ArenaEvents in the ledger; callers can also log the decision directly.
/// </summary>
public sealed record GlengarryDripDecision(
    string Reason,
    string? TopPersona,
    IReadOnlyList<string> DrippedLeadIds,
    string? BottomPersona,
    IReadOnlyList<string> RevokedLeadIds,
    DateTimeOffset RunAtUtc)
{
    /// <summary>True when the cycle actually moved leads — drip and/or revoke happened.</summary>
    public bool DidMutate => DrippedLeadIds.Count > 0 || RevokedLeadIds.Count > 0;

    /// <summary>The "nothing happened" decision.</summary>
    public static GlengarryDripDecision Skipped(string reason, DateTimeOffset at) =>
        new(reason, null, Array.Empty<string>(), null, Array.Empty<string>(), at);
}

/// <summary>Standard skip reasons.</summary>
public static class GlengarryDripSkipReasons
{
    public const string NotDueYet = "not_due_yet";
    public const string NoTopPersona = "no_top_persona";
    public const string NoPremiumLeadsAvailable = "no_premium_leads_available";
    public const string BottomPersonaCooldown = "bottom_persona_in_cooldown";
    public const string BottomPersonaEmpty = "bottom_persona_has_no_leads";
}
