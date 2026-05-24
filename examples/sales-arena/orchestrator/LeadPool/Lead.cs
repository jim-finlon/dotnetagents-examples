namespace SalesArena.Orchestrator.LeadPool;

/// <summary>
/// The minimal lead shape the pool deals with. Loaded from a lead-pack JSON
/// (see SA-05-02 schema). Sparse-intel cold leads omit most optional fields.
/// </summary>
public sealed record Lead
{
    /// <summary>Stable lead id within the pack (e.g. "L-0001").</summary>
    public required string Id { get; init; }

    /// <summary>"glengarry" or "cold".</summary>
    public required string Tier { get; init; }

    /// <summary>Company name (always present).</summary>
    public required string CompanyName { get; init; }

    public string? Industry { get; init; }
    public string? Size { get; init; }
    public string? Region { get; init; }
    public string? Domain { get; init; }
    public int? Headcount { get; init; }

    public string? ContactFirstName { get; init; }
    public string? ContactLastName { get; init; }
    public string? ContactRole { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }

    /// <summary>Free-text notes (only present on hand-curated glengarry-tier leads).</summary>
    public string? Notes { get; init; }

    // v2 renewal fields (SA-06-03; absent on v1 packs).
    public string? CustomerTier { get; init; }
    public decimal? Mrr { get; init; }
    public string? RenewalDate { get; init; }
    public double? ChurnRiskScore { get; init; }
    public IReadOnlyList<string>? ExpansionSignals { get; init; }
}

/// <summary>The top-level lead-pack manifest as loaded from JSON.</summary>
public sealed record LeadPack(
    string Version,
    string Name,
    string Description,
    bool Synthetic,
    IReadOnlyList<Lead> Leads);
