namespace SalesArena.Communications.Inbound;

/// <summary>
/// Keyword-driven classifier for offline tests (SA-01-06 risk note: deterministic stub LLM).
/// </summary>
public sealed class StubInboundClassificationModel : IInboundClassificationModel
{
    public Task<InboundClassification> ClassifyAsync(
        string projectedPrompt,
        InboundMessage message,
        CancellationToken cancellationToken = default)
    {
        _ = projectedPrompt;
        _ = cancellationToken;
        var text = message.Body.ToLowerInvariant();

        var intent = ClassifyIntent(text);
        var sentiment = ClassifySentiment(text);
        var urgency = ClassifyUrgency(text);

        return Task.FromResult(new InboundClassification(
            intent,
            sentiment,
            urgency,
            InboundClassificationIntent.ConsumerId));
    }

    private static InboundMessageIntent ClassifyIntent(string text)
    {
        if (text.Contains("unsubscribe", StringComparison.Ordinal) || text.Contains("opt out", StringComparison.Ordinal))
            return InboundMessageIntent.Unsubscribe;
        if (text.Contains("spam", StringComparison.Ordinal) || text.Contains("viagra", StringComparison.Ordinal))
            return InboundMessageIntent.Spam;
        if (text.Contains("schedule", StringComparison.Ordinal) || text.Contains("calendar", StringComparison.Ordinal))
            return InboundMessageIntent.Scheduling;
        if (text.Contains("objection", StringComparison.Ordinal) || text.Contains("too expensive", StringComparison.Ordinal))
            return InboundMessageIntent.Objection;
        if (text.Contains('?'))
            return InboundMessageIntent.ColdQuestion;
        return InboundMessageIntent.Interested;
    }

    private static InboundSentiment ClassifySentiment(string text)
    {
        if (text.Contains("hostile", StringComparison.Ordinal) || text.Contains("lawsuit", StringComparison.Ordinal))
            return InboundSentiment.Hostile;
        if (text.Contains("angry", StringComparison.Ordinal) || text.Contains("terrible", StringComparison.Ordinal))
            return InboundSentiment.Negative;
        if (text.Contains("thanks", StringComparison.Ordinal) || text.Contains("great", StringComparison.Ordinal))
            return InboundSentiment.Positive;
        return InboundSentiment.Neutral;
    }

    private static InboundUrgency ClassifyUrgency(string text)
    {
        if (text.Contains("asap", StringComparison.Ordinal) || text.Contains("urgent", StringComparison.Ordinal))
            return InboundUrgency.Immediate;
        if (text.Contains("today", StringComparison.Ordinal))
            return InboundUrgency.Today;
        if (text.Contains("this week", StringComparison.Ordinal))
            return InboundUrgency.ThisWeek;
        return InboundUrgency.Nurture;
    }
}
