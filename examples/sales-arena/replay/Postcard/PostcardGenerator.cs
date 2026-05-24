using System.Globalization;
using System.Security;
using System.Text;
using SalesArena.Orchestrator.Leaderboard;

namespace SalesArena.Replay.Postcard;

/// <summary>
/// Default <see cref="IPostcardGenerator"/>. Renders a self-contained SVG
/// document with inline CSS-in-SVG (no external font loads). Styles are
/// pure-XML-safe; every interpolated string is HTML-escaped on the way in.
/// </summary>
public sealed class PostcardGenerator : IPostcardGenerator
{
    public string Generate(SalesArena.Orchestrator.Leaderboard.Leaderboard leaderboard, PostcardOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(leaderboard);
        options ??= new PostcardOptions();

        var winner = leaderboard.Entries.FirstOrDefault(e => e.Tier == LeaderboardTier.Cadillac)
                     ?? leaderboard.Entries.FirstOrDefault();
        if (winner is null)
        {
            return EmptyPostcard(options);
        }

        var palette = ResolvePalette(options.Style);
        var sb = new StringBuilder();

        sb.AppendLine($"<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {0} {1}\" width=\"{0}\" height=\"{1}\" role=\"img\" aria-label=\"Sales Arena postcard\">",
            options.Width, options.Height));

        // Inline CSS-in-SVG keeps fonts portable.
        sb.AppendLine($"  <style><![CDATA[");
        sb.AppendLine($"    .bg {{ fill: {palette.Background}; }}");
        sb.AppendLine($"    .border {{ fill: none; stroke: {palette.Border}; stroke-width: 4; }}");
        sb.AppendLine($"    .title {{ font-family: {palette.HeadlineFont}; font-size: 48px; fill: {palette.Headline}; font-weight: 700; }}");
        sb.AppendLine($"    .winner {{ font-family: {palette.HeadlineFont}; font-size: 72px; fill: {palette.Accent}; font-weight: 700; }}");
        sb.AppendLine($"    .stat {{ font-family: {palette.BodyFont}; font-size: 26px; fill: {palette.Body}; }}");
        sb.AppendLine($"    .stat-num {{ font-family: {palette.BodyFont}; font-size: 36px; fill: {palette.Accent}; font-weight: 700; }}");
        sb.AppendLine($"    .footer {{ font-family: {palette.BodyFont}; font-size: 18px; fill: {palette.Body}; font-style: italic; }}");
        sb.AppendLine($"    .catchphrase {{ font-family: {palette.BodyFont}; font-size: 22px; fill: {palette.Body}; font-style: italic; }}");
        sb.AppendLine($"  ]]></style>");

        // Background + border
        sb.AppendLine($"  <rect class=\"bg\" width=\"{options.Width}\" height=\"{options.Height}\"/>");
        sb.AppendLine($"  <rect class=\"border\" x=\"20\" y=\"20\" width=\"{options.Width - 40}\" height=\"{options.Height - 40}\" rx=\"8\"/>");

        // Title
        var contestName = Escape(options.ContestDisplayName ?? leaderboard.ContestId);
        sb.AppendLine($"  <text class=\"title\" x=\"50\" y=\"100\">🏆 SALES ARENA</text>");
        sb.AppendLine($"  <text class=\"stat\" x=\"50\" y=\"140\">Contest: {contestName}</text>");

        // Winner — upper-case FIRST so we don't mangle the escape entity names ('&lt;' → '&LT;').
        sb.AppendLine($"  <text class=\"winner\" x=\"50\" y=\"230\">{Escape(winner.Persona.ToUpperInvariant())}</text>");
        sb.AppendLine($"  <text class=\"stat\" x=\"50\" y=\"270\">Cadillac Tier — Coffee's for closers.</text>");

        // Stats row
        var amount = string.Format(CultureInfo.InvariantCulture, "${0:N0}", winner.RevenueUsd);
        // Hand-format the percent so we don't pick up the InvariantCulture space ("80 %").
        var winRate = string.Format(CultureInfo.InvariantCulture, "{0:0}%", winner.WinRate * 100);
        sb.AppendLine($"  <text class=\"stat-num\" x=\"50\" y=\"360\">{amount}</text>");
        sb.AppendLine($"  <text class=\"stat\" x=\"50\" y=\"385\">revenue</text>");
        sb.AppendLine($"  <text class=\"stat-num\" x=\"280\" y=\"360\">{winner.DealsWon}</text>");
        sb.AppendLine($"  <text class=\"stat\" x=\"280\" y=\"385\">closed</text>");
        sb.AppendLine($"  <text class=\"stat-num\" x=\"430\" y=\"360\">{winRate}</text>");
        sb.AppendLine($"  <text class=\"stat\" x=\"430\" y=\"385\">win rate</text>");

        // Catchphrase
        if (!string.IsNullOrWhiteSpace(options.Catchphrase))
        {
            sb.AppendLine($"  <text class=\"catchphrase\" x=\"50\" y=\"440\">“{Escape(options.Catchphrase)}”</text>");
        }

        // Footer with timestamp
        var stamp = leaderboard.AsOfUtc.ToString("u", CultureInfo.InvariantCulture);
        sb.AppendLine($"  <text class=\"footer\" x=\"50\" y=\"{options.Height - 40}\">DNA Sales Arena · {stamp}</text>");

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static string EmptyPostcard(PostcardOptions options)
    {
        var palette = ResolvePalette(options.Style);
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {options.Width} {options.Height}\" width=\"{options.Width}\" height=\"{options.Height}\">\n" +
               $"  <rect width=\"{options.Width}\" height=\"{options.Height}\" fill=\"{palette.Background}\"/>\n" +
               $"  <text x=\"50\" y=\"50\" font-family=\"{palette.BodyFont}\" font-size=\"24\" fill=\"{palette.Body}\">No contest results yet.</text>\n" +
               $"</svg>\n";
    }

    private static Palette ResolvePalette(PostcardStyle style) => style switch
    {
        PostcardStyle.Modern => new Palette(
            Background: "#FAFAF7",
            Border: "#2D3B45",
            Headline: "#1B2A33",
            Accent: "#1A7F5A",
            Body: "#3D4D55",
            HeadlineFont: "Helvetica, Arial, sans-serif",
            BodyFont: "Helvetica, Arial, sans-serif"),

        PostcardStyle.Neon => new Palette(
            Background: "#0A0414",
            Border: "#FF2EBC",
            Headline: "#42F0FF",
            Accent: "#FF2EBC",
            Body: "#D6CFEA",
            HeadlineFont: "Impact, sans-serif",
            BodyFont: "Courier New, monospace"),

        _ => new Palette(  // Vintage default
            Background: "#F4ECD8",
            Border: "#5C4033",
            Headline: "#3E2C1C",
            Accent: "#A0522D",
            Body: "#5C4033",
            HeadlineFont: "Georgia, Times New Roman, serif",
            BodyFont: "Georgia, Times New Roman, serif"),
    };

    private static string Escape(string raw) => SecurityElement.Escape(raw) ?? string.Empty;

    private sealed record Palette(
        string Background,
        string Border,
        string Headline,
        string Accent,
        string Body,
        string HeadlineFont,
        string BodyFont);
}
