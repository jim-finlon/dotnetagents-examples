namespace SalesArena.Communications.Outbound;

public sealed record ProspectContext(
    string FirstName,
    string Company,
    string Industry,
    string RecentTopic,
    bool OutreachOptIn = true);

public enum OutboundIntent
{
    Introduce,
    FollowUp,
    MeetingNudge,
    ReEngage,
}

public sealed record DraftMessage(
    string PersonaId,
    string Channel,
    string VariantId,
    string? Subject,
    string Body,
    double Confidence,
    string Hypothesis);

public enum OutboundSendStatus
{
    Sent,
    PreviewRequired,
    RateLimited,
    GovernanceRefused,
    PreviewNotConfirmed,
}

public sealed record OutboundSendResult(
    OutboundSendStatus Status,
    DraftMessage? Draft = null,
    Guid? PreviewSessionId = null,
    string? PreviewToken = null,
    string? Detail = null);
