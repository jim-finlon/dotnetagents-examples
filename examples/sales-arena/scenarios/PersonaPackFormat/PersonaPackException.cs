namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Stable error codes the operator + UI surface in import error messages.
/// New codes are additive; existing values must not be renumbered.
/// </summary>
public enum PersonaPackErrorCode
{
    InvalidArchive = 1,
    ManifestMissing = 2,
    ManifestMalformed = 3,
    ManifestHashMismatch = 4,
    FileMissingFromArchive = 5,
    UnexpectedFileInArchive = 6,
    PathTraversal = 7,
    PathAbsolute = 8,
    PackSizeExceeded = 9,
    FileSizeExceeded = 10,
    SchemaUnsupported = 11,
    PersonaYamlMalformed = 12,
    PersonaYamlMissing = 13,
}

public sealed class PersonaPackException : Exception
{
    public PersonaPackErrorCode Code { get; }
    public string? Path { get; }

    public PersonaPackException(PersonaPackErrorCode code, string message, string? path = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        Path = path;
    }
}
