namespace SalesArena.Communications.Outbound;

public interface IOutboundDrafter
{
    Task<DraftMessage> DraftAsync(
        ProspectContext prospect,
        string personaId,
        string channel,
        OutboundIntent intent,
        CancellationToken cancellationToken = default);
}
