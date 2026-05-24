namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Pub/sub seam for bell events. Producers (the Arena orchestrator's tick
/// loop, the leaderboard-change handler, the Glengarry-drip runner) call
/// <see cref="PublishAsync"/>; consumers (webhook posters, SignalR hubs,
/// audio-cue players) subscribe via <see cref="BellRang"/>.
///
/// <para>In-proc by design — durable bell persistence is the ledger's job
/// (SA-02-03). The bus is the live broadcast tier; the ledger is the audit
/// trail. Tests should use this in-memory bus; production wires the same
/// shape with a SignalR transport.</para>
/// </summary>
public interface IBellEventBus
{
    /// <summary>Fires synchronously on the publisher's thread. Throw to signal failure.</summary>
    event EventHandler<BellEvent>? BellRang;

    /// <summary>Publish one bell event. Returns when all synchronous subscribers complete.</summary>
    Task PublishAsync(BellEvent evt, CancellationToken cancellationToken = default);
}

/// <summary>In-process synchronous bell bus. Subscribers attach to <see cref="BellRang"/>.</summary>
public sealed class InMemoryBellEventBus : IBellEventBus
{
    public event EventHandler<BellEvent>? BellRang;

    public Task PublishAsync(BellEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        BellRang?.Invoke(this, evt);
        return Task.CompletedTask;
    }
}
