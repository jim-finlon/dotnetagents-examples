using System.Diagnostics;
using System.Globalization;

namespace SalesArena.PersonaPackFormat;

/// <summary>Loads and validates an unpacked Sales Arena persona pack directory.</summary>
public sealed class PersonaPackLoader
{
    private static readonly string[] RequiredPersonaKeys =
    [
        "name",
        "author",
        "version",
        "model_tier",
        "tags",
        "bio_ref",
        "system_prompt_ref",
        "cadence_ref",
    ];

    public async Task<PersonaPackLoadResult> LoadAsync(
        string packDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packDirectory);

        var stopwatch = Stopwatch.StartNew();
        var root = Path.GetFullPath(packDirectory);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Persona pack directory not found: {root}");

        var personaYaml = await ReadRequiredTextAsync(root, "persona.yaml", cancellationToken).ConfigureAwait(false);
        var persona = PersonaYamlParser.Parse(personaYaml);
        foreach (var key in RequiredPersonaKeys)
        {
            if (!persona.TryGetValue(key, out var value) || (key != "tags" && string.IsNullOrWhiteSpace(value)))
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    $"persona.yaml is missing required top-level key '{key}'",
                    "persona.yaml");
            }
        }

        var bioRef = persona["bio_ref"];
        var systemPromptRef = persona["system_prompt_ref"];
        var cadenceRef = persona["cadence_ref"];

        var bioText = await ReadRequiredTextAsync(root, bioRef, cancellationToken).ConfigureAwait(false);
        var systemPrompt = await ReadRequiredTextAsync(root, systemPromptRef, cancellationToken).ConfigureAwait(false);
        var cadenceText = await ReadRequiredTextAsync(root, cadenceRef, cancellationToken).ConfigureAwait(false);
        var outreachVariants = Directory.EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => path.StartsWith("outreach/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (outreachVariants.Length == 0)
        {
            throw new PersonaPackException(
                PersonaPackErrorCode.FileMissingFromArchive,
                "persona pack must include at least one outreach markdown variant",
                "outreach");
        }

        var cadence = PersonaCadenceParser.Parse(cadenceText, cadenceRef);
        if (cadence.NarratorOnly)
        {
            if (Math.Abs(cadence.ChannelMixTotal) > 0.001)
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    "narrator_only packs must have a channel_mix total of 0",
                    cadenceRef);
            }
        }
        else
        {
            if (Math.Abs(cadence.ChannelMixTotal - 100.0) > 0.001)
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    string.Create(CultureInfo.InvariantCulture, $"cadence.yaml channel_mix must sum to 100 (got {cadence.ChannelMixTotal:0.###})"),
                    cadenceRef);
            }

            var systemPromptWordCount = CountWords(systemPrompt);
            if (systemPromptWordCount < 200)
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    string.Create(CultureInfo.InvariantCulture, $"system-prompt.md must be at least 200 words for non-narrator personas (got {systemPromptWordCount})"),
                    systemPromptRef);
            }
        }

        stopwatch.Stop();
        return new PersonaPackLoadResult(
            RootDirectory: root,
            Name: persona["name"],
            Author: persona["author"],
            Version: persona["version"],
            ModelTier: persona["model_tier"],
            NarratorOnly: cadence.NarratorOnly,
            ChannelMixTotal: cadence.ChannelMixTotal,
            BioText: bioText,
            SystemPrompt: systemPrompt,
            OutreachVariantPaths: outreachVariants,
            LoadElapsed: stopwatch.Elapsed);
    }

    private static async Task<string> ReadRequiredTextAsync(
        string root,
        string relativePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new PersonaPackException(
                PersonaPackErrorCode.FileMissingFromArchive,
                "persona pack required file reference is empty");
        }

        var normalized = ZipPersonaPackFormat.NormalizeAndValidatePath(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new PersonaPackException(
                PersonaPackErrorCode.PathTraversal,
                $"persona pack file '{relativePath}' escapes the pack directory",
                relativePath);
        }

        if (!File.Exists(fullPath))
        {
            throw new PersonaPackException(
                PersonaPackErrorCode.FileMissingFromArchive,
                $"required persona pack file '{relativePath}' is missing",
                relativePath);
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inWord)
                    count++;
                inWord = true;
            }
            else
            {
                inWord = false;
            }
        }
        return count;
    }
}

public sealed record PersonaPackLoadResult(
    string RootDirectory,
    string Name,
    string Author,
    string Version,
    string ModelTier,
    bool NarratorOnly,
    double ChannelMixTotal,
    string BioText,
    string SystemPrompt,
    IReadOnlyList<string> OutreachVariantPaths,
    TimeSpan LoadElapsed);

internal sealed record PersonaCadence(bool NarratorOnly, double ChannelMixTotal);

internal static class PersonaCadenceParser
{
    public static PersonaCadence Parse(string cadenceText, string fileNameForError = "cadence.yaml")
    {
        ArgumentNullException.ThrowIfNull(cadenceText);

        var narratorOnly = false;
        var channelMixTotal = 0.0;
        var inChannelMix = false;

        foreach (var raw in cadenceText.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            if (!char.IsWhiteSpace(line, 0) &&
                trimmed.StartsWith("narrator_only:", StringComparison.OrdinalIgnoreCase))
            {
                narratorOnly = trimmed[(trimmed.IndexOf(':') + 1)..].Trim()
                    .Equals("true", StringComparison.OrdinalIgnoreCase);
                inChannelMix = false;
                continue;
            }

            if (!char.IsWhiteSpace(line, 0) &&
                trimmed.StartsWith("channel_mix:", StringComparison.OrdinalIgnoreCase))
            {
                inChannelMix = true;
                continue;
            }

            if (!inChannelMix)
                continue;

            if (!char.IsWhiteSpace(line, 0))
            {
                inChannelMix = false;
                continue;
            }

            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
                continue;

            var valueText = trimmed[(colon + 1)..].Trim();
            if (double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                channelMixTotal += value;
            }
            else
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    $"{fileNameForError} channel_mix contains non-numeric value '{valueText}'",
                    fileNameForError);
            }
        }

        return new PersonaCadence(narratorOnly, channelMixTotal);
    }
}
