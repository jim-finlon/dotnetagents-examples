namespace SalesArena.Manager.Web.Services.LeadPool;

public interface ILeadPoolSnapshotProvider
{
    Task<IReadOnlyList<LeadPoolLead>> GetLeadsAsync(CancellationToken cancellationToken = default);
}
