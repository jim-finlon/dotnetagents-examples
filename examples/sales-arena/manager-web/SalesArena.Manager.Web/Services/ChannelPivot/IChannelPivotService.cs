namespace SalesArena.Manager.Web.Services.ChannelPivot;

public interface IChannelPivotService
{
    Task<ChannelPivotSnapshot> BuildSnapshotAsync(
        string? contestId = null,
        CancellationToken cancellationToken = default);

    string ExportCsv(ChannelPivotSnapshot snapshot, ChannelPivotMetricMode metricMode);
}
