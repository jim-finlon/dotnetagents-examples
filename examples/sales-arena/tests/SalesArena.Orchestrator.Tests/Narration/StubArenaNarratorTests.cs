using FluentAssertions;
using SalesArena.Orchestrator.Narration;
using Xunit;

namespace SalesArena.Orchestrator.Tests.Narration;

public sealed class StubArenaNarratorTests
{
    private static ArenaCue Cue(string line = "test line") => new(
        ContestId: "c1",
        CueKind: CueKinds.DealClosed,
        Line: line,
        Persona: "roma",
        LeadId: "L-1",
        TimestampUtc: new DateTimeOffset(2026, 5, 18, 12, 0, 0, TimeSpan.Zero),
        Tokens: new Dictionary<string, string>());

    [Fact]
    public async Task SpeakAsync_records_cue_when_unmuted()
    {
        var narrator = new StubArenaNarrator();
        await narrator.SpeakAsync(Cue("first"));
        await narrator.SpeakAsync(Cue("second"));

        narrator.Spoken.Select(c => c.Line).Should().BeEquivalentTo(new[] { "first", "second" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task Mute_drops_cues_until_unmute()
    {
        var narrator = new StubArenaNarrator();
        await narrator.SpeakAsync(Cue("before-mute"));

        narrator.Mute();
        narrator.IsMuted.Should().BeTrue();
        await narrator.SpeakAsync(Cue("while-muted"));

        narrator.Unmute();
        narrator.IsMuted.Should().BeFalse();
        await narrator.SpeakAsync(Cue("after-unmute"));

        narrator.Spoken.Select(c => c.Line).Should().BeEquivalentTo(new[] { "before-mute", "after-unmute" }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public async Task SpeakAsync_rejects_null_cue()
    {
        var narrator = new StubArenaNarrator();
        Func<Task> act = () => narrator.SpeakAsync(null!);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
