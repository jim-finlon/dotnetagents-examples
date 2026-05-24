using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace SalesArena.Research.Tests;

public class ResearchAgentTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static ResearchAgent BuildAgent(
        IReadOnlyList<PublicFeedItem>? feed = null,
        IReadOnlyList<CompanyFact>? facts = null,
        IReadOnlyList<KnownContact>? contacts = null) =>
        new(
            new InMemoryPublicFeedAdapter(feed ?? Array.Empty<PublicFeedItem>()),
            new InMemoryCompanyFactProvider(facts ?? Array.Empty<CompanyFact>()),
            new InMemoryKnownContactProvider(contacts ?? Array.Empty<KnownContact>()));

    [Fact]
    public async Task HappyPath_AggregatesAllSections()
    {
        var agent = BuildAgent(
            feed: new[]
            {
                new PublicFeedItem("Greybridge raises Series B", "https://strathamwire.example/news/1", T0, "Stratham Wire"),
                new PublicFeedItem("Greybridge opens Austin office", "https://strathamwire.example/news/2", T0.AddDays(-3), "Stratham Wire"),
            },
            facts: new[]
            {
                new CompanyFact("Headcount", "320", "10-K"),
                new CompanyFact("HQ", "Boston, MA"),
            },
            contacts: new[]
            {
                new KnownContact("Sheila Kenmore", "VP Ops", "linkedin.com/sheila"),
            });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            ProspectId: "greybridge",
            PersonaId: "moss",
            AllowedFeedHosts: new[] { "strathamwire.example" }));

        pager.ProspectId.Should().Be("greybridge");
        pager.CompanySnapshot.Should().HaveCount(2);
        pager.RecentSignals.Should().HaveCount(2);
        pager.KnownContacts.Should().ContainSingle();
        pager.SuggestedAngles.Should().NotBeEmpty();
        pager.Citations.Should().HaveCount(3); // 2 signals + 1 sourced fact
    }

    [Fact]
    public async Task AllowList_FiltersOutNonAllowedHosts()
    {
        var agent = BuildAgent(
            feed: new[]
            {
                new PublicFeedItem("Allowed", "https://strathamwire.example/a", T0, "Stratham"),
                new PublicFeedItem("Blocked", "https://pipelinevelocity.example/b", T0, "Pipeline"),
            });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", new[] { "strathamwire.example" }));

        pager.RecentSignals.Should().ContainSingle().Which.Title.Should().Be("Allowed");
    }

    [Fact]
    public async Task EmptyAllowList_LetsAllSignalsThrough()
    {
        var agent = BuildAgent(
            feed: new[] { new PublicFeedItem("A", "https://anywhere.example/x", T0, "Anywhere") });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", Array.Empty<string>()));

        pager.RecentSignals.Should().ContainSingle();
    }

    [Fact]
    public async Task SignalOrdering_NewestFirstThenOrdinalTitle()
    {
        var agent = BuildAgent(
            feed: new[]
            {
                new PublicFeedItem("B", "https://x.example/b", T0,                "x"),
                new PublicFeedItem("A", "https://x.example/a", T0,                "x"),
                new PublicFeedItem("Older", "https://x.example/older", T0.AddDays(-2), "x"),
            });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", new[] { "x.example" }));

        pager.RecentSignals.Should().HaveCount(3);
        pager.RecentSignals[0].Title.Should().Be("A");
        pager.RecentSignals[1].Title.Should().Be("B");
        pager.RecentSignals[2].Title.Should().Be("Older");
    }

    [Fact]
    public async Task SuggestedAngles_LeadWithLatestSignal()
    {
        var agent = BuildAgent(
            feed: new[] { new PublicFeedItem("Greybridge launches X", "https://x.example/n", T0, "x") });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", new[] { "x.example" }));

        pager.SuggestedAngles[0].Should().Contain("Greybridge launches X");
    }

    [Fact]
    public async Task NoSignalsNoFacts_FallbackAngle()
    {
        var agent = BuildAgent();

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", Array.Empty<string>()));

        pager.SuggestedAngles.Should().ContainSingle()
            .Which.Should().Contain("discovery");
    }

    [Fact]
    public async Task BlankProspect_Throws()
    {
        var agent = BuildAgent();
        Func<System.Threading.Tasks.Task> act = () => agent.AssembleOnePagerAsync(
            new ResearchRequest("   ", "moss", Array.Empty<string>()));
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ToMarkdown_DeterministicSnapshot()
    {
        var agent = BuildAgent(
            facts: new[] { new CompanyFact("HQ", "Boston, MA") },
            contacts: new[] { new KnownContact("Sheila", "VP", "linkedin/sheila") });

        var pager = await agent.AssembleOnePagerAsync(new ResearchRequest(
            "greybridge", "moss", Array.Empty<string>()));

        var md1 = pager.ToMarkdown();
        var md2 = pager.ToMarkdown();

        md1.Should().Be(md2);
        md1.Should().Contain("# Research One-Pager — greybridge / moss");
        md1.Should().Contain("## Company Snapshot");
        md1.Should().Contain("## Recent Signals");
        md1.Should().Contain("## Known Contacts");
        md1.Should().Contain("## Suggested Angles");
        md1.Should().Contain("## Citations");
    }
}
