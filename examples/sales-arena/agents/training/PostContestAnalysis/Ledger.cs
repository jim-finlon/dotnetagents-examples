using System.Collections.Generic;

namespace SalesArena.Training.PostContestAnalysis;

public interface IContestLedgerReader
{
    IReadOnlyList<ContestEventEntry> ReadEvents(string contestId);
}

public sealed class InMemoryContestLedger : IContestLedgerReader
{
    private readonly Dictionary<string, List<ContestEventEntry>> _byContest;

    public InMemoryContestLedger(IEnumerable<ContestEventEntry> events)
    {
        _byContest = new Dictionary<string, List<ContestEventEntry>>(System.StringComparer.Ordinal);
        foreach (var e in events)
        {
            if (!_byContest.TryGetValue(e.ContestId, out var list))
            {
                list = new List<ContestEventEntry>();
                _byContest[e.ContestId] = list;
            }
            list.Add(e);
        }
    }

    public IReadOnlyList<ContestEventEntry> ReadEvents(string contestId)
    {
        if (_byContest.TryGetValue(contestId, out var list)) return list;
        return System.Array.Empty<ContestEventEntry>();
    }
}
