using System.Text;

namespace SalesArena.Orchestrator.Coach;

/// <summary>
/// Sanitize an operator's halftime speech before it reaches the ledger or
/// the persona's prompt. Refuses empty/oversized text and obvious
/// prompt-injection markers; collapses control characters; trims.
/// Story SA-08-12 acceptance: "Overlay text is sanitized before persistence."
/// </summary>
public static class CoachSpeechSanitizer
{
    public static string Sanitize(string speech, CoachOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (speech is null || string.IsNullOrWhiteSpace(speech))
        {
            throw new CoachException(CoachErrorCode.SpeechEmpty,
                "coach speech must not be empty or whitespace-only");
        }

        // Strip control characters (keeping standard whitespace) so a copy-pasted
        // null/escape sequence can't slip through.
        var builder = new StringBuilder(speech.Length);
        foreach (var c in speech)
        {
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
            {
                throw new CoachException(CoachErrorCode.SpeechContainsControlCharacters,
                    "coach speech contains a forbidden control character");
            }
            builder.Append(c);
        }
        var collapsed = builder.ToString().Trim();
        if (collapsed.Length == 0)
        {
            throw new CoachException(CoachErrorCode.SpeechEmpty,
                "coach speech is empty after trim");
        }
        if (collapsed.Length > options.MaxSpeechLengthChars)
        {
            throw new CoachException(CoachErrorCode.SpeechTooLong,
                $"coach speech is {collapsed.Length} chars; cap is {options.MaxSpeechLengthChars}");
        }
        foreach (var marker in options.ForbiddenInjectionMarkers)
        {
            if (collapsed.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                throw new CoachException(CoachErrorCode.SpeechContainsPromptInjectionMarker,
                    $"coach speech contains a forbidden prompt-injection marker (\"{marker}\")");
            }
        }
        return collapsed;
    }
}
