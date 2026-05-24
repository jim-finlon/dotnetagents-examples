namespace SalesArena.Orchestrator.Orchestration;

public interface ICrmEventStream
{
    Task<IReadOnlyList<CrmStreamEvent>> ReadAvailableAsync(
        string contestId,
        CancellationToken cancellationToken = default);
}
