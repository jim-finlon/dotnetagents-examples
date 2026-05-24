using System.Globalization;

namespace SalesArena.PersonaPackFormat;

/// <summary>
/// Minimal `key: value` line parser. The pack format intentionally avoids a
/// full YAML dependency for v1; persona.yaml is shallow + the orchestrator
/// only needs `name`, `author`, `version`, `model_tier`, and tags. Deeper
/// fields are passed through unparsed.
/// </summary>
public static class PersonaYamlParser
{
    /// <summary>
    /// Parses a tiny subset of YAML: scalar `key: value` lines, `#` comments,
    /// and blank lines. Throws <see cref="PersonaPackException"/> on malformed
    /// content.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Parse(string yamlText, string fileNameForError = "persona.yaml")
    {
        ArgumentNullException.ThrowIfNull(yamlText);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var lines = yamlText.Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex].TrimEnd('\r');
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            // YAML lists (`- item`) or nested keys (indented) are skipped — we
            // store everything for round-trip but only care about top-level scalars.
            if (trimmed.StartsWith('-'))
            {
                continue;
            }
            if (char.IsWhiteSpace(raw, 0))
            {
                // Indented continuation; ignore for v1.
                continue;
            }

            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                throw new PersonaPackException(
                    PersonaPackErrorCode.PersonaYamlMalformed,
                    string.Create(CultureInfo.InvariantCulture, $"{fileNameForError}: line {lineIndex + 1} has no key:value separator"),
                    fileNameForError);
            }

            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim();
            value = StripInlineQuotes(value);
            result[key] = value;
        }

        return result;
    }

    private static string StripInlineQuotes(string value)
    {
        if (value.Length >= 2)
        {
            if ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\''))
            {
                return value[1..^1];
            }
        }
        return value;
    }

    /// <summary>
    /// Validates that the parsed persona.yaml declares a non-empty <c>name</c>.
    /// Throws <see cref="PersonaPackException"/> when missing.
    /// </summary>
    public static string RequireName(IReadOnlyDictionary<string, string> parsed)
    {
        if (!parsed.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            throw new PersonaPackException(
                PersonaPackErrorCode.PersonaYamlMalformed,
                "persona.yaml is missing a 'name:' key",
                "persona.yaml");
        }
        return name;
    }
}
