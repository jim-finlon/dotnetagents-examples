using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;

namespace SalesArena.Orchestrator.BellStream;

/// <summary>
/// Posts bell events to a Slack incoming webhook. Plain-text message format
/// (Slack mrkdwn). Webhook URL is treated as a credential — never logged,
/// never echoed, never persisted in the ledger payload.
/// </summary>
public sealed class SlackWebhookPoster : IBellWebhookPoster
{
    private readonly HttpClient _http;
    private readonly string? _webhookUrl;

    public SlackWebhookPoster(HttpClient http, string? webhookUrl)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _webhookUrl = webhookUrl;
    }

    public string TargetId => "slack";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_webhookUrl);

    public async Task<bool> PostAsync(BellEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (!IsConfigured) return false;

        var payload = new SlackPayload(FormatHeadline(evt));
        try
        {
            using var response = await _http.PostAsJsonAsync(_webhookUrl, payload, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            // Best-effort: bell drops never propagate.
            return false;
        }
    }

    public static string FormatHeadline(BellEvent evt)
    {
        // Slack mrkdwn: *bold*. Avoid PII; rely on the caller's already-sanitized Headline.
        var prefix = evt.Kind switch
        {
            BellKind.DealClosed => ":bell:",
            BellKind.CadillacPromotion => ":car:",
            BellKind.GlengarryDrip => ":gem:",
            BellKind.Ceremonial => ":mega:",
            _ => ":sparkles:",
        };

        var bodyPart = evt.ValueUsd is { } value
            ? string.Format(CultureInfo.InvariantCulture, " — *${0:N0}*", value)
            : "";

        return $"{prefix} *{evt.Persona}*: {evt.Headline}{bodyPart}";
    }

    private sealed record SlackPayload(string text);
}
