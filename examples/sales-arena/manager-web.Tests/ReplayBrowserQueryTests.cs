using SalesArena.Manager.Web.Services.Replay;
using SalesArena.Replay;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class ReplayBrowserQueryTests
{
    [Fact]
    public void HighlightsForLead_filters_by_lead_id_case_insensitive()
    {
        IReadOnlyList<ReplayHighlight> highlights =
        [
            new(ReplaySectionKind.ClosestCall, "Big miss", "roma", "L-1002", 50_000m, null),
            new(ReplaySectionKind.MvpTouch, "Great touch", "moss", "L-2001", null, null),
        ];

        var filtered = ReplayBrowserQuery.HighlightsForLead(highlights, "l-1002");

        Assert.Single(filtered);
        Assert.Equal("Big miss", filtered[0].Headline);
    }

    [Fact]
    public void SectionForLead_finds_section_containing_lead_id()
    {
        IReadOnlyList<ReplaySection> sections =
        [
            new(ReplaySectionKind.Leaderboard, "Board", "roma leads"),
            new(ReplaySectionKind.PersonaDealLog, "Deals", "- **L-1002** — Won"),
        ];

        var section = ReplayBrowserQuery.SectionForLead(sections, "L-1002");

        Assert.NotNull(section);
        Assert.Equal(ReplaySectionKind.PersonaDealLog, section!.Kind);
    }
}
