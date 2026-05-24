namespace SalesArena.Orchestrator.Orchestration;

public sealed class EmptyCrmEventStream : ICrmEventStream
{
    public static EmptyCrmEventStream Instance { get; } = new();

    private EmptyCrmEventStream()
    {
    }

    public Task<IReadOnlyList<CrmStreamEvent>> ReadAvailableAsync(
        string contestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<CrmStreamEvent>>(Array.Empty<CrmStreamEvent>());
}
