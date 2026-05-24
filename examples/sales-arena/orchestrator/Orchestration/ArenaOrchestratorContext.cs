namespace SalesArena.Orchestrator.Orchestration;

public sealed class ArenaOrchestratorContext
{
    public required string ContestId { get; init; }

    public required IReadOnlyCollection<PersonaPod> Pods { get; init; }

    public IReadOnlyList<CrmStreamEvent> CrmEvents { get; init; } = [];

    public IReadOnlyList<OutboundQueueItem> OutboundQueue { get; init; } = [];

    public List<PersonaActivity> Activities { get; } = [];
}
