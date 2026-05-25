using System.Security.Cryptography;
using System.Text;

namespace SalesArena.Communications.Inbound;

/// <summary>
/// Ingests from all channel adapters, dedupes, and normalizes to <see cref="InboundMessage"/>.
/// </summary>
public sealed class InboundMessageRouter
{
    private readonly IReadOnlyList<IInboundChannelAdapter> _adapters;
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public InboundMessageRouter(IEnumerable<IInboundChannelAdapter> adapters)
    {
        ArgumentNullException.ThrowIfNull(adapters);
        _adapters = adapters.ToList();
        if (_adapters.Count == 0)
        {
            throw new ArgumentException("At least one channel adapter is required.", nameof(adapters));
        }
    }

    public async Task<IReadOnlyList<InboundMessage>> IngestAsync(CancellationToken cancellationToken = default)
    {
        var normalized = new List<InboundMessage>();
        foreach (var adapter in _adapters)
        {
            var batch = await adapter.FetchPendingAsync(cancellationToken).ConfigureAwait(false);
            foreach (var raw in batch)
            {
                var dedupeKey = BuildDedupeKey(raw);
                if (!_seen.Add(dedupeKey))
                {
                    continue;
                }

                normalized.Add(new InboundMessage(
                    MessageId: Guid.NewGuid().ToString("N"),
                    Channel: raw.Channel,
                    ExternalId: raw.ExternalId,
                    Body: raw.Body.Trim(),
                    FromName: raw.FromName,
                    FromEmail: raw.FromEmail,
                    CompanyHint: raw.CompanyHint,
                    ReceivedAtUtc: raw.ReceivedAtUtc));
            }
        }

        return normalized;
    }

    private static string BuildDedupeKey(RawChannelMessage raw)
    {
        var material = $"{raw.Channel}\n{raw.ExternalId}\n{raw.Body}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        return Convert.ToHexString(hash);
    }
}
