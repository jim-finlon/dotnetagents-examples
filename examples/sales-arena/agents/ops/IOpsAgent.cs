namespace SalesArena.Ops;

public interface IOpsAgent
{
    ContestPlan SetupContest(ContestRequest request);

    DailyQueue BuildDailyQueue(BuildQueueRequest request);
}
