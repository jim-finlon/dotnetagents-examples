using FluentAssertions;
using SalesArena.PersonaPackFormat;
using Xunit;

namespace SalesArena.PersonaPackFormat.Tests;

public sealed class PersonaYamlParserTests
{
    [Fact]
    public void Parses_simple_scalar_lines()
    {
        var parsed = PersonaYamlParser.Parse("""
            # comment
            name: roma
            author: "operator"
            version: '0.1.0'

            model_tier: local-strong
            """);
        parsed["name"].Should().Be("roma");
        parsed["author"].Should().Be("operator");
        parsed["version"].Should().Be("0.1.0");
        parsed["model_tier"].Should().Be("local-strong");
    }

    [Fact]
    public void Skips_lists_and_nested_lines()
    {
        var parsed = PersonaYamlParser.Parse("""
            name: levene
            tags:
              - hunter
              - phone-first
            """);
        parsed["name"].Should().Be("levene");
        // `tags:` is a top-level key with an empty scalar value (the parser
        // intentionally does not descend into the nested list — those entries
        // are dropped). We do still surface the key so the orchestrator can
        // see it's present.
        parsed["tags"].Should().Be(string.Empty);
        parsed.Should().NotContainKey("- hunter");
    }

    [Fact]
    public void Throws_when_line_has_no_colon()
    {
        Action act = () => PersonaYamlParser.Parse("name: roma\njust-a-line\n");
        var ex = act.Should().Throw<PersonaPackException>().Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.PersonaYamlMalformed);
    }

    [Fact]
    public void RequireName_throws_when_missing()
    {
        var parsed = new Dictionary<string, string> { ["author"] = "x" };
        Action act = () => PersonaYamlParser.RequireName(parsed);
        var ex = act.Should().Throw<PersonaPackException>().Subject.First();
        ex.Code.Should().Be(PersonaPackErrorCode.PersonaYamlMalformed);
    }

    [Fact]
    public void RequireName_returns_value_when_present()
    {
        var parsed = new Dictionary<string, string> { ["name"] = "moss" };
        PersonaYamlParser.RequireName(parsed).Should().Be("moss");
    }
}
