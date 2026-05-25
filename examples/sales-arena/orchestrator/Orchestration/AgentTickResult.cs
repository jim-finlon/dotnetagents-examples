namespace SalesArena.Orchestrator.Orchestration;

public sealed record AgentTickResult(
    string AgentRole,
    string Activity,
    string? LeadId = null);
