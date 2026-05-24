namespace SalesArena.Replay.Podcast;

/// <summary>
/// Canonical podcast section discriminators. SA-08-14 acceptance pins the
/// 5-section structure: cold open, leaderboard recap, deal of the contest,
/// comeback, sign-off.
/// </summary>
public enum PodcastSectionKind
{
    ColdOpen = 1,
    LeaderboardRecap = 2,
    DealOfTheContest = 3,
    ComebackStory = 4,
    SignOff = 5,
}

/// <summary>
/// One scripted section before TTS rendering. Each <see cref="PodcastLine"/>
/// carries the voice id resolved from the <see cref="VoicePack"/>; the host
/// covers narration outside persona quotes.
/// </summary>
public sealed record PodcastScriptSection(
    PodcastSectionKind Kind,
    string Title,
    IReadOnlyList<PodcastLine> Lines);

public sealed record PodcastLine(
    string Text,
    string VoiceId,
    string? AttributedPersona);

/// <summary>
/// Resolved audio output. Wav is always present; Mp3 is present only when
/// an <see cref="IAudioEncoder"/> registered an mp3 path. Duration is the
/// sum of every synthesized clip + inter-section bed.
/// </summary>
public sealed record PodcastAudio(
    string ContestId,
    byte[] WavBytes,
    byte[]? Mp3Bytes,
    TimeSpan Duration,
    IReadOnlyList<PodcastSectionMarker> SectionMarkers);

public sealed record PodcastSectionMarker(
    PodcastSectionKind Kind,
    TimeSpan StartAt,
    TimeSpan Duration);
