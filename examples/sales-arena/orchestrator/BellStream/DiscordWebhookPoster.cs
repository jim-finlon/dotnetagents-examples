using System.Globalization;
using System.Net.Http.Json;

namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Posts bell events to a Discord incoming webhook. Discord-flavored markdown
/// (** for bold). Webhook URL treated as credential; never logged.
/// </summary>
public sealed class DiscordWebhookPoster : IBellWebhookPoster
{
    private readonly HttpClient _http;
    private readonly string? _webhookUrl;
    private readonly string _username;

    public DiscordWebhookPoster(HttpClient http, string? webhookUrl, string username = "Sales Arena Bell")
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _webhookUrl = webhookUrl;
        _username = string.IsNullOrWhiteSpace(username) ? "Sales Arena Bell" : username;
    }

    public string TargetId => "discord";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookUrl);

    public async Task<bool> PostAsync(BellEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!IsConfigured) return false;

        var payload = new DiscordPayload(FormatHeadline(evt), _username);
        try
        {
            using var response = await _http.PostAsJsonAsync(_webhookUrl, payload, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static string FormatHeadline(BellEvent evt)
    {
        var prefix = evt.Kind switch
        {
            BellKind.DealClosed => "🔔",
            BellKind.CadillacPromotion => "🚗",
            BellKind.GlengarryDrip => "💎",
            BellKind.Ceremonial => "📣",
            _ => "✨",
        };

        var bodyPart = evt.ValueUsd is { } value
            ? string.Format(CultureInfo.InvariantCulture, " — **${0:N0}**", value)
            : "";

        return $"{prefix} **{evt.Persona}**: {evt.Headline}{bodyPart}";
    }

    private sealed record DiscordPayload(string content, string username);
}
