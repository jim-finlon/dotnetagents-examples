namespace SalesArena.Orchestrator.Orchestration;

public sealed record CrmStreamEvent(
    string EventId,
    string Persona,
    string Kind,
    DateTimeOffset OccurredAtUtc);
