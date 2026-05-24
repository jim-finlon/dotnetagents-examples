using System.Text.RegularExpressions;

namespace SalesArena.Manager.Web.Services.Bullpen;

/// <summary>
/// Triple-layer redaction for public bullpen thought bubbles (SA-08-20).
/// </summary>
public static partial class BullpenThoughtSanitizer
{
    public static string BucketDealValue(decimal? valueUsd)
    {
        if (valueUsd is null or <= 0)
        {
            return "an undisclosed size";
        }

        var v = valueUsd.Value;
        return v switch
        {
            < 10_000m => "under $10K",
            < 50_000m => "$10K–$50K",
            < 100_000m => "$50K–$100K",
            _ => "$100K+",
        };
    }

    public static string SanitizeThought(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw;
        text = LeadIdRegex().Replace(text, "a lead");
        text = DollarRegex().Replace(text, _ => "[deal-size]");
        text = ProspectNameRegex().Replace(text, "a prospect");
        text = EmailRegex().Replace(text, "[contact]");
        text = WhitespaceRegex().Replace(text, " ").Trim();
        return text;
    }

    /// <summary>
    /// Redacts PII patterns but preserves deal-size bucket tokens (e.g. $10K–$50K).
    /// </summary>
    public static string SanitizePublicThought(string? raw, int maxChars = 140)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw;
        text = LeadIdRegex().Replace(text, "a lead");
        text = ProspectNameRegex().Replace(text, "a prospect");
        text = EmailRegex().Replace(text, "[contact]");
        text = WhitespaceRegex().Replace(text, " ").Trim();
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars].TrimEnd() + "…";
    }

    public static string SanitizeAndCap(string? raw, int maxChars = 140)
    {
        var sanitized = SanitizeThought(raw);
        if (sanitized.Length <= maxChars)
        {
            return sanitized;
        }

        return sanitized[..maxChars].TrimEnd() + "…";
    }

    [GeneratedRegex(@"\bL-\d+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex LeadIdRegex();

    [GeneratedRegex(@"\$[\d,]+(?:\.\d{1,2})?(?:\s*(?:K|k|M|m))?", RegexOptions.Compiled)]
    private static partial Regex DollarRegex();

    [GeneratedRegex(@"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+\b", RegexOptions.Compiled)]
    private static partial Regex ProspectNameRegex();

    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
