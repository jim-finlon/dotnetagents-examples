namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// Coach Mode tunables. Story SA-08-12 acceptance defaults: overlays
/// expire after 10 touches. Sanitization caps are operator-friendly
/// but generous enough for real halftime speeches.
/// </summary>
public sealed record CoachOptions
{
    /// <summary>Default lifespan of an overlay if the caller doesn't supply one.</summary>
    public int DefaultExpiresAfterTouches { get; init; } = 10;

    /// <summary>Maximum allowed length of the sanitized speech (chars).</summary>
    public int MaxSpeechLengthChars { get; init; } = 1_500;

    /// <summary>
    /// Substrings that signal a prompt-injection attempt. Matched
    /// case-insensitively against the sanitized speech; first hit is
    /// rejected. Operators can extend this list per-deployment.
    /// </summary>
    public IReadOnlyList<string> ForbiddenInjectionMarkers { get; init; } = new[]
    {
        "</system>",
        "<|system|>",
        "ignore previous instructions",
        "you are now",
        "disregard the above",
    };
}
