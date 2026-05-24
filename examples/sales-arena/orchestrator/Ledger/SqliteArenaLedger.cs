using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Data.Sqlite;

namespace SalesArena.Orchestrator.Ledger;

/// <summary>
/// Sqlite-backed <see cref="IArenaLedger"/>. Append-only schema with indexes
/// on the four routinely-filtered columns (contest_id, lead_id, persona, kind).
///
/// <para>Schema is created on first open + idempotent on re-open. Connection
/// strings:</para>
/// <list type="bullet">
///   <item><c>Data Source=:memory:</c> for unit tests.</item>
///   <item><c>Data Source=./.arena/ledger.db</c> for the demo Arena.</item>
/// </list>
/// </summary>
public sealed class SqliteArenaLedger : IArenaLedger
{
    private const string SchemaCreate = """
        CREATE TABLE IF NOT EXISTS arena_event (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            contest_id    TEXT    NOT NULL,
            kind          TEXT    NOT NULL,
            occurred_utc  TEXT    NOT NULL,
            lead_id       TEXT,
            persona       TEXT,
            payload_json  TEXT    NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_arena_event_contest ON arena_event (contest_id, occurred_utc);
        CREATE INDEX IF NOT EXISTS ix_arena_event_lead    ON arena_event (lead_id,    occurred_utc);
        CREATE INDEX IF NOT EXISTS ix_arena_event_persona ON arena_event (persona,    occurred_utc);
        CREATE INDEX IF NOT EXISTS ix_arena_event_kind    ON arena_event (kind,       occurred_utc);
        """;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    public SqliteArenaLedger(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = SchemaCreate;
        cmd.ExecuteNonQuery();
    }

    public async Task<ArenaEvent> AppendAsync(ArenaEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        ValidateForInsert(evt);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await InsertOneAsync(evt, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ArenaEvent>> AppendManyAsync(
        IEnumerable<ArenaEvent> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var batch = events.ToList();
        foreach (var e in batch) ValidateForInsert(e);
        if (batch.Count == 0) return Array.Empty<ArenaEvent>();

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var tx = (SqliteTransaction)await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var inserted = new List<ArenaEvent>(batch.Count);
            try
            {
                foreach (var e in batch)
                {
                    inserted.Add(await InsertOneAsync(e, cancellationToken, tx).ConfigureAwait(false));
                }
                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            return inserted;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async IAsyncEnumerable<ArenaEvent> QueryAsync(
        ArenaEventFilter filter,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = BuildQuery(filter, selectCount: false);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return ReadRow(reader);
        }
    }

    public async Task<long> CountAsync(ArenaEventFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = BuildQuery(filter, selectCount: true);
        var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(result);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
    }

    // ---- internals -------------------------------------------------------

    private static void ValidateForInsert(ArenaEvent evt)
    {
        if (string.IsNullOrWhiteSpace(evt.ContestId))
            throw new ArgumentException("ContestId is required.", nameof(evt));
        if (string.IsNullOrWhiteSpace(evt.Kind))
            throw new ArgumentException("Kind is required.", nameof(evt));
        if (!ArenaEventKinds.All.Contains(evt.Kind))
            throw new ArgumentException($"Unknown event kind: '{evt.Kind}'.", nameof(evt));
        if (string.IsNullOrWhiteSpace(evt.PayloadJson))
            throw new ArgumentException("PayloadJson is required (use \"{}\" for empty payloads).", nameof(evt));
    }

    private async Task<ArenaEvent> InsertOneAsync(ArenaEvent evt, CancellationToken cancellationToken, SqliteTransaction? tx = null)
    {
        await using var cmd = _connection.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO arena_event (contest_id, kind, occurred_utc, lead_id, persona, payload_json)
            VALUES ($contestId, $kind, $occurredUtc, $leadId, $persona, $payload);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$contestId", evt.ContestId);
        cmd.Parameters.AddWithValue("$kind", evt.Kind);
        cmd.Parameters.AddWithValue("$occurredUtc", evt.OccurredAtUtc.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$leadId", (object?)evt.LeadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$persona", (object?)evt.Persona ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$payload", evt.PayloadJson);

        var newId = Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false));
        return evt with { Id = newId };
    }

    private SqliteCommand BuildQuery(ArenaEventFilter filter, bool selectCount)
    {
        var cmd = _connection.CreateCommand();
        var sql = selectCount
            ? "SELECT COUNT(*) FROM arena_event WHERE 1=1"
            : "SELECT id, contest_id, kind, occurred_utc, lead_id, persona, payload_json FROM arena_event WHERE 1=1";

        if (filter.ContestId is not null)
        {
            sql += " AND contest_id = $contestId";
            cmd.Parameters.AddWithValue("$contestId", filter.ContestId);
        }
        if (filter.Kind is not null)
        {
            sql += " AND kind = $kind";
            cmd.Parameters.AddWithValue("$kind", filter.Kind);
        }
        if (filter.LeadId is not null)
        {
            sql += " AND lead_id = $leadId";
            cmd.Parameters.AddWithValue("$leadId", filter.LeadId);
        }
        if (filter.Persona is not null)
        {
            sql += " AND persona = $persona";
            cmd.Parameters.AddWithValue("$persona", filter.Persona);
        }
        if (filter.FromUtc is not null)
        {
            sql += " AND occurred_utc >= $fromUtc";
            cmd.Parameters.AddWithValue("$fromUtc", filter.FromUtc.Value.ToString("O", CultureInfo.InvariantCulture));
        }
        if (filter.ToUtc is not null)
        {
            sql += " AND occurred_utc <= $toUtc";
            cmd.Parameters.AddWithValue("$toUtc", filter.ToUtc.Value.ToString("O", CultureInfo.InvariantCulture));
        }

        if (!selectCount)
        {
            sql += filter.DescendingTime
                ? " ORDER BY occurred_utc DESC, id DESC"
                : " ORDER BY occurred_utc ASC, id ASC";
            if (filter.Limit is { } limit && limit > 0)
            {
                sql += " LIMIT $limit";
                cmd.Parameters.AddWithValue("$limit", limit);
            }
        }

        cmd.CommandText = sql;
        return cmd;
    }

    private static ArenaEvent ReadRow(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ContestId = reader.GetString(1),
        Kind = reader.GetString(2),
        OccurredAtUtc = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        LeadId = reader.IsDBNull(4) ? null : reader.GetString(4),
        Persona = reader.IsDBNull(5) ? null : reader.GetString(5),
        PayloadJson = reader.GetString(6),
    };
}
