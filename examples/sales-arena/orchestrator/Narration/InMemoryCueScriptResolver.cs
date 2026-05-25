namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Test-friendly script resolver. Pass per-cue templates in the constructor.
/// Lines rotate round-robin so a contest with N closes uses every line in
/// the template before repeating.
/// </summary>
public sealed class InMemoryCueScriptResolver : IArenaCueScriptResolver
{
    private readonly Dictionary<string, IReadOnlyList<string>> _templates;
    private readonly Dictionary<string, int> _cursors;
    private readonly Lock _lock = new();

    public InMemoryCueScriptResolver(IReadOnlyDictionary<string, IReadOnlyList<string>> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _templates = new Dictionary<string, IReadOnlyList<string>>(templates, StringComparer.OrdinalIgnoreCase);
        _cursors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    public string? Resolve(string cueKind, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentException.ThrowIfNullOrEmpty(cueKind);
        ArgumentNullException.ThrowIfNull(tokens);

        if (!_templates.TryGetValue(cueKind, out var lines) || lines.Count == 0)
        {
            return null;
        }

        string template;
        lock (_lock)
        {
            var cursor = _cursors.TryGetValue(cueKind, out var existing) ? existing : 0;
            template = lines[cursor % lines.Count];
            _cursors[cueKind] = cursor + 1;
        }

        return SubstituteTokens(template, tokens);
    }

    internal static string SubstituteTokens(string template, IReadOnlyDictionary<string, string> tokens)
    {
        var result = template;
        foreach (var pair in tokens)
        {
            result = result.Replace("{" + pair.Key + "}", pair.Value, StringComparison.Ordinal);
        }
        return result;
    }
}
