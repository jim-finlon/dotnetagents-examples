namespace SalesArena.Communications.Inbound;

public enum InboundMessageIntent
{
    Interested,
    Objection,
    Scheduling,
    Unsubscribe,
    ColdQuestion,
    Spam,
}

public enum InboundSentiment
{
    Positive,
    Neutral,
    Negative,
    Hostile,
}

public enum InboundUrgency
{
    Immediate,
    Today,
    ThisWeek,
    Nurture,
}

public sealed record RawChannelMessage(
    string Channel,
    string ExternalId,
    string Body,
    string? FromName,
    string? FromEmail,
    string? CompanyHint,
    DateTimeOffset ReceivedAtUtc);

public sealed record InboundMessage(
    string MessageId,
    string Channel,
    string ExternalId,
    string Body,
    string? FromName,
    string? FromEmail,
    string? CompanyHint,
    DateTimeOffset ReceivedAtUtc);

public sealed record InboundClassification(
    InboundMessageIntent Intent,
    InboundSentiment Sentiment,
    InboundUrgency Urgency,
    string PromptTemplateId);

public enum CrmCorrelationStatus
{
    Matched,
    Ambiguous,
    Miss,
}

public sealed record CrmCorrelationResult(
    CrmCorrelationStatus Status,
    string? LeadId,
    double Score,
    IReadOnlyList<string> CandidateLeadIds);

public sealed record CrmProspectIndexEntry(
    string LeadId,
    string? FirstName,
    string? LastName,
    string Company,
    string? Email);

public sealed record RoutedInboundMessage(
    InboundMessage Message,
    InboundClassification Classification,
    CrmCorrelationResult Correlation);
