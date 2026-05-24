using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Reference codec for <c>.salesman.zip</c> packs. Pure .NET — uses
/// <see cref="ZipArchive"/> + SHA-256 + System.Text.Json. No third-party
/// dependencies; the assembly is small enough to ship via the public-core
/// boundary.
/// </summary>
public sealed class ZipPersonaPackFormat : IPersonaPackFormat
{
    private const string ManifestFileName = "manifest.json";
    private const string PersonaYamlName = "persona.yaml";

    private readonly PersonaPackOptions _options;

    public ZipPersonaPackFormat(PersonaPackOptions? options = null)
    {
        _options = options ?? new PersonaPackOptions();
    }

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public async Task ExportAsync(PersonaPack pack, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(destination);

        ValidateNormalizedPaths(pack.Files.Keys);

        var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, bytes) in pack.Files)
        {
            hashes[path] = Sha256Hex(bytes);
        }

        var manifest = new PersonaManifest(
            Name: pack.Name,
            Author: pack.Author,
            Version: pack.Version,
            FileHashes: hashes,
            SignatureBase64: pack.SignatureBase64);

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        await WriteEntryAsync(archive, ManifestFileName, SerializeManifest(manifest), cancellationToken).ConfigureAwait(false);
        foreach (var (path, bytes) in pack.Files)
        {
            await WriteEntryAsync(archive, path, bytes, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<PersonaPack> ImportAsync(Stream source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        ZipArchive archive;
        try
        {
            archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new PersonaPackException(PersonaPackErrorCode.InvalidArchive, "Archive is not a valid zip", inner: ex);
        }

        using (archive)
        {
            if (archive.Entries.Count > _options.MaxEntryCount)
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PackSizeExceeded,
                    $"Archive has {archive.Entries.Count} entries; limit is {_options.MaxEntryCount}");
            }

            var manifestEntry = archive.GetEntry(ManifestFileName)
                ?? throw new PersonaPackException(PersonaPackErrorCode.ManifestMissing, "manifest.json is missing from the archive");

            PersonaManifest manifest;
            try
            {
                var manifestJson = await ReadEntryStringAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
                manifest = JsonSerializer.Deserialize<PersonaManifest>(manifestJson, _jsonOptions)
                    ?? throw new PersonaPackException(PersonaPackErrorCode.ManifestMalformed, "manifest.json deserialized to null");
            }
            catch (JsonException ex)
            {
                throw new PersonaPackException(PersonaPackErrorCode.ManifestMalformed, "manifest.json is not valid JSON", inner: ex);
            }

            if (!_options.AcceptedSchemaVersions.Contains(manifest.SchemaVersion))
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.SchemaUnsupported,
                    $"manifest schemaVersion '{manifest.SchemaVersion}' is not supported");
            }

            ValidateNormalizedPaths(manifest.FileHashes.Keys);

            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var totalBytes = 0L;

            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.FullName, ManifestFileName, StringComparison.Ordinal))
                {
                    continue;
                }

                var normalized = NormalizeAndValidatePath(entry.FullName);

                if (!manifest.FileHashes.ContainsKey(normalized))
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.UnexpectedFileInArchive,
                        $"file '{normalized}' is in the archive but not in manifest.json",
                        normalized);
                }

                if (entry.Length > _options.MaxPerFileUncompressedBytes)
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.FileSizeExceeded,
                        $"file '{normalized}' is {entry.Length} bytes; per-file limit is {_options.MaxPerFileUncompressedBytes}",
                        normalized);
                }

                totalBytes += entry.Length;
                if (totalBytes > _options.MaxTotalUncompressedBytes)
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.PackSizeExceeded,
                        $"archive total uncompressed bytes exceeded {_options.MaxTotalUncompressedBytes}");
                }

                var bytes = await ReadEntryBytesAsync(entry, cancellationToken).ConfigureAwait(false);
                var hash = Sha256Hex(bytes);
                if (!string.Equals(hash, manifest.FileHashes[normalized], StringComparison.OrdinalIgnoreCase))
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.ManifestHashMismatch,
                        $"file '{normalized}' sha256 does not match manifest",
                        normalized);
                }

                files[normalized] = bytes;
            }

            foreach (var declared in manifest.FileHashes.Keys)
            {
                if (!files.ContainsKey(declared))
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.FileMissingFromArchive,
                        $"manifest references '{declared}' but the file is missing from the archive",
                        declared);
                }
            }

            if (_options.RequirePersonaYaml)
            {
                if (!files.TryGetValue(PersonaYamlName, out var yamlBytes))
                {
                    throw new PersonaPackException(
                        PersonaPackErrorCode.PersonaYamlMissing,
                        "pack must contain persona.yaml at the archive root");
                }
                var parsed = PersonaYamlParser.Parse(Encoding.UTF8.GetString(yamlBytes), PersonaYamlName);
                PersonaYamlParser.RequireName(parsed);
            }

            return new PersonaPack(
                Name: manifest.Name,
                Author: manifest.Author,
                Version: manifest.Version,
                Files: files,
                SignatureBase64: manifest.SignatureBase64);
        }
    }

    /// <summary>SHA-256 of <paramref name="bytes"/> as lowercase hex. Public so test + tooling can construct manifests directly.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static byte[] SerializeManifest(PersonaManifest manifest)
        => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(manifest, _jsonOptions));

    private static async Task<string> ReadEntryStringAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var s = entry.Open();
        using var reader = new StreamReader(s, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> ReadEntryBytesAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        await using var s = entry.Open();
        using var ms = new MemoryStream(checked((int)entry.Length));
        await s.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var s = entry.Open();
        await s.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Validate that every path is in normalized form (no traversal, no absolute).</summary>
    public static void ValidateNormalizedPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            NormalizeAndValidatePath(path);
        }
    }

    /// <summary>
    /// Reject path-traversal (.. segments), Windows-style drives, absolute paths,
    /// and stray empty segments. Returns the normalized forward-slash path on
    /// success; throws otherwise.
    /// </summary>
    /// <summary>
    /// Reject path-traversal (.. segments), Windows-style drives, absolute paths,
    /// and stray empty segments. Returns the normalized forward-slash path on
    /// success; throws otherwise.
    /// </summary>
    public static string NormalizeAndValidatePath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new PersonaPackException(PersonaPackErrorCode.PathTraversal, "archive contains an empty path", raw);
        }

        var normalized = raw.Replace('\\', '/');
        if (normalized.StartsWith('/'))
        {
            throw new PersonaPackException(PersonaPackErrorCode.PathAbsolute, $"archive path '{raw}' is absolute", raw);
        }
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            throw new PersonaPackException(PersonaPackErrorCode.PathAbsolute, $"archive path '{raw}' is a Windows drive path", raw);
        }

        var segments = normalized.Split('/');
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                throw new PersonaPackException(PersonaPackErrorCode.PathTraversal, $"archive path '{raw}' contains a parent-directory segment", raw);
            }
        }
        return normalized;
    }
}
