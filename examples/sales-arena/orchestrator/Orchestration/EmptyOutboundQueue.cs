namespace SalesArena.Orchestrator.Orchestration;

public sealed class EmptyOutboundQueue : IOutboundQueue
{
    public static EmptyOutboundQueue Instance { get; } = new();

    private EmptyOutboundQueue()
    {
    }

    public Task<IReadOnlyList<OutboundQueueItem>> ReadPendingAsync(
        string contestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OutboundQueueItem>>(Array.Empty<OutboundQueueItem>());
}
