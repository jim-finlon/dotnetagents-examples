namespace SalesArena.Replay.Podcast;

/// <summary>
/// Renders a <see cref="SalesArena.Replay.ReplayReport"/> into a multi-voice
/// audio podcast. Production wiring plugs a real <see cref="ITtsAdapter"/>
/// + <see cref="IAudioEncoder"/> for mp3; tests + public-core hosts use the
/// stub + null encoder and ship wav only.
/// </summary>
public interface IPodcastRenderer
{
    Task<PodcastAudio> RenderAsync(
        SalesArena.Replay.ReplayReport report,
        VoicePack voicePack,
        CancellationToken cancellationToken = default);
}
