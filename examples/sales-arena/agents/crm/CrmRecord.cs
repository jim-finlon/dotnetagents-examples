namespace SalesArena.Crm;

/// <summary>
/// Mutable per-lead state record. Holds the current stage + persona-of-record
/// + timestamps. Extension data lives in <see cref="Metadata"/>.
/// </summary>
public sealed class CrmRecord
{
    /// <summary>Stable lead id from the lead pack (e.g. "L-0001").</summary>
    public string LeadId { get; init; } = string.Empty;

    /// <summary>Current stage. One of <see cref="CrmStages.All"/>.</summary>
    public string Stage { get; set; } = CrmStages.Lead;

    /// <summary>Persona that owns this record right now (e.g. "roma", "levene").</summary>
    public string Persona { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last stage transition.</summary>
    public DateTimeOffset UpdatedAtUtc { get; set; }

    /// <summary>UTC timestamp of the record's creation.</summary>
    public DateTimeOffset CreatedAtUtc { get; init; }

    /// <summary>Free-form tags (e.g. "glengarry", "high-value", "objection-pricing").</summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>Persona-specific or workflow-specific extension data. Keep small + stringly-typed for portability.</summary>
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.Ordinal);
}
