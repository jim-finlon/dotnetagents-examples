namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Operator configuration for the Bell Stream coordinator. All fields are
/// optional — if no webhook URLs are configured, the coordinator is a no-op
/// (which is the safe default; never sends to a real channel by accident).
/// </summary>
public sealed record BellStreamOptions
{
    /// <summary>Slack incoming-webhook URL. When set, every bell posts a Slack message.</summary>
    public string? SlackWebhookUrl { get; init; }

    /// <summary>Discord incoming-webhook URL. When set, every bell posts a Discord message.</summary>
    public string? DiscordWebhookUrl { get; init; }

    /// <summary>Maximum bells dispatched per minute. Default 5. Use 0 to disable rate limiting.</summary>
    public int BellRateLimitPerMin { get; init; } = 5;

    /// <summary>Discord username override for the webhook poster.</summary>
    public string DiscordUsername { get; init; } = "Sales Arena Bell";

    /// <summary>How long a webhook post can take before being abandoned.</summary>
    public TimeSpan WebhookTimeout { get; init; } = TimeSpan.FromSeconds(5);
}
