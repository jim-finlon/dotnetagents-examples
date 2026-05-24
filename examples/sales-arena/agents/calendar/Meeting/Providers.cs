using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Meeting;

public interface ICompanyNewsProvider
{
    Task<IReadOnlyList<CompanyNews>> GetRecentAsync(ProspectId prospectId, CancellationToken ct);
}

public interface IMutualConnectionProvider
{
    Task<IReadOnlyList<MutualConnection>> GetForAsync(ProspectId prospectId, Persona persona, CancellationToken ct);
}

public interface ICrmHistoryProvider
{
    Task<IReadOnlyList<CrmHistoryEntry>> GetHistoryAsync(ProspectId prospectId, CancellationToken ct);
}

public interface IKnowledgeTalkingPointProvider
{
    Task<IReadOnlyList<TalkingPoint>> GetTalkingPointsAsync(ProspectId prospectId, Persona persona, CancellationToken ct);
}

public interface ICrmEventPublisher
{
    Task PublishStageChangeAsync(CrmStageChangeEvent stageChange, CancellationToken ct);
}

internal static class EmptyProviders
{
    public static readonly IReadOnlyList<CompanyNews> News = System.Array.Empty<CompanyNews>();
    public static readonly IReadOnlyList<MutualConnection> Connections = System.Array.Empty<MutualConnection>();
    public static readonly IReadOnlyList<CrmHistoryEntry> History = System.Array.Empty<CrmHistoryEntry>();
    public static readonly IReadOnlyList<TalkingPoint> TalkingPoints = System.Array.Empty<TalkingPoint>();
}

public sealed class EmptyCompanyNewsProvider : ICompanyNewsProvider
{
    public Task<IReadOnlyList<CompanyNews>> GetRecentAsync(ProspectId prospectId, CancellationToken ct)
        => Task.FromResult(EmptyProviders.News);
}

public sealed class EmptyMutualConnectionProvider : IMutualConnectionProvider
{
    public Task<IReadOnlyList<MutualConnection>> GetForAsync(ProspectId prospectId, Persona persona, CancellationToken ct)
        => Task.FromResult(EmptyProviders.Connections);
}

public sealed class EmptyCrmHistoryProvider : ICrmHistoryProvider
{
    public Task<IReadOnlyList<CrmHistoryEntry>> GetHistoryAsync(ProspectId prospectId, CancellationToken ct)
        => Task.FromResult(EmptyProviders.History);
}

public sealed class EmptyKnowledgeTalkingPointProvider : IKnowledgeTalkingPointProvider
{
    public Task<IReadOnlyList<TalkingPoint>> GetTalkingPointsAsync(ProspectId prospectId, Persona persona, CancellationToken ct)
        => Task.FromResult(EmptyProviders.TalkingPoints);
}

public sealed class RecordingCrmEventPublisher : ICrmEventPublisher
{
    private readonly List<CrmStageChangeEvent> _events = new();

    public IReadOnlyList<CrmStageChangeEvent> Events => _events;

    public Task PublishStageChangeAsync(CrmStageChangeEvent stageChange, CancellationToken ct)
    {
        _events.Add(stageChange);
        return Task.CompletedTask;
    }
}
