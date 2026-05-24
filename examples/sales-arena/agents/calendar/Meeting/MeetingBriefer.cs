using System;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Meeting;

public interface IMeetingBriefer
{
    Task<Brief> AssembleBriefAsync(ProspectId prospectId, Persona persona, CancellationToken ct = default);
}

public sealed class MeetingBriefer : IMeetingBriefer
{
    private readonly ICompanyNewsProvider _news;
    private readonly IMutualConnectionProvider _connections;
    private readonly ICrmHistoryProvider _history;
    private readonly IKnowledgeTalkingPointProvider _talkingPoints;

    public MeetingBriefer(
        ICompanyNewsProvider news,
        IMutualConnectionProvider connections,
        ICrmHistoryProvider history,
        IKnowledgeTalkingPointProvider talkingPoints)
    {
        _news = news ?? throw new ArgumentNullException(nameof(news));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _talkingPoints = talkingPoints ?? throw new ArgumentNullException(nameof(talkingPoints));
    }

    public async Task<Brief> AssembleBriefAsync(ProspectId prospectId, Persona persona, CancellationToken ct = default)
    {
        if (prospectId is null) throw new ArgumentNullException(nameof(prospectId));
        if (persona is null) throw new ArgumentNullException(nameof(persona));

        var newsTask = _news.GetRecentAsync(prospectId, ct);
        var connectionsTask = _connections.GetForAsync(prospectId, persona, ct);
        var historyTask = _history.GetHistoryAsync(prospectId, ct);
        var talkingPointsTask = _talkingPoints.GetTalkingPointsAsync(prospectId, persona, ct);

        await Task.WhenAll(newsTask, connectionsTask, historyTask, talkingPointsTask).ConfigureAwait(false);

        return new Brief(
            ProspectId: prospectId,
            Persona: persona,
            CompanyNews: newsTask.Result,
            MutualConnections: connectionsTask.Result,
            CrmHistory: historyTask.Result,
            TalkingPoints: talkingPointsTask.Result);
    }
}
