namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// Composes the base persona prompt with any active overlay's sanitized
/// speech. Story SA-08-12 constraint: "Persona system prompt is appended,
/// not replaced." The overlay shows up as a clearly-marked addendum block
/// so the persona (and audit reviewers) can see exactly what was added.
/// </summary>
public static class PromptOverlayApplier
{
    public const string AddendumHeader = "\n\n# Operator Halftime Speech";
    public const string AddendumFooter = "\n# (overlay expires after the touch counter reaches zero)\n";

    /// <summary>
    /// Returns the per-touch system prompt: <paramref name="basePrompt"/>
    /// unchanged when no active overlay; otherwise the base + a clearly-marked
    /// addendum block carrying the sanitized speech.
    /// </summary>
    public static string Compose(string basePrompt, PromptOverlay? overlay)
    {
        ArgumentNullException.ThrowIfNull(basePrompt);
        if (overlay is null || !overlay.IsActive)
        {
            return basePrompt;
        }
        return basePrompt + AddendumHeader + "\n\n" + overlay.SanitizedSpeech + AddendumFooter;
    }
}
