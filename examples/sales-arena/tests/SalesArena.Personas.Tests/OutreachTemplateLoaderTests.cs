using FluentAssertions;
using SalesArena.OutreachTemplates;
using Xunit;

namespace SalesArena.Personas.Tests;

public sealed class OutreachTemplateLoaderTests
{
    [Fact]
    public void LoadAll_loads_72_templates_with_valid_frontmatter()
    {
        var loader = CreateLoader();
        var all = loader.LoadAll();

        all.Should().HaveCount(OutreachTemplateCatalog.ExpectedTemplateCount);
        all.Should().OnlyContain(t => OutreachTemplateCatalog.CanonicalPersonaIds.Contains(t.PersonaId));
        all.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Hypothesis));
        all.Should().OnlyContain(t => t.WordCountTarget > 0);
        all.Should().OnlyContain(t => t.SubstitutionTokens.Count > 0);
    }

    [Fact]
    public void Hypotheses_are_distinct_per_persona_and_channel()
    {
        var loader = CreateLoader();
        var all = loader.LoadAll();

        foreach (var persona in OutreachTemplateCatalog.CanonicalPersonaIds)
        {
            foreach (var channel in OutreachTemplateCatalog.Channels)
            {
                var slice = all.Where(t => t.PersonaId == persona && t.Channel == channel).ToList();
                slice.Should().HaveCount(3);
                slice.Select(t => t.Hypothesis.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Should().HaveCount(3, $"{persona}/{channel} variants must differ");
            }
        }
    }

    [Fact]
    public void Mitch_and_Murray_templates_are_narrator_only()
    {
        var loader = CreateLoader();
        var narrator = loader.LoadForPersona("mitch-and-murray");

        narrator.Should().HaveCount(12);
        narrator.Should().OnlyContain(t => t.NarratorOnly);
    }

    [Fact]
    public void Bodies_include_required_substitution_tokens()
    {
        var loader = CreateLoader();
        var all = loader.LoadAll().Where(t => !t.NarratorOnly);

        all.Should().OnlyContain(t => t.BodyMarkdown.Contains("{{prospect.first_name}}", StringComparison.Ordinal));
        all.Should().OnlyContain(t =>
            t.SubstitutionTokens.Any(token => token.Contains("prospect.company", StringComparison.Ordinal)));
    }

    private static OutreachTemplateLoader CreateLoader()
    {
        var root = FindPersonasRoot();
        return new OutreachTemplateLoader(root);
    }

    private static string FindPersonasRoot()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "personas"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "personas"),
        };

        foreach (var c in candidates)
        {
            var resolved = Path.GetFullPath(c);
            if (Directory.Exists(Path.Combine(resolved, "roma", "outreach")))
            {
                return resolved;
            }
        }

        throw new DirectoryNotFoundException(
            $"could not locate personas root; checked: {string.Join(", ", candidates)}");
    }
}
