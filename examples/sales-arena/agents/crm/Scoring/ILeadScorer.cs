namespace SalesArena.Crm.Scoring;

public interface ILeadScorer
{
    Task<LeadScore> ScoreAsync(
        CrmRecord lead,
        IcpProfile icp,
        string personaId,
        CancellationToken cancellationToken = default);
}
