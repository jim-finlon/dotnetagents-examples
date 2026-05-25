using System.Text.Json;
using FluentAssertions;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class NarrationCueEngineTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static (NarrationCueEngine engine, StubArenaNarrator narrator, FakeTimeProvider time)
        BuildEngine(IReadOnlyDictionary<string, IReadOnlyList<string>>? templates = null, int rateLimit = 5)
    {
        templates ??= new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [CueKinds.ContestOpened] = new[] { "{contest} opens. Make the bell ring." },
            [CueKinds.DealClosed] = new[] { "{persona} closed {lead} for {value}." },
            [CueKinds.GlengarryDripped] = new[] { "{persona} earned {count} premium leads." },
            [CueKinds.PersonaDropped] = new[] { "{count} leads off {persona}'s board." },
            [CueKinds.PersonaPromoted] = new[] { "{persona} moves from {from} to {to}." },
            [CueKinds.BellRung] = new[] { "Bell rings for {persona}: {reason}." },
        };

        var time = new FakeTimeProvider(_t0);
        var narrator = new StubArenaNarrator();
        var resolver = new InMemoryCueScriptResolver(templates);
        var limiter = new NarrationRateLimiter(maxEvents: rateLimit, window: TimeSpan.FromHours(1), timeProvider: time);
        var engine = new NarrationCueEngine(narrator, resolver, limiter, time);
        return (engine, narrator, time);
    }

    private static ArenaEvent EventOf<T>(string kind, T payload, string contestId = "contest-1")
        where T : class
        => new()
        {
            ContestId = contestId,
            Kind = kind,
            OccurredAtUtc = _t0,
            PayloadJson = JsonSerializer.Serialize(payload),
        };

    [Fact]
    public async Task DealClosed_won_speaks_cue_with_substituted_tokens()
    {
        var (engine, narrator, _) = BuildEngine();
        var payload = new DealClosedPayload("L-1", "roma", "Won", 48000m, LossReason: null);
        var (outcome, cue) = await engine.HandleAsync(EventOf(ArenaEventKinds.DealClosed, payload));

        outcome.Should().Be(CueDispatchOutcome.Spoken);
        cue.Should().NotBeNull();
        cue!.Line.Should().Be("roma closed L-1 for $48,000.");
        cue.CueKind.Should().Be(CueKinds.DealClosed);
        cue.Persona.Should().Be("roma");
        cue.LeadId.Should().Be("L-1");
        narrator.Spoken.Should().HaveCount(1);
    }

    [Fact]
    public async Task DealClosed_lost_is_not_narrated()
    {
        var (engine, narrator, _) = BuildEngine();
        var payload = new DealClosedPayload("L-2", "levene", "Lost", ValueUsd: null, LossReason: "no-budget");
        var (outcome, cue) = await engine.HandleAsync(EventOf(ArenaEventKinds.DealClosed, payload));

        outcome.Should().Be(CueDispatchOutcome.UnsupportedEvent);
        cue.Should().BeNull();
        narrator.Spoken.Should().BeEmpty();
    }

    [Fact]
    public async Task ContestPhaseChanged_started_maps_to_ContestOpened_and_bypasses_rate_limit()
    {
        // Tiny rate limit; we'll burn it down with deal-closed events first, then verify
        // the cold open still plays.
        var (engine, narrator, _) = BuildEngine(rateLimit: 1);

        await engine.HandleAsync(EventOf(
            ArenaEventKinds.DealClosed,
            new DealClosedPayload("L-A", "moss", "Won", 5000m, null)));

        // Rate limit is consumed; next non-cold-open should be denied.
        var second = await engine.HandleAsync(EventOf(
            ArenaEventKinds.DealClosed,
            new DealClosedPayload("L-B", "aaronow", "Won", 7000m, null)));
        second.Outcome.Should().Be(CueDispatchOutcome.RateLimited);

        // ContestOpened still plays.
        var opened = await engine.HandleAsync(EventOf(
            ArenaEventKinds.ContestPhaseChanged,
            new ContestPhaseChangedPayload("Started", null)));

        opened.Outcome.Should().Be(CueDispatchOutcome.Spoken);
        opened.Cue!.CueKind.Should().Be(CueKinds.ContestOpened);
        narrator.Spoken.Select(c => c.CueKind).Should().BeEquivalentTo(new[] { CueKinds.DealClosed, CueKinds.ContestOpened });
    }

    [Fact]
    public async Task ContestPhaseChanged_other_phases_are_ignored()
    {
        var (engine, _, _) = BuildEngine();
        var (outcome, cue) = await engine.HandleAsync(EventOf(
            ArenaEventKinds.ContestPhaseChanged,
            new ContestPhaseChangedPayload("Paused", "operator-stop")));

        outcome.Should().Be(CueDispatchOutcome.UnsupportedEvent);
        cue.Should().BeNull();
    }

    [Fact]
    public async Task GlengarryLeadDripped_announces_drip()
    {
        var (engine, narrator, _) = BuildEngine();
        var payload = new GlengarryLeadDrippedPayload("roma", new[] { "L-1", "L-2", "L-3" }, "Tier-1 reward");
        var (outcome, cue) = await engine.HandleAsync(EventOf(ArenaEventKinds.GlengarryLeadDripped, payload));

        outcome.Should().Be(CueDispatchOutcome.Spoken);
        cue!.Line.Should().Be("roma earned 3 premium leads.");
        narrator.Spoken.Should().ContainSingle();
    }

    [Fact]
    public async Task LeadsRevoked_maps_to_PersonaDropped_cue()
    {
        var (engine, _, _) = BuildEngine();
        var payload = new LeadsRevokedPayload("aaronow", new[] { "L-4", "L-5" }, "bottom-tier");
        var (outcome, cue) = await engine.HandleAsync(EventOf(ArenaEventKinds.LeadsRevoked, payload));

        outcome.Should().Be(CueDispatchOutcome.Spoken);
        cue!.CueKind.Should().Be(CueKinds.PersonaDropped);
        cue.Line.Should().Be("2 leads off aaronow's board.");
    }

    [Fact]
    public async Task MissingTemplate_returns_NoTemplate_and_does_not_consume_rate_limit()
    {
        var sparse = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            // DealClosed deliberately absent.
            [CueKinds.ContestOpened] = new[] { "open" },
        };
        var (engine, narrator, _) = BuildEngine(sparse, rateLimit: 2);

        var (outcome, cue) = await engine.HandleAsync(EventOf(
            ArenaEventKinds.DealClosed,
            new DealClosedPayload("L-X", "roma", "Won", 1m, null)));

        outcome.Should().Be(CueDispatchOutcome.NoTemplate);
        cue.Should().BeNull();
        narrator.Spoken.Should().BeEmpty();
    }

    [Fact]
    public async Task Muted_narrator_records_Muted_outcome_without_speaking()
    {
        var (engine, narrator, _) = BuildEngine();
        narrator.Mute();

        var (outcome, cue) = await engine.HandleAsync(EventOf(
            ArenaEventKinds.DealClosed,
            new DealClosedPayload("L-9", "roma", "Won", 1234m, null)));

        outcome.Should().Be(CueDispatchOutcome.Muted);
        cue.Should().NotBeNull();
        narrator.Spoken.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownEventKind_returns_UnsupportedEvent()
    {
        var (engine, _, _) = BuildEngine();
        var evt = new ArenaEvent
        {
            ContestId = "c1",
            Kind = "UnknownKind",
            OccurredAtUtc = _t0,
            PayloadJson = "{}",
        };
        var (outcome, _) = await engine.HandleAsync(evt);

        outcome.Should().Be(CueDispatchOutcome.UnsupportedEvent);
    }

    [Fact]
    public async Task AnnouncePromotionAsync_dispatches_PersonaPromoted_cue()
    {
        var (engine, narrator, _) = BuildEngine();
        var (outcome, cue) = await engine.AnnouncePromotionAsync(
            contestId: "c1",
            persona: "moss",
            fromTier: "bench",
            toTier: "floor");

        outcome.Should().Be(CueDispatchOutcome.Spoken);
        cue!.CueKind.Should().Be(CueKinds.PersonaPromoted);
        cue.Line.Should().Be("moss moves from bench to floor.");
        narrator.Spoken.Should().HaveCount(1);
    }

    [Fact]
    public async Task ValueUsd_null_renders_as_undisclosed_amount()
    {
        var (engine, _, _) = BuildEngine();
        var payload = new DealClosedPayload("L-3", "roma", "Won", null, null);
        var (outcome, cue) = await engine.HandleAsync(EventOf(ArenaEventKinds.DealClosed, payload));

        outcome.Should().Be(CueDispatchOutcome.Spoken);
        cue!.Line.Should().Contain("an undisclosed amount");
    }
}
