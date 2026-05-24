using SalesArena.Manager.Web.Services.Bullpen;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class BullpenThoughtSanitizerTests
{
    [Theory]
    [InlineData(5_000, "under $10K")]
    [InlineData(25_000, "$10K–$50K")]
    [InlineData(75_000, "$50K–$100K")]
    [InlineData(250_000, "$100K+")]
    public void BucketDealValue_maps_ranges(decimal value, string expected)
    {
        Assert.Equal(expected, BullpenThoughtSanitizer.BucketDealValue(value));
    }

    [Fact]
    public void SanitizeThought_redacts_lead_ids_dollars_and_prospect_names()
    {
        var raw = "Closed L-42 with Acme Industries for $25,000";
        var sanitized = BullpenThoughtSanitizer.SanitizeThought(raw);

        Assert.DoesNotContain("L-42", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("$25,000", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme Industries", sanitized, StringComparison.Ordinal);
        Assert.Contains("a lead", sanitized, StringComparison.Ordinal);
        Assert.Contains("a prospect", sanitized, StringComparison.Ordinal);
    }
}
