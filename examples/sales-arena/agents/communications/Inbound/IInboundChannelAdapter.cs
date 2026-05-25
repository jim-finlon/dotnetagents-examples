namespace SalesArena.Communications.Inbound;

/// <summary>
/// Pulls messages from a single inbound channel. Reference implementations are local-fake only.
/// </summary>
public interface IInboundChannelAdapter
{
    string Channel { get; }

    Task<IReadOnlyList<RawChannelMessage>> FetchPendingAsync(CancellationToken cancellationToken = default);
}
