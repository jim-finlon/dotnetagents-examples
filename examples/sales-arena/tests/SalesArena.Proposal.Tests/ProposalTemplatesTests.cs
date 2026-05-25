using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace SalesArena.Proposal.Tests;

public class ProposalTemplatesTests
{
    private static readonly string[] Personas = { "roma", "levene", "moss", "aaronow", "williamson", "mitch-and-murray" };
    private static readonly string[] Tiers = { "starter", "pro", "enterprise" };

    public static System.Collections.Generic.IEnumerable<object[]> GetTemplateTestData()
    {
        foreach (var p in Personas)
        {
            foreach (var t in Tiers)
            {
                yield return new object[] { p, t };
            }
        }
    }

    private static string FindPersonaDir(string slug)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "personas", slug),
            Path.Combine(AppContext.BaseDirectory, "personas", slug),
        };
        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved))
                return resolved;
        }

        throw new DirectoryNotFoundException(
            $"could not locate base persona '{slug}'; checked: {string.Join(", ", candidates)}");
    }

    [Theory]
    [MemberData(nameof(GetTemplateTestData))]
    public void VerifyTemplate_SubstitutionAndDistinctness(string persona, string tier)
    {
        // 1. Locate and load the template
        var personaDir = FindPersonaDir(persona);
        var templatePath = Path.Combine(personaDir, "proposals", $"{tier}.md");
        File.Exists(templatePath).Should().BeTrue($"Template file must exist: {templatePath}");
        var templateContent = File.ReadAllText(templatePath);

        // 2. Fictional prospect
        var prospect = new ProposalProspect(
            Company: "Acme Corp",
            FirstName: "Alex",
            Role: "VP of Revenue",
            Timezone: "Pacific",
            RegulatedIndustryOrEuOrCustomResidency: "EU data residency"
        );

        // 3. Render and measure time
        var agent = new ProposalAgent();
        var sw = Stopwatch.StartNew();
        var rendered = agent.SubstituteTemplate(templateContent, prospect);
        sw.Stop();

        // 4. Assert performance budget (< 100 ms)
        sw.ElapsedMilliseconds.Should().BeLessThan(100, $"Template rendering for {persona}/{tier} should be fast");

        // 5. Assert no unresolved tokens
        var unresolvedMatches = Regex.Matches(rendered, @"\{\{[^}]+\}\}");
        unresolvedMatches.Count.Should().Be(0, $"Rendered template must contain no unresolved tokens: {string.Join(", ", unresolvedMatches.Cast<Match>().Select(m => m.Value))}");

        // 6. Assert persona-style distinctness
        var wordCount = CountWords(rendered);
        var normalizedText = Regex.Replace(rendered, @"\s+", " ");

        switch (persona)
        {
            case "moss":
                wordCount.Should().BeLessThanOrEqualTo(250, "Moss templates must be short (<= 250 words)");
                break;

            case "williamson":
                var standardMatches = Regex.Matches(rendered, "standard", RegexOptions.IgnoreCase);
                standardMatches.Count.Should().BeGreaterThanOrEqualTo(5, "Williamson templates must contain the word 'standard' at least 5 times");
                break;

            case "levene":
                var caseStudies = new[] { "Greybridge", "Stratham", "Fenwick", "Hartfield", "Northwood", "Okonkwo" };
                caseStudies.Any(cs => rendered.Contains(cs, StringComparison.OrdinalIgnoreCase))
                    .Should().BeTrue("Levene templates must contain at least one case-study name from the knowledge pack");
                break;

            case "roma":
                var questionWords = new[] { "What", "Why", "How", "Who", "Where", "Which", "When", "Will", "Would", "Should", "Could", "Is", "Are", "Can" };
                questionWords.Any(qw => rendered.Contains(qw, StringComparison.OrdinalIgnoreCase))
                    .Should().BeTrue("Roma templates must contain at least one question word");
                break;

            case "aaronow":
                normalizedText.Contains("per our standard process", StringComparison.OrdinalIgnoreCase)
                    .Should().BeTrue("Aaronow templates must contain 'per our standard process' case-insensitively");
                break;

            case "mitch-and-murray":
                // Assert narrator_only: true in the frontmatter
                rendered.Should().Contain("narrator_only: true", "Mitch & Murray templates must set 'narrator_only: true' in frontmatter");
                break;
        }
    }

    private static int CountWords(string text) =>
        text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
}
