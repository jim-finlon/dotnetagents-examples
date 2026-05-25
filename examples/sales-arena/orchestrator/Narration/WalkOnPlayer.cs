namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// What the player decided to do for a given walk-on request. Lets the
/// caller (a Blazor host or replay-side audio renderer) decide whether to
/// trigger HTML5 audio playback, queue, or skip.
/// </summary>
public enum WalkOnDecision
{
    Played = 1,
    Muted = 2,
    DeferredForBell = 3,
    NoWalkOnForPersona = 4,
}

/// <summary>
/// Per-persona walk-on dispatcher for SA-08-10. Pure decision logic —
/// the host wires actual audio playback against the returned <see cref="WalkOnRequest"/>.
/// Coordinates with the SA-02-06 bell so the bell + walk-on don't overlap.
/// </summary>
public interface IWalkOnPlayer
{
    bool IsMuted { get; }
    void Mute();
    void Unmute();

    /// <summary>
    /// Tell the player that the bell is currently ringing. Walk-ons
    /// requested while a bell is in flight are decided as
    /// <see cref="WalkOnDecision.DeferredForBell"/>.
    /// </summary>
    void NotifyBellStart();

    /// <summary>Tell the player the bell has finished and walk-ons may resume.</summary>
    void NotifyBellEnd();

    /// <summary>Configure (or override) which on-disk file a persona's walk-on plays.</summary>
    void RegisterWalkOn(string persona, string filePath);

    /// <returns>The play decision + the resolved walk-on request the host should act on.</returns>
    (WalkOnDecision Decision, WalkOnRequest? Request) Play(string persona, DateTimeOffset now);
}

public sealed record WalkOnRequest(string Persona, string FilePath, DateTimeOffset RequestedAtUtc);
