using System.Globalization;

namespace SalesArena.OutreachTemplates;

internal static class OutreachTemplateFrontmatterParser
{
    public static IReadOnlyDictionary<string, string> Parse(string yamlText, string fileNameForError)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in yamlText.Split('\n'))
        {
            var trimmed = raw.TrimEnd('\r').Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var colon = trimmed.IndexOf(':');
            if (colon <= 0)
            {
                throw new OutreachTemplateLoadException(
                    $"{fileNameForError}: malformed frontmatter line '{trimmed}'");
            }

            var key = trimmed[..colon].Trim();
            var value = trimmed[(colon + 1)..].Trim().Trim('"');
            result[key] = value;
        }

        return result;
    }

    public static int RequireWordCountTarget(IReadOnlyDictionary<string, string> meta, string fileName)
    {
        if (!meta.TryGetValue("word_count_target", out var raw)
            || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var target)
            || target <= 0)
        {
            throw new OutreachTemplateLoadException(
                $"{fileName}: word_count_target must be a positive integer");
        }

        return target;
    }

    public static IReadOnlyList<string> ParseSubstitutions(IReadOnlyDictionary<string, string> meta)
    {
        if (!meta.TryGetValue("substitutions", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.StartsWith("{{", StringComparison.Ordinal) ? s : "{{" + s + "}}")
            .ToArray();
    }
}
