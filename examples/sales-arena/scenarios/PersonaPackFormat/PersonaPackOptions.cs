namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Hardening knobs for the zip codec. Defaults are intentionally conservative
/// for community-uploaded packs (SA-07-01 risk note: malicious zip files).
/// </summary>
public sealed record PersonaPackOptions
{
    /// <summary>Total uncompressed bytes allowed across all files. Default 16 MiB.</summary>
    public long MaxTotalUncompressedBytes { get; init; } = 16 * 1024 * 1024;

    /// <summary>Per-file uncompressed cap. Default 4 MiB.</summary>
    public long MaxPerFileUncompressedBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>Max number of entries in the archive. Default 256.</summary>
    public int MaxEntryCount { get; init; } = 256;

    /// <summary>
    /// Require persona.yaml to parse as valid simple-YAML and to declare a
    /// `name:` key. Tightens guard against silently broken packs.
    /// </summary>
    public bool RequirePersonaYaml { get; init; } = true;

    /// <summary>
    /// Schema versions accepted on import. Always includes "v1".
    /// </summary>
    public IReadOnlySet<string> AcceptedSchemaVersions { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "v1" };
}
