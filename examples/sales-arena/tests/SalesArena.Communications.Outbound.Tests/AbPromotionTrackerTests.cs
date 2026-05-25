using FluentAssertions;
using SalesArena.Communications.Outbound;
using SalesArena.OutreachTemplates;
using Xunit;

namespace SalesArena.Communications.Outbound.Tests;

public sealed class AbPromotionTrackerTests
{
    [Fact]
    public void Promotes_winner_after_minimum_sends_and_significant_reply_gap()
    {
        var tracker = new AbPromotionTracker();
        const string persona = "roma";
        const string channel = "email";
        var variants = OutreachTemplateCatalog.VariantIds.ToList();

        for (var i = 0; i < AbPromotionTracker.MinimumSendsPerVariant; i++)
        {
            tracker.RecordSend(persona, channel, "variant-1");
            tracker.RecordReply(persona, channel, "variant-1");
        }

        for (var i = 0; i < AbPromotionTracker.MinimumSendsPerVariant; i++)
        {
            tracker.RecordSend(persona, channel, "variant-2");
        }

        tracker.GetPromotedVariant(persona, channel).Should().Be("variant-1");
        tracker.SelectVariantForSend(persona, channel, variants).Should().Be("variant-1");
    }
}
