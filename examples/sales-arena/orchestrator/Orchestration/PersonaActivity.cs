namespace SalesArena.Orchestrator.Orchestration;

public sealed record PersonaActivity(
    string PodId,
    string Persona,
    string AgentRole,
    string Activity,
    string? LeadId,
    DateTimeOffset OccurredAtUtc);
