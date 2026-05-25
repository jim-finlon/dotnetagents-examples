namespace SalesArena.Orchestrator.Orchestration;

public sealed record ArenaOrchestratorOptions
{
    public TimeSpan TickInterval { get; init; } = TimeSpan.FromSeconds(5);

    public int MaxConcurrentPodTicks { get; init; } = 6;
}
