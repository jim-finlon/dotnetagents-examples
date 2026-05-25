namespace SalesArena.Orchestrator.Narration;

/// <summary>
/// Voice configuration the runtime TTS adapter consumes. Decoupled from any
/// specific provider — `VoiceId` is the adapter's identifier (Piper voice
/// name, ElevenLabs voice id, Azure neural voice, etc.). Pitch/rate are
/// hints; adapters that don't support them ignore the values.
/// </summary>
public sealed record PersonaVoiceProfile(
    string Persona,
    string VoiceId,
    double PitchShiftSemitones,
    double SpeakingRate)
{
    public static PersonaVoiceProfile Default(string persona) =>
        new(persona, VoiceId: "narrator-default", PitchShiftSemitones: 0.0, SpeakingRate: 1.0);
}
