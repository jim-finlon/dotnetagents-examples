namespace SalesArena.Crm;

/// <summary>
/// One entry in the per-lead activity log. Built by the
/// <see cref="CrmPipelineStateMachine"/> on every accepted transition and
/// read back by the replay engine + drill-down views.
/// </summary>
public sealed record ActivityLogEntry(
    long Id,
    string LeadId,
    string FromStage,
    string ToStage,
    string Persona,
    DateTimeOffset OccurredAtUtc,
    string? EvidenceRef);
