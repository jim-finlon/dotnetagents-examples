using FluentAssertions;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class WalkOnPlayerTests
{
    private static readonly DateTimeOffset _t0 = new(2026, 5, 18, 12, 0, 0, TimeSpan.Zero);

    private static InMemoryWalkOnPlayer Build(bool muted = false)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["roma"] = "/sound/roma.wav",
            ["levene"] = "/sound/levene.wav",
        };
        return new InMemoryWalkOnPlayer(map, startMuted: muted);
    }

    [Fact]
    public void Play_resolves_to_file_path_for_known_persona()
    {
        var player = Build();
        var (decision, request) = player.Play("roma", _t0);
        decision.Should().Be(WalkOnDecision.Played);
        request.Should().NotBeNull();
        request!.Persona.Should().Be("roma");
        request.FilePath.Should().Be("/sound/roma.wav");
        request.RequestedAtUtc.Should().Be(_t0);
    }

    [Fact]
    public void Play_for_unmapped_persona_returns_NoWalkOnForPersona()
    {
        var player = Build();
        var (decision, request) = player.Play("ghost", _t0);
        decision.Should().Be(WalkOnDecision.NoWalkOnForPersona);
        request.Should().BeNull();
    }

    [Fact]
    public void Play_when_muted_returns_Muted_with_request_intact()
    {
        var player = Build(muted: true);
        var (decision, request) = player.Play("roma", _t0);
        decision.Should().Be(WalkOnDecision.Muted);
        request.Should().NotBeNull("mute returns a request so the operator UI can show what would have played");
    }

    [Fact]
    public void Play_during_bell_returns_DeferredForBell()
    {
        var player = Build();
        player.NotifyBellStart();
        var (decision, request) = player.Play("levene", _t0);
        decision.Should().Be(WalkOnDecision.DeferredForBell);
        request!.FilePath.Should().Be("/sound/levene.wav");
    }

    [Fact]
    public void NotifyBellEnd_re_enables_playback()
    {
        var player = Build();
        player.NotifyBellStart();
        player.Play("roma", _t0).Decision.Should().Be(WalkOnDecision.DeferredForBell);
        player.NotifyBellEnd();
        player.Play("roma", _t0).Decision.Should().Be(WalkOnDecision.Played);
    }

    [Fact]
    public void Mute_then_Unmute_round_trips()
    {
        var player = Build();
        player.IsMuted.Should().BeFalse();
        player.Mute();
        player.IsMuted.Should().BeTrue();
        player.Unmute();
        player.IsMuted.Should().BeFalse();
    }

    [Fact]
    public void RegisterWalkOn_overrides_or_adds_persona_to_file_mapping()
    {
        var player = Build();
        player.RegisterWalkOn("moss", "/custom/moss-jazz.wav");
        var (decision, request) = player.Play("moss", _t0);
        decision.Should().Be(WalkOnDecision.Played);
        request!.FilePath.Should().Be("/custom/moss-jazz.wav");
    }

    [Fact]
    public void DefaultMap_contains_all_six_base_personas()
    {
        var map = InMemoryWalkOnPlayer.DefaultMap("/audio/walk-ons");
        map.Keys.Should().BeEquivalentTo(new[]
        {
            "roma", "levene", "moss", "aaronow", "williamson", "mitch-and-murray",
        });
        map["roma"].Should().EndWith("roma.wav");
        map["mitch-and-murray"].Should().EndWith("mitch-and-murray.wav");
    }

    [Fact]
    public void RegisterWalkOn_rejects_empty_inputs()
    {
        var player = Build();
        Action emptyPersona = () => player.RegisterWalkOn("", "/x.wav");
        Action emptyPath = () => player.RegisterWalkOn("roma", "");
        emptyPersona.Should().Throw<ArgumentException>();
        emptyPath.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Play_when_muted_AND_bell_ringing_prioritizes_Muted()
    {
        var player = Build(muted: true);
        player.NotifyBellStart();
        var (decision, _) = player.Play("roma", _t0);
        decision.Should().Be(WalkOnDecision.Muted,
            "mute is the operator's master kill-switch; bell coordination is downstream of it");
    }
}
