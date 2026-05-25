using Microsoft.Data.Sqlite;

namespace SalesArena.Crm;

/// <summary>
/// Sqlite-backed <see cref="IActivityLog"/>. One table, append-only, single
/// writer per process. Schema is created on first open; safe for re-open.
///
/// <para>Connection-string conventions:</para>
/// <list type="bullet">
///   <item><c>Data Source=:memory:</c> for tests (in-process; one connection's lifetime).</item>
///   <item><c>Data Source=./.arena/activity.db</c> for the demo Arena (local-relative path).</item>
/// </list>
/// </summary>
public sealed class SqliteActivityLog : IActivityLog
{
    private const string SchemaCreate = """
        CREATE TABLE IF NOT EXISTS activity_log (
            id            INTEGER PRIMARY KEY AUTOINCREMENT,
            lead_id       TEXT    NOT NULL,
            from_stage    TEXT    NOT NULL,
            to_stage      TEXT    NOT NULL,
            persona       TEXT    NOT NULL,
            occurred_utc  TEXT    NOT NULL,
            evidence_ref  TEXT
        );
        CREATE INDEX IF NOT EXISTS ix_activity_lead ON activity_log (lead_id, occurred_utc);
        """;

    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Opens an activity-log connection. The connection stays open for the
    /// lifetime of the log; closing it requires <see cref="DisposeAsync"/>.
    /// </summary>
    public SqliteActivityLog(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _connection = new SqliteConnection(connectionString);
        _connection.Open();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = SchemaCreate;
        cmd.ExecuteNonQuery();
    }

    public async Task<long> AppendAsync(ActivityLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO activity_log (lead_id, from_stage, to_stage, persona, occurred_utc, evidence_ref)
                VALUES ($leadId, $fromStage, $toStage, $persona, $occurredUtc, $evidenceRef);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$leadId", entry.LeadId);
            cmd.Parameters.AddWithValue("$fromStage", entry.FromStage);
            cmd.Parameters.AddWithValue("$toStage", entry.ToStage);
            cmd.Parameters.AddWithValue("$persona", entry.Persona);
            cmd.Parameters.AddWithValue("$occurredUtc", entry.OccurredAtUtc.ToString("O"));
            cmd.Parameters.AddWithValue("$evidenceRef", (object?)entry.EvidenceRef ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return Convert.ToInt64(result);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityLogEntry>> GetByLeadAsync(string leadId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT id, lead_id, from_stage, to_stage, persona, occurred_utc, evidence_ref
            FROM activity_log
            WHERE lead_id = $leadId
            ORDER BY occurred_utc ASC, id ASC;
            """;
        cmd.Parameters.AddWithValue("$leadId", leadId);

        var rows = new List<ActivityLogEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new ActivityLogEntry(
                Id: reader.GetInt64(0),
                LeadId: reader.GetString(1),
                FromStage: reader.GetString(2),
                ToStage: reader.GetString(3),
                Persona: reader.GetString(4),
                OccurredAtUtc: DateTimeOffset.Parse(reader.GetString(5), null, System.Globalization.DateTimeStyles.RoundtripKind),
                EvidenceRef: reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return rows;
    }

    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM activity_log;";
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
}
