using FluentAssertions;
using SalesArena.Communications.Outbound;
using SalesArena.OutreachTemplates;
using Xunit;

namespace SalesArena.Communications.Outbound.Tests;

public sealed class OutboundDrafterTests
{
    [Theory]
    [InlineData("roma", "email")]
    [InlineData("levene", "sms")]
    [InlineData("moss", "chat")]
    [InlineData("aaronow", "linkedin")]
    public async Task DraftAsync_substitutes_prospect_tokens(string personaId, string channel)
    {
        var tracker = new AbPromotionTracker();
        var loader = new OutreachTemplateLoader(OutboundTestHost.PersonasRoot);
        var drafter = new OutboundDrafter(loader, tracker);
        var prospect = OutboundTestHost.SampleProspect;

        var draft = await drafter.DraftAsync(prospect, personaId, channel, OutboundIntent.Introduce);

        draft.PersonaId.Should().Be(personaId);
        draft.Channel.Should().Be(channel);
        draft.Body.Should().Contain(prospect.FirstName);
        draft.VariantId.Should().StartWith("variant-");
        draft.Confidence.Should().BeInRange(0.5, 1.0);
        draft.Hypothesis.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DraftAsync_email_extracts_subject_line()
    {
        var tracker = new AbPromotionTracker();
        var loader = new OutreachTemplateLoader(OutboundTestHost.PersonasRoot);
        var drafter = new OutboundDrafter(loader, tracker);

        var draft = await drafter.DraftAsync(
            OutboundTestHost.SampleProspect,
            "roma",
            "email",
            OutboundIntent.FollowUp);

        draft.Subject.Should().NotBeNullOrWhiteSpace();
        draft.Body.Should().NotContain("Subject:");
        draft.Body.Should().Contain("Alex");
    }
}
