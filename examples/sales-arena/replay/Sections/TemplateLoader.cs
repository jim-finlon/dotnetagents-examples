namespace SalesArena.Replay.Sections;

/// <summary>
/// Loads the per-section header template from disk + applies the standard
/// token substitutions (<c>{{contest_id}}</c>, <c>{{contest_name}}</c>,
/// <c>{{generated_at_utc}}</c>). Falls back to a built-in default header
/// when the template file is missing — keeps the engine usable in test
/// environments without copy-to-output template dirs.
/// </summary>
public static class TemplateLoader
{
    private static readonly IReadOnlyDictionary<ReplaySectionKind, string> DefaultHeaders = new Dictionary<ReplaySectionKind, string>
    {
        [ReplaySectionKind.Leaderboard]      = "## 🏆 Final Leaderboard\n\nContest **{{contest_name}}** as of {{generated_at_utc}}.",
        [ReplaySectionKind.PersonaDealLog]   = "## 📜 Persona Deal Logs\n\nEvery deal each closer worked, win or lose.",
        [ReplaySectionKind.ClosestCall]      = "## 🔪 Closest Call\n\nThe biggest deal that almost slipped through the cracks.",
        [ReplaySectionKind.BestComeback]     = "## 🚀 Best Comeback\n\nFrom the bottom to the board — biggest position climb.",
        [ReplaySectionKind.MvpTouch]         = "## 🔔 MVP Touch\n\nThe single touch that flipped the most value.",
        [ReplaySectionKind.SteakKnivesShowcase] = "## 🔪 Steak Knives Showcase\n\nA toast to the runner-up — Cadillac was within reach.",
        [ReplaySectionKind.Roast]              = "## 🔥 The Roast\n\nThe winners get the mic. Citations from the contest log only — the hallucination guard is on.",
    };

    public static string LoadHeader(ReplaySectionKind kind, SectionContext ctx)
    {
        string raw = TryReadTemplate(kind, ctx.TemplateDir)
                    ?? DefaultHeaders[kind];
        return Substitute(raw, ctx);
    }

    private static string? TryReadTemplate(ReplaySectionKind kind, string? templateDir)
    {
        if (templateDir is null) return null;
        var path = Path.Combine(templateDir, $"{kind.ToString().ToLowerInvariant()}.md");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string Substitute(string template, SectionContext ctx) =>
        template
            .Replace("{{contest_id}}", ctx.ContestId, StringComparison.Ordinal)
            .Replace("{{contest_name}}", ctx.ContestDisplayName, StringComparison.Ordinal)
            .Replace("{{generated_at_utc}}", ctx.GeneratedAtUtc.ToString("u"), StringComparison.Ordinal);
}
