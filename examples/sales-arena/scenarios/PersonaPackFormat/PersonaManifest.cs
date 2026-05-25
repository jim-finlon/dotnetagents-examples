namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Pack manifest. Lives inside the zip at <c>manifest.json</c>. The manifest
/// itself is NOT included in <see cref="FileHashes"/> — it's the
/// authoritative root, and including it would create a chicken-and-egg
/// integrity loop.
/// </summary>
public sealed record PersonaManifest(
    string Name,
    string Author,
    string Version,
    IReadOnlyDictionary<string, string> FileHashes,
    string? SignatureBase64 = null,
    string SchemaVersion = "v1");
