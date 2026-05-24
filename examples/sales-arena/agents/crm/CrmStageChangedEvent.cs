namespace SalesArena.Crm;

/// <summary>
/// Emitted on every CRM stage transition. The Arena Ledger (SA-02-03) and the
/// supporting cast (Invoice, Training) subscribe via <see cref="ICrmEventPublisher"/>.
/// </summary>
/// <param name="LeadId">The lead being transitioned (e.g. "L-0001").</param>
/// <param name="FromStage">The stage the record left.</param>
/// <param name="ToStage">The stage the record entered.</param>
/// <param name="Persona">Which persona caused the transition.</param>
/// <param name="OccurredAtUtc">When the transition was applied (UTC).</param>
/// <param name="EvidenceRef">Optional evidence pointer (e.g. inbound-message id, meeting id, proposal id).</param>
public sealed record CrmStageChangedEvent(
    string LeadId,
    string FromStage,
    string ToStage,
    string Persona,
    DateTimeOffset OccurredAtUtc,
    string? EvidenceRef);
