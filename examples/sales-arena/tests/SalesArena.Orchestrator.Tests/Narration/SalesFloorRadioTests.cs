using FluentAssertions;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class SalesFloorRadioTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static (SalesFloorRadio radio, StubArenaNarrator narrator, FakeTimeProvider time)
        BuildRadio(SalesFloorRadioOptions? options = null, int rateLimit = 10)
    {
        var time = new FakeTimeProvider(_t0);
        var narrator = new StubArenaNarrator();
        var templates = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [AmbientCueKinds.ContestProgress] = new[] { "{elapsed_minutes}m in. {front_runner} on {closes}." },
            [AmbientCueKinds.PersonaMomentum] = new[] { "{persona} fired {touches} touches." },
            [AmbientCueKinds.LeadAged] = new[] { "{lead} silent for {hours}h." },
            [AmbientCueKinds.GenericFiller] = new[] { "Quiet floor for {quiet_minutes}m." },
        };
        var resolver = new InMemoryCueScriptResolver(templates);
        var limiter = new NarrationRateLimiter(rateLimit, TimeSpan.FromHours(1), time);
        options ??= new SalesFloorRadioOptions { StartMuted = false };
        var radio = new SalesFloorRadio(narrator, resolver, limiter, options, time);
        return (radio, narrator, time);
    }

    private static RadioStateSnapshot Quiet(
        TimeSpan? timeSinceLastBell = null,
        TimeSpan? contestElapsed = null,
        IReadOnlyDictionary<string, int>? streaks = null,
        IReadOnlyList<AgedLead>? aged = null,
        string? frontRunner = null,
        int frontRunnerCloses = 0) => new()
    {
        ContestId = "c-1",
        TimeSinceLastBell = timeSinceLastBell ?? TimeSpan.FromMinutes(2),
        ContestElapsed = contestElapsed ?? TimeSpan.FromMinutes(2),
        PersonaTouchStreaks = streaks ?? new Dictionary<string, int>(StringComparer.Ordinal),
        AgedLeads = aged ?? Array.Empty<AgedLead>(),
        FrontRunner = frontRunner,
        FrontRunnerCloses = frontRunnerCloses,
    };

    [Fact]
    public void StartMuted_default_is_true()
    {
        var (radio, _, _) = BuildRadio(new SalesFloorRadioOptions());
        radio.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task Muted_radio_skips_speaking()
    {
        var (radio, narrator, _) = BuildRadio(new SalesFloorRadioOptions());
        var (outcome, cue) = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(30),
            aged: new[] { new AgedLead("L-1", TimeSpan.FromHours(6)) }));

        outcome.Should().Be(AmbientCueOutcome.Muted);
        cue.Should().BeNull();
        narrator.Spoken.Should().BeEmpty();
    }

    [Fact]
    public async Task UnmuteFor_expires_back_to_muted()
    {
        var (radio, _, time) = BuildRadio(new SalesFloorRadioOptions());
        radio.UnmuteFor(TimeSpan.FromMinutes(10));
        radio.IsMuted.Should().BeFalse();

        time.Advance(TimeSpan.FromMinutes(11));
        radio.IsMuted.Should().BeTrue();
    }

    [Fact]
    public async Task UnmuteFor_zero_or_negative_throws()
    {
        var (radio, _, _) = BuildRadio();
        Action zero = () => radio.UnmuteFor(TimeSpan.Zero);
        zero.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task TooSoonAfterBell_blocks_ambient_speech()
    {
        var (radio, narrator, _) = BuildRadio();
        var (outcome, _) = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromSeconds(10),
            aged: new[] { new AgedLead("L-1", TimeSpan.FromHours(8)) }));

        outcome.Should().Be(AmbientCueOutcome.TooSoonAfterBell);
        narrator.Spoken.Should().BeEmpty();
    }

    [Fact]
    public async Task LeadAged_wins_over_PersonaMomentum_and_progress()
    {
        var (radio, narrator, _) = BuildRadio();
        var snap = Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(5),
            contestElapsed: TimeSpan.FromMinutes(45),
            streaks: new Dictionary<string, int>(StringComparer.Ordinal) { ["levene"] = 5 },
            aged: new[] { new AgedLead("L-42", TimeSpan.FromHours(7)) },
            frontRunner: "roma", frontRunnerCloses: 3);

        var (outcome, cue) = await radio.TickAsync(snap);

        outcome.Should().Be(AmbientCueOutcome.Spoken);
        cue!.CueKind.Should().Be(AmbientCueKinds.LeadAged);
        cue.LeadId.Should().Be("L-42");
        cue.Line.Should().Be("L-42 silent for 7h.");
    }

    [Fact]
    public async Task PersonaMomentum_wins_over_progress()
    {
        var (radio, narrator, _) = BuildRadio();
        var snap = Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(5),
            contestElapsed: TimeSpan.FromMinutes(45),
            streaks: new Dictionary<string, int>(StringComparer.Ordinal) { ["levene"] = 5, ["moss"] = 1 },
            frontRunner: "roma", frontRunnerCloses: 3);

        var (outcome, cue) = await radio.TickAsync(snap);
        outcome.Should().Be(AmbientCueOutcome.Spoken);
        cue!.CueKind.Should().Be(AmbientCueKinds.PersonaMomentum);
        cue.Persona.Should().Be("levene");
        cue.Line.Should().Be("levene fired 5 touches.");
    }

    [Fact]
    public async Task ContestProgress_fires_once_per_bucket()
    {
        var (radio, narrator, time) = BuildRadio(new SalesFloorRadioOptions
        {
            StartMuted = false,
            ContestProgressEvery = TimeSpan.FromMinutes(15),
            MinSpacingBetweenAmbient = TimeSpan.Zero,
        });

        var first = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(5),
            contestElapsed: TimeSpan.FromMinutes(15),
            frontRunner: "roma", frontRunnerCloses: 1));
        first.Outcome.Should().Be(AmbientCueOutcome.Spoken);
        first.Cue!.CueKind.Should().Be(AmbientCueKinds.ContestProgress);

        // Same bucket — should not fire again.
        time.Advance(TimeSpan.FromMinutes(2));
        var second = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(2),
            contestElapsed: TimeSpan.FromMinutes(17),
            frontRunner: "roma", frontRunnerCloses: 1));
        second.Outcome.Should().Be(AmbientCueOutcome.NoCueSelected);

        // Next 15-minute bucket — fires again.
        time.Advance(TimeSpan.FromMinutes(14));
        var third = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(10),
            contestElapsed: TimeSpan.FromMinutes(31),
            frontRunner: "roma", frontRunnerCloses: 2));
        third.Outcome.Should().Be(AmbientCueOutcome.Spoken);
        third.Cue!.CueKind.Should().Be(AmbientCueKinds.ContestProgress);
    }

    [Fact]
    public async Task GenericFiller_fires_only_after_inactivity_threshold()
    {
        var (radio, _, _) = BuildRadio(new SalesFloorRadioOptions
        {
            StartMuted = false,
            ContestProgressEvery = TimeSpan.FromHours(99), // skip progress
            InactivityFillerThreshold = TimeSpan.FromMinutes(5),
        });

        var below = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(4),
            contestElapsed: TimeSpan.FromMinutes(8)));
        below.Outcome.Should().Be(AmbientCueOutcome.NoCueSelected);

        var above = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(5),
            contestElapsed: TimeSpan.FromMinutes(9)));
        above.Outcome.Should().Be(AmbientCueOutcome.Spoken);
        above.Cue!.CueKind.Should().Be(AmbientCueKinds.GenericFiller);
        above.Cue.Line.Should().Be("Quiet floor for 5m.");
    }

    [Fact]
    public async Task TooSoonAfterPriorAmbient_blocks_back_to_back()
    {
        var (radio, _, time) = BuildRadio(new SalesFloorRadioOptions
        {
            StartMuted = false,
            MinSpacingBetweenAmbient = TimeSpan.FromMinutes(2),
        });

        var first = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(10),
            aged: new[] { new AgedLead("L-1", TimeSpan.FromHours(6)) }));
        first.Outcome.Should().Be(AmbientCueOutcome.Spoken);

        time.Advance(TimeSpan.FromSeconds(30));
        var second = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(11),
            aged: new[] { new AgedLead("L-2", TimeSpan.FromHours(7)) }));
        second.Outcome.Should().Be(AmbientCueOutcome.TooSoonAfterPriorAmbient);

        time.Advance(TimeSpan.FromMinutes(2));
        var third = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(13),
            aged: new[] { new AgedLead("L-3", TimeSpan.FromHours(8)) }));
        third.Outcome.Should().Be(AmbientCueOutcome.Spoken);
        third.Cue!.LeadId.Should().Be("L-3");
    }

    [Fact]
    public async Task RateLimited_when_limiter_full()
    {
        var (radio, _, _) = BuildRadio(rateLimit: 1);
        var snap = Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(10),
            aged: new[] { new AgedLead("L-1", TimeSpan.FromHours(8)) });

        (await radio.TickAsync(snap)).Outcome.Should().Be(AmbientCueOutcome.Spoken);

        // Force a new ambient slot to be eligible; rate limiter should refuse.
        var (radio2, _, time2) = BuildRadio(
            new SalesFloorRadioOptions { StartMuted = false, MinSpacingBetweenAmbient = TimeSpan.Zero },
            rateLimit: 1);
        (await radio2.TickAsync(snap)).Outcome.Should().Be(AmbientCueOutcome.Spoken);
        (await radio2.TickAsync(snap)).Outcome.Should().Be(AmbientCueOutcome.RateLimited);
    }

    [Fact]
    public async Task PersonaMomentum_picks_highest_streak_with_deterministic_tiebreak()
    {
        var (radio, _, _) = BuildRadio();
        var snap = Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(5),
            streaks: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["roma"] = 5,
                ["moss"] = 5,        // tie at 5
                ["aaronow"] = 2,     // below threshold
            });

        var (outcome, cue) = await radio.TickAsync(snap);
        outcome.Should().Be(AmbientCueOutcome.Spoken);
        cue!.CueKind.Should().Be(AmbientCueKinds.PersonaMomentum);
        // OrdinalAscending tiebreak: "moss" < "roma".
        cue.Persona.Should().Be("moss");
    }

    [Fact]
    public async Task LeadAged_below_threshold_does_not_trigger()
    {
        var (radio, _, _) = BuildRadio(new SalesFloorRadioOptions
        {
            StartMuted = false,
            LeadAgedThreshold = TimeSpan.FromHours(4),
            ContestProgressEvery = TimeSpan.FromHours(99),
            InactivityFillerThreshold = TimeSpan.FromHours(99),
        });

        var (outcome, _) = await radio.TickAsync(Quiet(
            timeSinceLastBell: TimeSpan.FromMinutes(10),
            aged: new[] { new AgedLead("L-1", TimeSpan.FromHours(3)) }));

        outcome.Should().Be(AmbientCueOutcome.NoCueSelected);
    }

    [Fact]
    public async Task Unmute_makes_subsequent_unmute_persist()
    {
        var (radio, _, time) = BuildRadio(new SalesFloorRadioOptions { StartMuted = true });
        radio.Unmute();
        radio.IsMuted.Should().BeFalse();

        time.Advance(TimeSpan.FromHours(24));
        radio.IsMuted.Should().BeFalse();

        radio.Mute();
        radio.IsMuted.Should().BeTrue();
    }
}
