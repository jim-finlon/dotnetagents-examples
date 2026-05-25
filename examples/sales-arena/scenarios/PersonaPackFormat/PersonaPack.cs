namespace SalesArena.PersonaPackFormat;

/// <summary>
/// An in-memory persona pack. The zip codec
/// (<see cref="ZipPersonaPackFormat"/>) ferries this between the operator
/// filesystem and the orchestrator's persona registry.
/// </summary>
/// <param name="Name">Persona's display name (also the directory name).</param>
/// <param name="Author">Pack author handle.</param>
/// <param name="Version">SemVer or operator-chosen version string.</param>
/// <param name="Files">
/// Relative-path → raw bytes. Paths are forward-slash, no leading slash, no
/// `..`. Validated on import.
/// </param>
/// <param name="SignatureBase64">Optional Ed25519/RSA signature over the manifest.</param>
public sealed record PersonaPack(
    string Name,
    string Author,
    string Version,
    IReadOnlyDictionary<string, byte[]> Files,
    string? SignatureBase64 = null);
