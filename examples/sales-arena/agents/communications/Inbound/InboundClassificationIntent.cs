using DotNetAgents.Agents.IntentProjector;

namespace SalesArena.Communications.Inbound;

/// <summary>
/// Builds the SA-01-06 classification rubric as an <see cref="IntentDocument"/> for
/// <see cref="IntentProjector"/> prompt projection.
/// </summary>
public static class InboundClassificationIntent
{
    public const string ConsumerId = "sales-arena-inbound-classifier";

    public static IntentDocument Create() =>
        new(
            Id: "sales-arena.inbound-classification.v1",
            Title: "Sales Arena inbound message classifier",
            Version: "1.0.0",
            Summary: "Classify inbound sales messages by intent, sentiment, and urgency.",
            Blocks:
            [
                new IntentBlock(
                    "intent-axis",
                    "Intent labels",
                    IntentBlockRole.Policy,
                    IntentContextScope.ToolSurface,
                    10,
                    IntentSecurityClassification.Public,
                    "Return one intent: interested, objection, scheduling, unsubscribe, cold-question, spam."),
                new IntentBlock(
                    "sentiment-axis",
                    "Sentiment labels",
                    IntentBlockRole.Policy,
                    IntentContextScope.ToolSurface,
                    20,
                    IntentSecurityClassification.Public,
                    "Return one sentiment: positive, neutral, negative, hostile."),
                new IntentBlock(
                    "urgency-axis",
                    "Urgency labels",
                    IntentBlockRole.Policy,
                    IntentContextScope.ToolSurface,
                    30,
                    IntentSecurityClassification.Public,
                    "Return one urgency: immediate, today, this-week, nurture."),
            ],
            Consumers:
            [
                new IntentConsumerProfile(
                    ConsumerId,
                    "Sales Arena inbound classifier",
                    IntentConsumerKind.AgentTool,
                    [IntentProjectionKind.ToolPrompt],
                    SupportsLargeContext: false,
                    RequiresOfflineSafeOutput: true),
            ]);
}
