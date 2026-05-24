using System.Text.Json;

namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// One ledger row. Append-only, content-addressed by <see cref="Id"/>.
/// </summary>
/// <remarks>
/// <para>The ledger stores the event in two parts: indexed columns (kind,
/// contest_id, lead_id, persona, occurred_utc) for query speed, and a
/// JSON-serialized <see cref="PayloadJson"/> for the typed contents. Consumers
/// fetch typed payloads via <see cref="GetPayload{T}"/>.</para>
///
/// <para>Constructed by callers; the <see cref="Id"/> is 0 until the ledger
/// assigns it on append.</para>
/// </remarks>
public sealed record ArenaEvent
{
    /// <summary>Ledger-assigned monotonically increasing id. 0 before append.</summary>
    public long Id { get; init; }

    /// <summary>Contest scope. Every event belongs to exactly one contest.</summary>
    public required string ContestId { get; init; }

    /// <summary>One of <see cref="ArenaEventKinds"/>.</summary>
    public required string Kind { get; init; }

    /// <summary>When the event happened (UTC).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Optional lead-scope; indexed for per-deal trace queries.</summary>
    public string? LeadId { get; init; }

    /// <summary>Optional persona-scope; indexed for per-persona leaderboard inputs.</summary>
    public string? Persona { get; init; }

    /// <summary>JSON-serialized typed payload. Use <see cref="GetPayload{T}"/> to deserialize.</summary>
    public required string PayloadJson { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Deserialize the payload to <typeparamref name="T"/>.</summary>
    /// <returns>The deserialized payload, or <c>null</c> if the JSON is empty.</returns>
    public T? GetPayload<T>() where T : class
    {
        if (string.IsNullOrWhiteSpace(PayloadJson)) return null;
        return JsonSerializer.Deserialize<T>(PayloadJson, JsonOptions);
    }

    /// <summary>Helper: serialize <paramref name="payload"/> to JSON for ledger storage.</summary>
    public static string SerializePayload<T>(T payload) where T : class =>
        JsonSerializer.Serialize(payload, JsonOptions);
}
