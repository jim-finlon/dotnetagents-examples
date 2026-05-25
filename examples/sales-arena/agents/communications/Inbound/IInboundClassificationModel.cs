namespace SalesArena.Communications.Inbound;

/// <summary>
/// Deterministic or LLM-backed classifier. Tests use <see cref="StubInboundClassificationModel"/>.
/// </summary>
public interface IInboundClassificationModel
{
    Task<InboundClassification> ClassifyAsync(
        string projectedPrompt,
        InboundMessage message,
        CancellationToken cancellationToken = default);
}
