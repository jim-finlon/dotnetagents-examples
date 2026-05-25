namespace SalesArena.Orchestrator.Orchestration;

public interface IOutboundQueue
{
    Task<IReadOnlyList<OutboundQueueItem>> ReadPendingAsync(
        string contestId,
        CancellationToken cancellationToken = default);
}
