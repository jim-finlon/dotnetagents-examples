using DotNetAgents.Agents.IntentProjector;

namespace SalesArena.Communications.Inbound;

/// <summary>
/// Projects the inbound classification rubric via <see cref="IntentProjector"/>, then classifies
/// through an injectable model (stub in tests).
/// </summary>
public sealed class InboundClassifier
{
    private readonly IInboundClassificationModel _model;
    private readonly string _projectedPrompt;

    public InboundClassifier(
        IInboundClassificationModel model,
        IntentProjector? projector = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        var intentProjector = projector ?? new IntentProjector();
        var receipt = intentProjector.Project(
            InboundClassificationIntent.Create(),
            new IntentProjectionRequest(
                IntentProjectionKind.ToolPrompt,
                InboundClassificationIntent.ConsumerId));
        _projectedPrompt = receipt.Artifacts[0].Content;
    }

    public string ProjectedPromptTemplate => _projectedPrompt;

    public Task<InboundClassification> ClassifyAsync(
        InboundMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var prompt = _projectedPrompt + "\n\n---\nINBOUND MESSAGE:\n" + message.Body;
        return _model.ClassifyAsync(prompt, message, cancellationToken);
    }
}
