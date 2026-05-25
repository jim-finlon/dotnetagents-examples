namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// Typed payload records for every <see cref="ArenaEventKinds"/> discriminator.
///
/// <para>These records are the strongly-typed view of the JSON stored in
/// <see cref="ArenaEvent.PayloadJson"/>. The leaderboard (SA-02-04) and replay
/// (SA-04-01) consume them via <see cref="ArenaEvent.GetPayload{T}"/>. Schema
/// evolution: add fields with defaults; never remove or rename in v1.</para>
/// </summary>
public sealed record LeadAssignedPayload(
    string LeadId,
    string Persona,
    string Source);

public sealed record LeadResearchedPayload(
    string LeadId,
    string Persona,
    int SignalsFound,
    string? BriefRef);

public sealed record TouchSentPayload(
    string LeadId,
    string Persona,
    string Channel,
    string TemplateId,
    string VariantId,
    string? Subject,
    int CharacterCount);

public sealed record InboundReceivedPayload(
    string LeadId,
    string Persona,
    string Channel,
    string Intent,
    string Sentiment,
    string Urgency);

public sealed record MeetingBookedPayload(
    string LeadId,
    string Persona,
    DateTimeOffset ScheduledForUtc,
    int DurationMinutes,
    string? CalendarRef);

public sealed record MeetingHeldPayload(
    string LeadId,
    string Persona,
    int DurationMinutes,
    int DecisionsRecorded,
    int ActionsRecorded,
    string? TranscriptRef);

public sealed record ProposalSentPayload(
    string LeadId,
    string Persona,
    string PricingTier,
    decimal TotalContractValueUsd,
    string? ProposalRef);

public sealed record ObjectionPayload(
    string LeadId,
    string Persona,
    string ObjectionKind,
    string ResponseTemplateId,
    string? OutcomeNote);

public sealed record DealClosedPayload(
    string LeadId,
    string Persona,
    string Outcome,                  // "Won" | "Lost"
    decimal? ValueUsd,
    string? LossReason);

public sealed record GlengarryLeadDrippedPayload(
    string Persona,
    IReadOnlyList<string> LeadIds,
    string Reason);

public sealed record LeadsRevokedPayload(
    string Persona,
    IReadOnlyList<string> LeadIds,
    string Reason);

public sealed record BellRungPayload(
    string Reason,
    string? Persona,
    string? LeadId,
    string? NarrationLine);

public sealed record LeaderboardSnapshotPayload(
    IReadOnlyList<LeaderboardEntry> Entries,
    string ScoringConfigId);

public sealed record LeaderboardEntry(
    string Persona,
    int Position,
    string Tier,                     // "Cadillac" | "SteakKnives" | "YouAreFired"
    decimal RevenueUsd,
    int DealsWon,
    int DealsLost,
    double ConversionRate);

public sealed record ContestPhaseChangedPayload(
    string Phase,                    // "Init" | "Started" | "Paused" | "Resumed" | "Ended"
    string? Reason);
