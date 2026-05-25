using FluentAssertions;
using SalesArena.Bench;
using Xunit;

namespace SalesArena.Bench.Tests;

public sealed class InMemoryBenchManagerTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AddToReserve_appends_within_capacity()
    {
        var bench = new InMemoryBenchManager();
        bench.AddToReserve("a", _t0).Should().BeNull();
        bench.AddToReserve("b", _t0.AddSeconds(1)).Should().BeNull();

        bench.Reserve.Select(e => e.Persona).Should().BeEquivalentTo(new[] { "a", "b" });
        bench.IsReserve("a").Should().BeTrue();
        bench.IsActive("a").Should().BeFalse();
    }

    [Fact]
    public void AddToReserve_evicts_oldest_FIFO_at_capacity()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { MaxReserveSize = 3 });
        bench.AddToReserve("first", _t0);
        bench.AddToReserve("second", _t0.AddSeconds(10));
        bench.AddToReserve("third", _t0.AddSeconds(20));

        // Reserve now full; adding a 4th evicts "first".
        var eviction = bench.AddToReserve("fourth", _t0.AddSeconds(30));

        eviction.Should().NotBeNull();
        eviction!.Kind.Should().Be(BenchEventKinds.BenchEvicted);
        eviction.Persona.Should().Be("first");
        eviction.RelatedPersona.Should().Be("fourth");

        bench.Reserve.Select(e => e.Persona).Should().BeEquivalentTo(new[] { "second", "third", "fourth" });
    }

    [Fact]
    public void AddToReserve_rejects_duplicates()
    {
        var bench = new InMemoryBenchManager();
        bench.AddToReserve("a", _t0);
        Action act = () => bench.AddToReserve("a", _t0.AddSeconds(1));
        act.Should().Throw<BenchException>().Which.Code.Should().Be(BenchErrorCode.AlreadyOnRoster);
    }

    [Fact]
    public void Promote_moves_persona_from_reserve_to_active()
    {
        var bench = new InMemoryBenchManager();
        bench.AddToReserve("a", _t0);

        var evt = bench.Promote("a", "operator_request", _t0.AddSeconds(60));

        evt.Kind.Should().Be(BenchEventKinds.PersonaPromoted);
        evt.Persona.Should().Be("a");
        evt.Reason.Should().Be("operator_request");
        bench.IsActive("a").Should().BeTrue();
        bench.IsReserve("a").Should().BeFalse();
    }

    [Fact]
    public void Promote_throws_when_persona_not_on_reserve()
    {
        var bench = new InMemoryBenchManager();
        Action act = () => bench.Promote("nobody", "reason", _t0);
        act.Should().Throw<BenchException>().Which.Code.Should().Be(BenchErrorCode.NotOnReserve);
    }

    [Fact]
    public void Promote_throws_when_active_floor_is_full()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { MaxActiveSize = 2 });
        bench.AddToReserve("a", _t0);
        bench.AddToReserve("b", _t0.AddSeconds(1));
        bench.AddToReserve("c", _t0.AddSeconds(2));

        bench.Promote("a", "r", _t0.AddSeconds(10));
        bench.Promote("b", "r", _t0.AddSeconds(20));

        Action act = () => bench.Promote("c", "r", _t0.AddSeconds(30));
        act.Should().Throw<BenchException>().Which.Code.Should().Be(BenchErrorCode.ActiveFloorFull);
    }

    [Fact]
    public void Relegate_moves_active_persona_to_reserve()
    {
        var bench = new InMemoryBenchManager();
        bench.AddToReserve("a", _t0);
        bench.Promote("a", "r", _t0.AddSeconds(10));

        var evt = bench.Relegate("a", "operator_relegate", _t0.AddSeconds(30));

        evt.Kind.Should().Be(BenchEventKinds.PersonaRelegated);
        bench.IsReserve("a").Should().BeTrue();
        bench.IsActive("a").Should().BeFalse();
        bench.LastEvictionFromLastRelegate.Should().BeNull("reserve had capacity");
    }

    [Fact]
    public void Relegate_throws_when_persona_not_active()
    {
        var bench = new InMemoryBenchManager();
        Action act = () => bench.Relegate("nobody", "reason", _t0);
        act.Should().Throw<BenchException>().Which.Code.Should().Be(BenchErrorCode.NotActive);
    }

    [Fact]
    public void Relegate_evicts_oldest_bencher_when_reserve_full()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { MaxReserveSize = 2 });
        bench.AddToReserve("rookie", _t0);
        bench.AddToReserve("veteran", _t0.AddSeconds(10));
        bench.Promote("rookie", "r", _t0.AddSeconds(20)); // rookie active; veteran reserve
        bench.AddToReserve("walk-on", _t0.AddSeconds(30)); // now veteran + walk-on; reserve full

        bench.Relegate("rookie", "operator_relegate", _t0.AddSeconds(40));

        bench.LastEvictionFromLastRelegate.Should().NotBeNull();
        bench.LastEvictionFromLastRelegate!.Persona.Should().Be("veteran");
        bench.LastEvictionFromLastRelegate.Kind.Should().Be(BenchEventKinds.BenchEvicted);
        bench.Reserve.Select(e => e.Persona).Should().BeEquivalentTo(new[] { "walk-on", "rookie" });
    }

    [Fact]
    public void RecordContestEnd_increments_counter_for_below_floor_personas()
    {
        var bench = new InMemoryBenchManager(new BenchOptions
        {
            EloFloor = 1450,
            ConsecutiveBelowFloorBeforeRelegation = 3,
        });
        bench.AddToReserve("slacker", _t0);
        bench.Promote("slacker", "r", _t0.AddSeconds(5));

        var t = _t0.AddMinutes(60);
        bench.RecordContestEnd(new Dictionary<string, double> { ["slacker"] = 1440 }, t).Should().BeEmpty();
        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(1);

        bench.RecordContestEnd(new Dictionary<string, double> { ["slacker"] = 1430 }, t.AddMinutes(60)).Should().BeEmpty();
        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(2);

        // Third contest below floor — relegation fires.
        var events = bench.RecordContestEnd(new Dictionary<string, double> { ["slacker"] = 1420 }, t.AddMinutes(120));
        events.Should().ContainSingle(e => e.Kind == BenchEventKinds.PersonaRelegated && e.Persona == "slacker");
        bench.IsActive("slacker").Should().BeFalse();
        bench.IsReserve("slacker").Should().BeTrue();
    }

    [Fact]
    public void RecordContestEnd_resets_counter_when_persona_climbs_back_above_floor()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { EloFloor = 1450, ConsecutiveBelowFloorBeforeRelegation = 3 });
        bench.AddToReserve("clutch", _t0);
        bench.Promote("clutch", "r", _t0);

        bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1440 }, _t0.AddMinutes(60));
        bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1440 }, _t0.AddMinutes(120));
        // Counter at 2. Climb back above floor — counter resets.
        bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1470 }, _t0.AddMinutes(180));

        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(0);

        // Need 3 more dips to trigger.
        bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1440 }, _t0.AddMinutes(240)).Should().BeEmpty();
        bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1440 }, _t0.AddMinutes(300)).Should().BeEmpty();
        var thirdDip = bench.RecordContestEnd(new Dictionary<string, double> { ["clutch"] = 1440 }, _t0.AddMinutes(360));
        thirdDip.Should().ContainSingle(e => e.Kind == BenchEventKinds.PersonaRelegated);
    }

    [Fact]
    public void RecordContestEnd_missing_rating_preserves_counter()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { EloFloor = 1450, ConsecutiveBelowFloorBeforeRelegation = 3 });
        bench.AddToReserve("ghost", _t0);
        bench.Promote("ghost", "r", _t0);

        bench.RecordContestEnd(new Dictionary<string, double> { ["ghost"] = 1440 }, _t0.AddMinutes(60));
        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(1);

        // Persona missing from the ratings dict — leave counter at 1.
        bench.RecordContestEnd(new Dictionary<string, double>(), _t0.AddMinutes(120));
        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(1);
    }

    [Fact]
    public void RecordContestEnd_emits_eviction_when_relegated_persona_displaces_oldest_bencher()
    {
        var bench = new InMemoryBenchManager(new BenchOptions
        {
            MaxReserveSize = 2,
            EloFloor = 1450,
            ConsecutiveBelowFloorBeforeRelegation = 2,
        });

        // Park slacker on the active floor first, then fill the reserve to
        // capacity so the relegation has nowhere to land without an eviction.
        bench.AddToReserve("slacker", _t0);
        bench.Promote("slacker", "r", _t0.AddSeconds(1));
        bench.AddToReserve("a", _t0.AddSeconds(2));        // reserve=[a]
        bench.AddToReserve("b", _t0.AddSeconds(3));        // reserve=[a, b] — at capacity

        bench.RecordContestEnd(new Dictionary<string, double> { ["slacker"] = 1440 }, _t0.AddMinutes(60));
        var events = bench.RecordContestEnd(new Dictionary<string, double> { ["slacker"] = 1430 }, _t0.AddMinutes(120));

        events.Select(e => e.Kind).Should().BeEquivalentTo(new[] { BenchEventKinds.PersonaRelegated, BenchEventKinds.BenchEvicted });
        events.Single(e => e.Kind == BenchEventKinds.BenchEvicted).Persona.Should().Be("a");
        bench.Reserve.Select(e => e.Persona).Should().BeEquivalentTo(new[] { "b", "slacker" });
    }

    [Fact]
    public void Constructor_rejects_zero_or_negative_options()
    {
        Action zeroReserve = () => _ = new InMemoryBenchManager(new BenchOptions { MaxReserveSize = 0 });
        zeroReserve.Should().Throw<ArgumentOutOfRangeException>();
        Action zeroActive = () => _ = new InMemoryBenchManager(new BenchOptions { MaxActiveSize = 0 });
        zeroActive.Should().Throw<ArgumentOutOfRangeException>();
        Action zeroCounter = () => _ = new InMemoryBenchManager(new BenchOptions { ConsecutiveBelowFloorBeforeRelegation = 0 });
        zeroCounter.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Edge_case_exactly_at_floor_does_not_count_below()
    {
        var bench = new InMemoryBenchManager(new BenchOptions { EloFloor = 1450 });
        bench.AddToReserve("borderline", _t0);
        bench.Promote("borderline", "r", _t0);

        // Exactly at the floor — `<` not `<=`, so the counter stays 0.
        bench.RecordContestEnd(new Dictionary<string, double> { ["borderline"] = 1450 }, _t0.AddMinutes(60));
        bench.Active.Single().ConsecutiveContestsBelowThreshold.Should().Be(0);
    }
}
