using System.Text.Json;
using FluentAssertions;
using SalesArena.Orchestrator.LeadPool;
using Xunit;

namespace SalesArena.Orchestrator.Tests;

/// <summary>
/// Pins the lead-pool contract: atomic assignment under concurrency, tier
/// filtering, release-back-to-pool semantics, validation of malformed packs,
/// and integration with the real Glengarry-v1 pack from SA-05-02.
/// </summary>
public sealed class InMemoryLeadPoolTests
{
    [Fact]
    public async Task LoadAsync_reads_a_tiny_synthetic_pack_and_indexes_by_id()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(new[]
        {
            FixtureLead("L-0001", "glengarry"),
            FixtureLead("L-0002", "cold"),
            FixtureLead("L-0003", "cold"),
        });
        try
        {
            var pack = await pool.LoadAsync(path);
            pack.Leads.Should().HaveCount(3);
            pack.Synthetic.Should().BeTrue();
            pool.Snapshot().Total.Should().Be(3);
            pool.Snapshot().Available.Should().Be(3);
            pool.GetAssignedPod("L-0001").Should().BeNull();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AssignAsync_distributes_atomically_and_refuses_double_claim()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(NLeads(5));
        try
        {
            await pool.LoadAsync(path);

            var roma = await pool.AssignAsync("roma", 2);
            var levene = await pool.AssignAsync("levene", 2);

            roma.Select(l => l.Id).Should().Equal(new[] { "L-0001", "L-0002" });
            levene.Select(l => l.Id).Should().Equal(new[] { "L-0003", "L-0004" });

            pool.GetAssignedPod("L-0001").Should().Be("roma");
            pool.GetAssignedPod("L-0003").Should().Be("levene");
            pool.GetAssignedPod("L-0005").Should().BeNull();

            var snap = pool.Snapshot();
            snap.Total.Should().Be(5);
            snap.Available.Should().Be(1);
            snap.Assigned.Should().Be(4);
            snap.Released.Should().Be(0);
            snap.AssignedByPod.Should().BeEquivalentTo(new Dictionary<string, int> { { "roma", 2 }, { "levene", 2 } });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AssignAsync_with_tier_filter_only_picks_matching_leads()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(new[]
        {
            FixtureLead("L-0001", "cold"),
            FixtureLead("L-0002", "glengarry"),
            FixtureLead("L-0003", "cold"),
            FixtureLead("L-0004", "glengarry"),
            FixtureLead("L-0005", "cold"),
        });
        try
        {
            await pool.LoadAsync(path);

            var glengarry = await pool.AssignAsync("roma", 2, tier: "glengarry");
            glengarry.Select(l => l.Id).Should().Equal(new[] { "L-0002", "L-0004" });

            var cold = await pool.AssignAsync("levene", 2, tier: "cold");
            cold.Select(l => l.Id).Should().Equal(new[] { "L-0001", "L-0003" });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AssignAsync_refuses_when_insufficient_leads_available()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(NLeads(3));
        try
        {
            await pool.LoadAsync(path);
            await pool.AssignAsync("roma", 2);

            var act = async () => await pool.AssignAsync("levene", 5);
            var ex = await act.Should().ThrowAsync<LeadPoolException>();
            ex.Which.Code.Should().Be(LeadPoolException.Codes.InsufficientAvailable);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReleaseAsync_returns_leads_to_pool_and_makes_them_reassignable()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(NLeads(3));
        try
        {
            await pool.LoadAsync(path);
            var batch = await pool.AssignAsync("roma", 3);
            (await ToReleased(pool, "roma", batch.Select(b => b.Id))).Should().BeTrue();

            var snap = pool.Snapshot();
            snap.Assigned.Should().Be(0);
            snap.Released.Should().Be(3);

            // A released lead can be re-assigned (to the same or a different pod).
            var levene = await pool.AssignAsync("levene", 2);
            levene.Select(l => l.Id).Should().Equal(new[] { "L-0001", "L-0002" });
            pool.GetAssignedPod("L-0001").Should().Be("levene");
            pool.Snapshot().Released.Should().Be(1);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ReleaseAsync_refuses_if_lead_is_not_owned_by_pod()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(NLeads(3));
        try
        {
            await pool.LoadAsync(path);
            await pool.AssignAsync("roma", 2);

            // levene tries to release roma's leads
            var act = async () => await pool.ReleaseAsync("levene", new[] { "L-0001" });
            var ex = await act.Should().ThrowAsync<LeadPoolException>();
            ex.Which.Code.Should().Be(LeadPoolException.Codes.LeadNotAssignedToPod);

            // Unknown lead id
            var unknown = async () => await pool.ReleaseAsync("roma", new[] { "L-9999" });
            var ex2 = await unknown.Should().ThrowAsync<LeadPoolException>();
            ex2.Which.Code.Should().Be(LeadPoolException.Codes.LeadUnknown);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_AssignAsync_never_double_claims_a_lead()
    {
        var pool = new InMemoryLeadPool();
        var path = WriteFixturePack(NLeads(100));
        try
        {
            await pool.LoadAsync(path);

            // 10 pods concurrently each claim 10 leads = 100 total exactly.
            var tasks = Enumerable.Range(0, 10)
                .Select(i => Task.Run(() => pool.AssignAsync($"pod-{i}", 10)))
                .ToArray();
            await Task.WhenAll(tasks);

            // Every lead-id must appear in exactly one pod's claim.
            var allClaimed = tasks.SelectMany(t => t.Result).Select(l => l.Id).ToList();
            allClaimed.Should().HaveCount(100);
            allClaimed.Distinct().Should().HaveCount(100);

            var snap = pool.Snapshot();
            snap.Assigned.Should().Be(100);
            snap.Available.Should().Be(0);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_refuses_pack_without_synthetic_flag()
    {
        var pool = new InMemoryLeadPool();
        var path = Path.GetTempFileName();
        try
        {
            // Hand-write a malformed pack with synthetic: false
            File.WriteAllText(path, """
                { "version": "v1", "name": "bad", "description": "no synthetic flag", "synthetic": false,
                  "leads": [{ "id": "L-0001", "tier": "cold", "company": { "name": "X" } }] }
                """);
            var act = async () => await pool.LoadAsync(path);
            var ex = await act.Should().ThrowAsync<LeadPoolException>();
            ex.Which.Code.Should().Be(LeadPoolException.Codes.PackInvalid);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_refuses_pack_with_wrong_version()
    {
        var pool = new InMemoryLeadPool();
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, """
                { "version": "v0", "name": "x", "description": "x", "synthetic": true,
                  "leads": [{ "id": "L-0001", "tier": "cold", "company": { "name": "X" } }] }
                """);
            var act = async () => await pool.LoadAsync(path);
            var ex = await act.Should().ThrowAsync<LeadPoolException>();
            ex.Which.Code.Should().Be(LeadPoolException.Codes.PackInvalid);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task AssignAsync_before_LoadAsync_throws_NOT_LOADED()
    {
        var pool = new InMemoryLeadPool();
        var act = async () => await pool.AssignAsync("roma", 1);
        var ex = await act.Should().ThrowAsync<LeadPoolException>();
        ex.Which.Code.Should().Be(LeadPoolException.Codes.PackNotLoaded);
    }

    [Fact]
    public async Task RealGlengarryV1_pack_loads_and_assigns()
    {
        // Integration check against the actual SA-05-02 lead pack.
        var packPath = LocateGlengarryV1();
        if (packPath is null)
        {
            // The pack is part of the repo; skip if missing (e.g. consumers who pull just this dir).
            return;
        }

        var pool = new InMemoryLeadPool();
        var pack = await pool.LoadAsync(packPath);

        pack.Leads.Should().HaveCount(200);
        pack.Leads.Count(l => l.Tier == "glengarry").Should().Be(20);
        pack.Leads.Count(l => l.Tier == "cold").Should().Be(180);

        // Roma claims 5 glengarry premium leads; Levene grabs 40 cold leads.
        var roma = await pool.AssignAsync("roma", 5, tier: "glengarry");
        var levene = await pool.AssignAsync("levene", 40, tier: "cold");

        roma.Should().HaveCount(5);
        roma.Should().AllSatisfy(l => l.Tier.Should().Be("glengarry"));
        levene.Should().HaveCount(40);
        levene.Should().AllSatisfy(l => l.Tier.Should().Be("cold"));

        var snap = pool.Snapshot();
        snap.Assigned.Should().Be(45);
        snap.AssignedByPod["roma"].Should().Be(5);
        snap.AssignedByPod["levene"].Should().Be(40);
        snap.AvailableByTier["glengarry"].Should().Be(15);
        snap.AvailableByTier["cold"].Should().Be(140);
    }

    // ---- helpers ---------------------------------------------------------

    private static IEnumerable<object> NLeads(int n)
    {
        for (var i = 1; i <= n; i++)
        {
            yield return FixtureLead($"L-{i:0000}", i % 5 == 0 ? "glengarry" : "cold");
        }
    }

    private static object FixtureLead(string id, string tier) => new
    {
        id,
        tier,
        company = new { name = $"Acme {id}", industry = "saas", size = "smb" },
    };

    private static string WriteFixturePack(IEnumerable<object> leads)
    {
        var pack = new
        {
            version = "v1",
            name = "test-fixture",
            description = "Tiny fixture pack for unit tests.",
            synthetic = true,
            leads = leads.ToArray(),
        };

        var path = Path.GetTempFileName();
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false });
        File.WriteAllText(path, json);
        return path;
    }

    private static async Task<bool> ToReleased(ILeadPool pool, string podId, IEnumerable<string> ids)
    {
        await pool.ReleaseAsync(podId, ids);
        return true;
    }

    private static string? LocateGlengarryV1()
    {
        // Walk up from the test binary toward repo root, looking for samples/sales-arena/lead-packs/glengarry-v1/leads.json.
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 12 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "samples", "sales-arena", "lead-packs", "glengarry-v1", "leads.json");
            if (File.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        return null;
    }
}
