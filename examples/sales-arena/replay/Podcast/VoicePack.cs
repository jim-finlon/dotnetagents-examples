namespace SalesArena.Replay.Podcast;

/// <summary>
/// Maps personas to TTS voice ids plus a single "host" voice for narration.
/// Voice ids are provider-agnostic; the registered <see cref="ITtsAdapter"/>
/// interprets them.
/// </summary>
public sealed record VoicePack
{
    public required string HostVoiceId { get; init; }

    public required IReadOnlyDictionary<string, string> PersonaVoices { get; init; }

    /// <summary>Voice id used when a persona is mentioned but lacks a mapping.</summary>
    public string FallbackVoiceId { get; init; } = "neutral";

    public string ResolveForPersona(string? persona)
    {
        if (string.IsNullOrEmpty(persona)) return HostVoiceId;
        if (PersonaVoices.TryGetValue(persona, out var v)) return v;
        return FallbackVoiceId;
    }
}
