namespace SalesArena.Orchestrator.Orchestration;

public interface IArenaPodAgent
{
    string Role { get; }

    Task<AgentTickResult?> TickAsync(
        PersonaPod pod,
        ArenaOrchestratorContext context,
        CancellationToken cancellationToken = default);
}
