namespace SalesArena.Crm;

/// <summary>
/// In-process synchronous publisher. Each call to <see cref="PublishAsync"/>
/// fires <see cref="StageChanged"/> on the calling thread. Subscribers that
/// need durability or async fan-out wrap this with their own queue.
/// </summary>
public sealed class InMemoryCrmEventPublisher : ICrmEventPublisher
{
    public event EventHandler<CrmStageChangedEvent>? StageChanged;

    public Task PublishAsync(CrmStageChangedEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        StageChanged?.Invoke(this, evt);
        return Task.CompletedTask;
    }
}
