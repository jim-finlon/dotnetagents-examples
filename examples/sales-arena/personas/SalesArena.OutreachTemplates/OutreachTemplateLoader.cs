namespace SalesArena.OutreachTemplates;

/// <summary>
/// Filesystem loader for persona outreach markdown templates.
/// </summary>
public sealed class OutreachTemplateLoader(string personasRootDirectory) : IOutreachTemplateLoader
{
    private readonly string _personasRoot = personasRootDirectory
        ?? throw new ArgumentNullException(nameof(personasRootDirectory));

    public IReadOnlyList<OutreachTemplateRecord> LoadAll()
    {
        var results = new List<OutreachTemplateRecord>(OutreachTemplateCatalog.ExpectedTemplateCount);
        foreach (var persona in OutreachTemplateCatalog.CanonicalPersonaIds)
        {
            results.AddRange(LoadForPersona(persona));
        }

        if (results.Count != OutreachTemplateCatalog.ExpectedTemplateCount)
        {
            throw new OutreachTemplateLoadException(
                $"Expected {OutreachTemplateCatalog.ExpectedTemplateCount} templates, found {results.Count}");
        }

        return results;
    }

    public IReadOnlyList<OutreachTemplateRecord> LoadForPersona(string personaId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(personaId);
        var personaDir = Path.Combine(_personasRoot, personaId);
        if (!Directory.Exists(personaDir))
        {
            throw new OutreachTemplateLoadException($"Persona directory not found: {personaDir}");
        }

        var records = new List<OutreachTemplateRecord>(12);
        foreach (var channel in OutreachTemplateCatalog.Channels)
        {
            foreach (var variant in OutreachTemplateCatalog.VariantIds)
            {
                var path = Path.Combine(personaDir, "outreach", channel, $"{variant}.md");
                if (!File.Exists(path))
                {
                    throw new OutreachTemplateLoadException($"Missing template: {path}");
                }

                records.Add(ParseFile(personaId, channel, variant, path));
            }
        }

        return records;
    }

    private static OutreachTemplateRecord ParseFile(
        string personaId,
        string channel,
        string variantId,
        string absolutePath)
    {
        var text = File.ReadAllText(absolutePath);
        var split = text.Split("---", 3, StringSplitOptions.None);
        if (split.Length < 3)
        {
            throw new OutreachTemplateLoadException($"{absolutePath}: expected YAML frontmatter block");
        }

        var meta = OutreachTemplateFrontmatterParser.Parse(split[1], absolutePath);
        if (!meta.TryGetValue("hypothesis", out var hypothesis) || string.IsNullOrWhiteSpace(hypothesis))
        {
            throw new OutreachTemplateLoadException($"{absolutePath}: hypothesis is required");
        }

        if (!meta.TryGetValue("variant_id", out var variantFromFile) || string.IsNullOrWhiteSpace(variantFromFile))
        {
            throw new OutreachTemplateLoadException($"{absolutePath}: variant_id is required");
        }

        var wordTarget = OutreachTemplateFrontmatterParser.RequireWordCountTarget(meta, absolutePath);
        var substitutions = OutreachTemplateFrontmatterParser.ParseSubstitutions(meta);
        var body = split[2].Trim();
        var narratorOnly = meta.TryGetValue("narrator_only", out var narratorRaw)
            && bool.TryParse(narratorRaw, out var narratorFlag)
            && narratorFlag;

        return new OutreachTemplateRecord(
            personaId,
            channel,
            variantId,
            hypothesis,
            wordTarget,
            substitutions,
            body,
            narratorOnly);
    }
}
