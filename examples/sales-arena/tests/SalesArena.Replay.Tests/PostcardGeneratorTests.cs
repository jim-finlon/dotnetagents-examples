using System.Xml.Linq;
using FluentAssertions;
using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Replay.Postcard;
using Xunit;

namespace SalesArena.Replay.Tests;

/// <summary>
/// Pins the Postcard generator: well-formed SVG, all 3 styles render,
/// token substitutions are XML-escaped, empty leaderboard handled.
/// </summary>
public sealed class PostcardGeneratorTests
{
    [Fact]
    public void Generate_returns_well_formed_xml_svg_document()
    {
        var generator = new PostcardGenerator();
        var board = NewBoard(("roma", 100_000m, 2));

        var svg = generator.Generate(board);

        // Round-trip through XLinq: parsing without throwing == well-formed.
        var doc = XDocument.Parse(svg);
        doc.Root!.Name.LocalName.Should().Be("svg");
        doc.Root.Attribute("xmlns")!.Value.Should().Be("http://www.w3.org/2000/svg");
    }

    [Fact]
    public void Generate_renders_winner_persona_revenue_and_winrate()
    {
        var generator = new PostcardGenerator();
        var board = NewBoard(("roma", 100_000m, 4, 1));

        var svg = generator.Generate(board, new PostcardOptions(ContestDisplayName: "Tuesday Bake-Off"));

        svg.Should().Contain("ROMA");                  // winner upper-cased
        svg.Should().Contain("$100,000");              // revenue
        svg.Should().Contain("4");                     // deals won
        svg.Should().Contain("80%");                   // win rate (4/(4+1))
        svg.Should().Contain("Tuesday Bake-Off");      // contest display name
        svg.Should().Contain("Coffee's for closers");  // signature line
    }

    [Theory]
    [InlineData(PostcardStyle.Vintage)]
    [InlineData(PostcardStyle.Modern)]
    [InlineData(PostcardStyle.Neon)]
    public void Generate_emits_well_formed_svg_for_every_style(PostcardStyle style)
    {
        var generator = new PostcardGenerator();
        var board = NewBoard(("levene", 50_000m, 3));

        var svg = generator.Generate(board, new PostcardOptions(Style: style));

        XDocument.Parse(svg).Root!.Name.LocalName.Should().Be("svg");
        // Each style ships with at least the headline + accent palette tokens visible.
        svg.Should().Contain("LEVENE");
        svg.Should().Contain("$50,000");
    }

    [Fact]
    public void Generate_escapes_persona_and_contest_names_for_XML_safety()
    {
        var generator = new PostcardGenerator();
        // Adversarial persona name with characters that would break raw SVG.
        var board = new SalesArena.Orchestrator.Leaderboard.Leaderboard(
            "C&E<><contest>",
            ScoringConfigIds.ByRevenue,
            DateTimeOffset.UtcNow,
            new[]
            {
                new LeaderboardRow(
                    Position: 1,
                    Tier: LeaderboardTier.Cadillac,
                    Persona: "<script>alert('xss')</script>",
                    Score: 1,
                    RevenueUsd: 1_000m,
                    DealsWon: 1,
                    DealsLost: 0,
                    WinRate: 1.0),
            });

        var svg = generator.Generate(board);

        // Document must still parse as XML.
        XDocument.Parse(svg);
        // The hostile string must not appear unescaped.
        svg.Should().NotContain("<script>");
        // The escaped variant should be present.
        svg.Should().Contain("&lt;");
    }

    [Fact]
    public void Generate_renders_catchphrase_when_supplied()
    {
        var generator = new PostcardGenerator();
        var board = NewBoard(("moss", 25_000m, 2));

        var svg = generator.Generate(board, new PostcardOptions(
            Catchphrase: "They tell you no three times before they say yes."));

        svg.Should().Contain("They tell you no three times before they say yes.");
    }

    [Fact]
    public void Generate_with_empty_leaderboard_returns_empty_postcard_svg()
    {
        var generator = new PostcardGenerator();
        var emptyBoard = new SalesArena.Orchestrator.Leaderboard.Leaderboard(
            "empty",
            ScoringConfigIds.ByRevenue,
            DateTimeOffset.UtcNow,
            Array.Empty<LeaderboardRow>());

        var svg = generator.Generate(emptyBoard);

        XDocument.Parse(svg).Root!.Name.LocalName.Should().Be("svg");
        svg.Should().Contain("No contest results yet");
    }

    [Fact]
    public void Generate_with_no_Cadillac_tier_falls_back_to_first_entry()
    {
        var generator = new PostcardGenerator();
        // Build a board with no Cadillac (manually crafted): first entry is SteakKnives.
        var board = new SalesArena.Orchestrator.Leaderboard.Leaderboard(
            "no-cadillac",
            ScoringConfigIds.ByRevenue,
            DateTimeOffset.UtcNow,
            new[]
            {
                new LeaderboardRow(
                    Position: 1,
                    Tier: LeaderboardTier.SteakKnives,
                    Persona: "aaronow",
                    Score: 10,
                    RevenueUsd: 10_000m,
                    DealsWon: 1,
                    DealsLost: 0,
                    WinRate: 1.0),
            });

        var svg = generator.Generate(board);

        svg.Should().Contain("AARONOW");
    }

    // ---- helpers --------------------------------------------------------

    private static SalesArena.Orchestrator.Leaderboard.Leaderboard NewBoard(params (string Persona, decimal Revenue, int Wins)[] rows)
    {
        var entries = rows.Select((r, idx) => new LeaderboardRow(
            Position: idx + 1,
            Tier: idx == 0 ? LeaderboardTier.Cadillac : LeaderboardTier.SteakKnives,
            Persona: r.Persona,
            Score: (double)r.Revenue,
            RevenueUsd: r.Revenue,
            DealsWon: r.Wins,
            DealsLost: 0,
            WinRate: r.Wins > 0 ? 1.0 : 0.0)).ToList();
        return new SalesArena.Orchestrator.Leaderboard.Leaderboard("test-contest", ScoringConfigIds.ByRevenue, DateTimeOffset.UtcNow, entries);
    }

    private static SalesArena.Orchestrator.Leaderboard.Leaderboard NewBoard((string Persona, decimal Revenue, int Wins, int Losses) row)
    {
        var winRate = (row.Wins + row.Losses) > 0 ? (double)row.Wins / (row.Wins + row.Losses) : 0.0;
        var entries = new[]
        {
            new LeaderboardRow(
                Position: 1,
                Tier: LeaderboardTier.Cadillac,
                Persona: row.Persona,
                Score: (double)row.Revenue,
                RevenueUsd: row.Revenue,
                DealsWon: row.Wins,
                DealsLost: row.Losses,
                WinRate: winRate),
        };
        return new SalesArena.Orchestrator.Leaderboard.Leaderboard("test-contest", ScoringConfigIds.ByRevenue, DateTimeOffset.UtcNow, entries);
    }
}
