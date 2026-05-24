namespace SalesArena.Crm.Scoring;

/// <summary>
/// Deterministic sub-scores for reproducible leaderboard tests (SA-01-03).
/// </summary>
public sealed class StubLeadScoreModel : ILeadScoreModel
{
    public Task<(LeadSubScores SubScores, IReadOnlyList<string> Rationale)> ScoreAsync(
        CrmRecord lead,
        IcpProfile icp,
        string rubricPrompt,
        CancellationToken cancellationToken = default)
    {
        _ = rubricPrompt;
        _ = cancellationToken;

        var industry = lead.Metadata.GetValueOrDefault("industry") ?? string.Empty;
        var fit = icp.TargetIndustries.Any(t =>
            industry.Contains(t, StringComparison.OrdinalIgnoreCase))
            ? 82
            : 48;

        var intentSignal = lead.Metadata.GetValueOrDefault("intent_signal") ?? "low";
        var intent = intentSignal switch
        {
            "high" => 88,
            "medium" => 62,
            _ => 35,
        };

        var role = lead.Metadata.GetValueOrDefault("contact_role") ?? string.Empty;
        var power = role.Contains("vp", StringComparison.OrdinalIgnoreCase)
            || role.Contains("director", StringComparison.OrdinalIgnoreCase)
            ? 80
            : 45;

        var rationale = new[]
        {
            $"Fit {fit}: industry '{industry}' vs ICP '{icp.Name}'.",
            $"Intent {intent}: signal '{intentSignal}'.",
            $"Power {power}: role '{role}'.",
        };

        IReadOnlyList<string> lines = rationale;
        return Task.FromResult((new LeadSubScores(fit, intent, power), lines));
    }
}
