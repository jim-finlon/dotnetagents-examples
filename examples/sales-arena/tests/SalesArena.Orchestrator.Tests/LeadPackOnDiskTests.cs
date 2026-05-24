using FluentAssertions;
using SalesArena.Orchestrator.LeadPool;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// SA-06-03 — on-disk lead packs (v1 Glengarry + v2 SaaS-renewal) load without error.
/// </summary>
public sealed class LeadPackOnDiskTests
{
    [Fact]
    public async Task GlengarryV1_and_SaasRenewalV2_packs_load_without_error()
    {
        var glengarry = LocatePack("glengarry-v1", "leads.json");
        var saasRenewal = LocatePack("saas-renewal-v1", "leads.json");

        if (glengarry is null || saasRenewal is null)
        {
            return;
        }

        var pool = new InMemoryLeadPool();

        var v1 = await pool.LoadAsync(glengarry);
        v1.Version.Should().Be("v1");
        v1.Leads.Should().HaveCount(200);

        var v2 = await pool.LoadAsync(saasRenewal);
        v2.Version.Should().Be("v2");
        v2.Leads.Should().HaveCount(100);
        v2.Leads.Should().AllSatisfy(l =>
        {
            l.CustomerTier.Should().NotBeNullOrEmpty();
            l.Mrr.Should().NotBeNull();
            l.RenewalDate.Should().NotBeNullOrEmpty();
            l.ChurnRiskScore.Should().NotBeNull();
        });

        // Reload v1 after v2 to prove mixed-version sessions do not crash.
        var v1Again = await pool.LoadAsync(glengarry);
        v1Again.Version.Should().Be("v1");
        v1Again.Leads.Should().HaveCount(200);
    }

    [Fact]
    public async Task SaasRenewalV2_pack_loads_under_100ms()
    {
        var path = LocatePack("saas-renewal-v1", "leads.json");
        if (path is null)
        {
            return;
        }

        var pool = new InMemoryLeadPool();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await pool.LoadAsync(path);
        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    private static string? LocatePack(string packDir, string fileName)
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "samples", "sales-arena", "lead-packs", packDir, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }
}
