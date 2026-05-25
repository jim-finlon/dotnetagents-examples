using System;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace SalesArena.Proposal.Tests;

public class ProposalAgentTests
{
    private static ProposalContext SampleContext(
        decimal starter = 99m, decimal pro = 299m, decimal enterprise = 999m) => new(
            ProspectId: "greybridge",
            PersonaId: "moss",
            StarterMonthlyUsd: starter,
            ProMonthlyUsd: pro,
            EnterpriseMonthlyUsd: enterprise,
            StarterValueProps: new[] { new ValueProp("Single seat", "1 user, e-mail support") },
            ProValueProps: new[] { new ValueProp("Team", "10 seats, priority support") },
            EnterpriseValueProps: new[] { new ValueProp("Org", "Unlimited seats, named CSM") },
            EnterpriseAddOns: new[] { "SAML SSO", "Custom DPA" },
            Citations: new[] { "Mitch & Murray Pricing Card v3" });

    [Fact]
    public void Compose_HappyPath_ProducesThreeTiersInOrder()
    {
        var agent = new ProposalAgent();
        var proposal = agent.ComposeProposal(SampleContext());

        proposal.Tiers.Should().HaveCount(3);
        proposal.Tiers.Select(t => t.Tier).Should().Equal(ProposalTier.Starter, ProposalTier.Pro, ProposalTier.Enterprise);
        proposal.Tiers[0].MonthlyUsd.Should().Be(99m);
        proposal.Tiers[1].MonthlyUsd.Should().Be(299m);
        proposal.Tiers[2].MonthlyUsd.Should().Be(999m);
    }

    [Fact]
    public void Compose_EnterpriseAddOns_Preserved()
    {
        var agent = new ProposalAgent();
        var proposal = agent.ComposeProposal(SampleContext());

        proposal.Tiers[2].AddOns.Should().Equal(new[] { "SAML SSO", "Custom DPA" });
    }

    [Fact]
    public void Compose_ValueProps_Preserved()
    {
        var agent = new ProposalAgent();
        var proposal = agent.ComposeProposal(SampleContext());

        proposal.Tiers[0].ValueProps.Should().ContainSingle().Which.Title.Should().Be("Single seat");
        proposal.Tiers[1].ValueProps.Should().ContainSingle().Which.Title.Should().Be("Team");
        proposal.Tiers[2].ValueProps.Should().ContainSingle().Which.Title.Should().Be("Org");
    }

    [Fact]
    public void Compose_BlankProspect_Throws()
    {
        var agent = new ProposalAgent();
        var ctx = SampleContext() with { ProspectId = "   " };
        var act = () => agent.ComposeProposal(ctx);
        act.Should().Throw<ArgumentException>().WithMessage("*ProspectId*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Compose_NonPositivePrice_Throws(decimal price)
    {
        var agent = new ProposalAgent();
        var ctx = SampleContext(starter: price);
        var act = () => agent.ComposeProposal(ctx);
        act.Should().Throw<ArgumentException>().WithMessage("*tier prices*");
    }

    [Fact]
    public void ToMarkdown_IsDeterministic()
    {
        var proposal = new ProposalAgent().ComposeProposal(SampleContext());

        var md1 = proposal.ToMarkdown();
        var md2 = proposal.ToMarkdown();

        md1.Should().Be(md2);
    }

    [Fact]
    public void ToMarkdown_ContainsAllTiersAndCitation()
    {
        var md = new ProposalAgent().ComposeProposal(SampleContext()).ToMarkdown();

        md.Should().Contain("# Proposal — greybridge / persona: moss");
        md.Should().Contain("## Starter");
        md.Should().Contain("## Pro");
        md.Should().Contain("## Enterprise");
        md.Should().Contain("[1] Mitch & Murray Pricing Card v3");
    }

    [Fact]
    public void ToMarkdown_NoCitations_RendersNone()
    {
        var ctx = SampleContext() with { Citations = Array.Empty<string>() };
        var md = new ProposalAgent().ComposeProposal(ctx).ToMarkdown();

        md.Should().Contain("## Citations");
        md.Should().Contain("_None._");
    }
}
