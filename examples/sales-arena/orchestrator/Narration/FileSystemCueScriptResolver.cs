namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Loads cue templates from a directory of `.txt` files. Each file is named
/// after a cue kind (e.g. `deal-closed.txt`, `cold-open.txt`); blank lines
/// and `#` comments are ignored. The file mapping is case-insensitive and
/// kebab-cased; `DealClosed` resolves to `deal-closed.txt`.
/// </summary>
public sealed class FileSystemCueScriptResolver : IArenaCueScriptResolver
{
    private readonly InMemoryCueScriptResolver _inner;

    public FileSystemCueScriptResolver(string scriptDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(scriptDirectory);
        if (!Directory.Exists(scriptDirectory))
        {
            throw new DirectoryNotFoundException(scriptDirectory);
        }

        var templates = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var cueKind in CueKinds.All.Concat(AmbientCueKinds.All))
        {
            var path = Path.Combine(scriptDirectory, FileNameFor(cueKind));
            if (!File.Exists(path))
            {
                continue;
            }

            var lines = File.ReadAllLines(path)
                .Select(static l => l.Trim())
                .Where(static l => l.Length > 0 && !l.StartsWith('#'))
                .ToArray();

            if (lines.Length > 0)
            {
                templates[cueKind] = lines;
            }
        }

        _inner = new InMemoryCueScriptResolver(templates);
    }

    public string? Resolve(string cueKind, IReadOnlyDictionary<string, string> tokens)
        => _inner.Resolve(cueKind, tokens);

    /// <summary>
    /// Maps a cue kind to its on-disk script file name. ContestOpened is
    /// served by `cold-open.txt` so operators have an obvious place to fork
    /// the iconic monologue (story SA-02-06 acceptance).
    /// </summary>
    public static string FileNameFor(string cueKind) => cueKind switch
    {
        CueKinds.ContestOpened => "cold-open.txt",
        _ => KebabCase(cueKind) + ".txt",
    };

    /// <summary>
    /// PascalCase → kebab-case. Public so tests + extensions can derive the
    /// expected on-disk file name for a custom cue kind.
    /// </summary>
    public static string KebabCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
        {
            return pascalCase;
        }

        var builder = new System.Text.StringBuilder(pascalCase.Length + 4);
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (i > 0 && char.IsUpper(c))
            {
                builder.Append('-');
            }
            builder.Append(char.ToLowerInvariant(c));
        }
        return builder.ToString();
    }
}
