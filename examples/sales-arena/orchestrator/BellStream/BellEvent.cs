namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// A theatrical bell event broadcast to anyone watching the contest live —
/// SignalR spectators, Slack channels, Discord webhooks, the operator's
/// closing-bell app.
/// </summary>
/// <param name="Kind">Why the bell rang.</param>
/// <param name="ContestId">Which contest fired it.</param>
/// <param name="Persona">Who's the subject (winner / promoted / dripped-to).</param>
/// <param name="LeadId">Optional lead reference (for deal-closed bells).</param>
/// <param name="ValueUsd">Optional deal value.</param>
/// <param name="Headline">Plain-text headline, already sanitized for spectator surfaces.</param>
/// <param name="OccurredAtUtc">When the bell rang.</param>
public sealed record BellEvent(
    BellKind Kind,
    string ContestId,
    string Persona,
    string? LeadId,
    decimal? ValueUsd,
    string Headline,
    DateTimeOffset OccurredAtUtc);

/// <summary>The reason a bell rang.</summary>
public enum BellKind
{
    /// <summary>A deal just closed Won. The bread-and-butter bell.</summary>
    DealClosed,

    /// <summary>A persona promoted into the Cadillac tier.</summary>
    CadillacPromotion,

    /// <summary>Premium leads dripped to a top-tier persona.</summary>
    GlengarryDrip,

    /// <summary>Operator-triggered ceremonial ring (test, opening, closing).</summary>
    Ceremonial,
}
