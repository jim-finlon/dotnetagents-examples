using System.Text.Json;

namespace SalesArena.Communications.Inbound;

/// <summary>
/// Offline adapter that reads canned messages from JSON fixtures (SA-01-06).
/// </summary>
public class LocalFixtureInboundAdapter(string channel, string fixturePath) : IInboundChannelAdapter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string Channel { get; } = channel ?? throw new ArgumentNullException(nameof(channel));

    public async Task<IReadOnlyList<RawChannelMessage>> FetchPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(fixturePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(fixturePath);
        var envelope = await JsonSerializer.DeserializeAsync<FixtureEnvelope>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (envelope?.Messages is null)
        {
            return [];
        }

        return envelope.Messages
            .Where(m => string.Equals(m.Channel, Channel, StringComparison.OrdinalIgnoreCase))
            .Select(m => new RawChannelMessage(
                Channel,
                m.ExternalId,
                m.Body,
                m.FromName,
                m.FromEmail,
                m.CompanyHint,
                m.ReceivedAtUtc))
            .ToList();
    }

    private sealed record FixtureEnvelope(IReadOnlyList<FixtureMessage>? Messages);

    private sealed record FixtureMessage(
        string Channel,
        string ExternalId,
        string Body,
        string? FromName,
        string? FromEmail,
        string? CompanyHint,
        DateTimeOffset ReceivedAtUtc);
}

public sealed class LocalEmailInboundAdapter(string fixturePath)
    : LocalFixtureInboundAdapter("email", fixturePath);

public sealed class LocalSmsInboundAdapter(string fixturePath)
    : LocalFixtureInboundAdapter("sms", fixturePath);

public sealed class LocalWebFormInboundAdapter(string fixturePath)
    : LocalFixtureInboundAdapter("webform", fixturePath);

public sealed class LocalChatInboundAdapter(string fixturePath)
    : LocalFixtureInboundAdapter("chat", fixturePath);
