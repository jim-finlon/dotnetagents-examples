namespace SalesArena.Manager.Web.Services.BossOffice;

public interface IBossOfficeMetricsProvider
{
    BossOfficeMetricsSnapshot GetSnapshot();
}
