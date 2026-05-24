namespace SalesArena.Crm.Scoring;

/// <summary>
/// Pluggable model surface (stub in tests; local LLM gateway in production hosts).
/// </summary>
public interface ILeadScoreModel
{
    Task<(LeadSubScores SubScores, IReadOnlyList<string> Rationale)> ScoreAsync(
        CrmRecord lead,
        IcpProfile icp,
        string rubricPrompt,
        CancellationToken cancellationToken = default);
}
