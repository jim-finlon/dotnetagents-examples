using System.IO.Compression;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using SalesArena.PersonaPackFormat;
using Xunit;

namespace SalesArena.PersonaPackFormat.Tests;

public sealed class ZipPersonaPackFormatTests
{
    private static PersonaPack BuildPack() => new(
        Name: "roma",
        Author: "operator",
        Version: "0.1.0",
        Files: new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["persona.yaml"] = Encoding.UTF8.GetBytes("""
                name: roma
                author: operator
                version: 0.1.0
                model_tier: local-strong
                """),
            ["system-prompt.md"] = Encoding.UTF8.GetBytes("You are Roma, consultative closer. 200 words elided."),
            ["cadence.yaml"] = Encoding.UTF8.GetBytes("touches_per_day: 12"),
            ["bio.md"] = Encoding.UTF8.GetBytes("Roma was the only one who would still answer the phone after midnight."),
        });

    [Fact]
    public async Task Roundtrip_export_then_import_returns_identical_pack()
    {
        var format = new ZipPersonaPackFormat();
        var original = BuildPack();

        await using var ms = new MemoryStream();
        await format.ExportAsync(original, ms);
        ms.Position = 0;
        var imported = await format.ImportAsync(ms);

        imported.Name.Should().Be(original.Name);
        imported.Author.Should().Be(original.Author);
        imported.Version.Should().Be(original.Version);
        imported.Files.Keys.Should().BeEquivalentTo(original.Files.Keys);
        foreach (var (path, bytes) in original.Files)
        {
            imported.Files[path].Should().Equal(bytes, $"{path} must round-trip byte-for-byte");
        }
    }

    [Fact]
    public async Task TamperedManifest_hash_is_refused()
    {
        var format = new ZipPersonaPackFormat();
        await using var ms = new MemoryStream();
        await format.ExportAsync(BuildPack(), ms);

        // Rewrite manifest.json with a hash that won't match persona.yaml.
        ms.Position = 0;
        var bytes = ms.ToArray();
        var tampered = TamperManifest(bytes, persona =>
        {
            var copy = new Dictionary<string, string>(persona.FileHashes, StringComparer.Ordinal)
            {
                ["persona.yaml"] = new string('a', 64),
            };
            return persona with { FileHashes = copy };
        });

        await using var tamperedMs = new MemoryStream(tampered);
        Func<Task> act = () => format.ImportAsync(tamperedMs);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.ManifestHashMismatch);
    }

    [Fact]
    public async Task MissingFileFromArchive_is_refused()
    {
        var format = new ZipPersonaPackFormat();
        await using var ms = new MemoryStream();
        await format.ExportAsync(BuildPack(), ms);

        var rewrite = AsExpandable(ms.ToArray());
        using (var archive = new ZipArchive(rewrite, ZipArchiveMode.Update, leaveOpen: true))
        {
            archive.GetEntry("cadence.yaml")!.Delete();
        }
        rewrite.Position = 0;

        Func<Task> act = () => format.ImportAsync(rewrite);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.FileMissingFromArchive);
        ex.Path.Should().Be("cadence.yaml");
    }

    [Fact]
    public async Task MalformedPersonaYaml_is_refused()
    {
        var format = new ZipPersonaPackFormat();
        var pack = BuildPack();

        // Replace persona.yaml with a junk byte string.
        var malformed = new Dictionary<string, byte[]>(pack.Files, StringComparer.Ordinal)
        {
            ["persona.yaml"] = Encoding.UTF8.GetBytes("this is not yaml without a colon"),
        };
        var bad = pack with { Files = malformed };

        await using var ms = new MemoryStream();
        await format.ExportAsync(bad, ms);
        ms.Position = 0;

        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.PersonaYamlMalformed);
    }

    [Fact]
    public async Task PersonaYaml_without_name_is_refused()
    {
        var format = new ZipPersonaPackFormat();
        var pack = BuildPack();

        var noName = new Dictionary<string, byte[]>(pack.Files, StringComparer.Ordinal)
        {
            ["persona.yaml"] = Encoding.UTF8.GetBytes("author: someone\nversion: 0.1.0"),
        };
        var bad = pack with { Files = noName };

        await using var ms = new MemoryStream();
        await format.ExportAsync(bad, ms);
        ms.Position = 0;

        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.PersonaYamlMalformed);
    }

    [Fact]
    public async Task UnexpectedExtraFile_in_archive_is_refused()
    {
        var format = new ZipPersonaPackFormat();
        await using var ms = new MemoryStream();
        await format.ExportAsync(BuildPack(), ms);

        var rewrite = AsExpandable(ms.ToArray());
        using (var archive = new ZipArchive(rewrite, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = archive.CreateEntry("smuggled.txt", CompressionLevel.NoCompression);
            await using var s = entry.Open();
            await s.WriteAsync(new byte[] { 0x42 });
        }
        rewrite.Position = 0;

        Func<Task> act = () => format.ImportAsync(rewrite);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.UnexpectedFileInArchive);
    }

    [Theory]
    [InlineData("../etc/passwd")]
    [InlineData("a/../b")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/Windows/System32/secret.txt")]
    public void NormalizeAndValidatePath_rejects_malicious_paths(string path)
    {
        Action act = () => ZipPersonaPackFormat.NormalizeAndValidatePath(path);
        act.Should().Throw<PersonaPackException>();
    }

    [Fact]
    public async Task PathTraversal_in_archive_is_refused()
    {
        // Hand-craft a zip with a path-traversal entry + manifest that points at it.
        await using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var yaml = Encoding.UTF8.GetBytes("name: x");
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["../etc/passwd"] = ZipPersonaPackFormat.Sha256Hex(yaml),
            };
            var manifest = new PersonaManifest("x", "x", "0.1.0", hashes);

            // Manifest first.
            var mEntry = archive.CreateEntry("manifest.json");
            await using (var ms2 = mEntry.Open())
            {
                var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var bytes = Encoding.UTF8.GetBytes(json);
                await ms2.WriteAsync(bytes);
            }

            // Then the traversal entry.
            var bad = archive.CreateEntry("../etc/passwd");
            await using var s = bad.Open();
            await s.WriteAsync(yaml);
        }
        ms.Position = 0;

        var format = new ZipPersonaPackFormat();
        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.PathTraversal);
    }

    [Fact]
    public async Task ManifestMissing_is_refused()
    {
        await using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("persona.yaml");
            await using var s = entry.Open();
            await s.WriteAsync(Encoding.UTF8.GetBytes("name: x"));
        }
        ms.Position = 0;

        var format = new ZipPersonaPackFormat();
        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.ManifestMissing);
    }

    [Fact]
    public async Task SchemaVersion_not_in_AcceptedSchemaVersions_is_refused()
    {
        var pack = BuildPack();
        var format = new ZipPersonaPackFormat();
        await using var ms = new MemoryStream();
        await format.ExportAsync(pack, ms);

        var bytes = TamperManifest(ms.ToArray(), m => m with { SchemaVersion = "vNext" });
        await using var modified = new MemoryStream(bytes);

        Func<Task> act = () => format.ImportAsync(modified);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.SchemaUnsupported);
    }

    [Fact]
    public async Task InvalidZip_is_refused()
    {
        await using var ms = new MemoryStream(Encoding.UTF8.GetBytes("not a zip file"));
        var format = new ZipPersonaPackFormat();
        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.InvalidArchive);
    }

    [Fact]
    public async Task PerFileSizeLimit_blocks_oversized_entries()
    {
        var pack = BuildPack();
        var oversize = new Dictionary<string, byte[]>(pack.Files, StringComparer.Ordinal)
        {
            ["big.bin"] = new byte[2_000_000],
        };
        var fat = pack with { Files = oversize };

        var format = new ZipPersonaPackFormat(new PersonaPackOptions
        {
            MaxPerFileUncompressedBytes = 1_500_000,
        });
        await using var ms = new MemoryStream();
        await format.ExportAsync(fat, ms);
        ms.Position = 0;

        Func<Task> act = () => format.ImportAsync(ms);
        var ex = (await act.Should().ThrowAsync<PersonaPackException>()).Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.FileSizeExceeded);
    }

    private static byte[] TamperManifest(byte[] original, Func<PersonaManifest, PersonaManifest> mutate)
    {
        using var ms = AsExpandable(original);
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var manifestEntry = archive.GetEntry("manifest.json")!;
            string current;
            using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8))
            {
                current = reader.ReadToEnd();
            }
            var manifest = JsonSerializer.Deserialize<PersonaManifest>(current, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            var rewritten = mutate(manifest);

            manifestEntry.Delete();
            var fresh = archive.CreateEntry("manifest.json");
            using var writer = new StreamWriter(fresh.Open(), Encoding.UTF8);
            writer.Write(JsonSerializer.Serialize(rewritten, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        return ms.ToArray();
    }

    private static MemoryStream AsExpandable(byte[] bytes)
    {
        var ms = new MemoryStream();
        ms.Write(bytes, 0, bytes.Length);
        ms.Position = 0;
        return ms;
    }
}
