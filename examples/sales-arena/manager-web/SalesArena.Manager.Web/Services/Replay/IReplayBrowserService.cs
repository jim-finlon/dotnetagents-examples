using SalesArena.Replay;

namespace SalesArena.Manager.Web.Services.Replay;

public interface IReplayBrowserService
{
    Task<IReadOnlyList<ReplayContestSummary>> ListContestsAsync(CancellationToken cancellationToken = default);

    Task<ReplayReport> GetReportAsync(string contestId, CancellationToken cancellationToken = default);

    Task<ReplayDealFocus?> GetDealFocusAsync(
        string contestId,
        string leadId,
        ReplayReport? report = null,
        CancellationToken cancellationToken = default);
}
