using System.Net;
using System.Text.RegularExpressions;

namespace SalesArena.Manager.Web.Services.Gallery;

/// <summary>
/// HTML-escapes operator-visible persona text before Blazor render (SA-07-03 security AC).
/// </summary>
public static partial class GalleryTextSanitizer
{
    public static string Escape(string? value) =>
        WebUtility.HtmlEncode(value ?? string.Empty);

    public static string ToPlainExcerpt(string? markdown, int maxChars = 160)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        var plain = MarkdownLinkRegex().Replace(markdown, "$1");
        plain = plain.Replace('#', ' ')
            .Replace('*', ' ')
            .Replace('_', ' ')
            .Replace('`', ' ');
        plain = WhitespaceRegex().Replace(plain, " ").Trim();
        if (plain.Length <= maxChars)
        {
            return plain;
        }

        return plain[..maxChars].TrimEnd() + "…";
    }

    public static string ToPromptPreview(string? markdown, int maxChars = 480)
    {
        var plain = ToPlainExcerpt(markdown, maxChars);
        return plain;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRegex();
}
