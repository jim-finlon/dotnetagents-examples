using SalesArena.Manager.Web.Services.LeadPool;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class LeadPoolQueryTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ApplyFilter_Warm_returns_only_warm_leads()
    {
        var leads = SampleLeads();
        var warm = LeadPoolQuery.ApplyFilter(leads, LeadPoolFilterChip.Warm, Now);
        Assert.All(warm, l => Assert.Equal(LeadPoolFilterChip.Warm, l.ResolveFilterChip(Now)));
        Assert.Contains(warm, l => l.LeadId == "warm-1");
    }

    [Fact]
    public void ApplySort_LeadScore_descending_orders_highest_first()
    {
        var leads = SampleLeads();
        var sorted = LeadPoolQuery.ApplySort(leads, LeadPoolSortKey.LeadScore, descending: true);
        Assert.Equal("high", sorted[0].LeadId);
        Assert.Equal("low", sorted[^1].LeadId);
    }

    private static IReadOnlyList<LeadPoolLead> SampleLeads() =>
    [
        new(
            "hot-1",
            "Hot Co",
            "moss",
            "Prospect",
            70,
            Now.AddHours(-2),
            0,
            HasReply: true,
            IsClosedWon: false,
            IsClosedLost: false,
            []),
        new(
            "warm-1",
            "Warm Co",
            "romano",
            "Qualified",
            55,
            Now.AddDays(-3),
            1,
            HasReply: false,
            IsClosedWon: false,
            IsClosedLost: false,
            []),
        new(
            "high",
            "High Score",
            "levene",
            "Proposal",
            99,
            Now.AddHours(-10),
            0,
            HasReply: true,
            IsClosedWon: false,
            IsClosedLost: false,
            []),
        new(
            "low",
            "Low Score",
            "aaronow",
            "Prospect",
            10,
            Now.AddHours(-8),
            0,
            HasReply: true,
            IsClosedWon: false,
            IsClosedLost: false,
            []),
    ];
}
