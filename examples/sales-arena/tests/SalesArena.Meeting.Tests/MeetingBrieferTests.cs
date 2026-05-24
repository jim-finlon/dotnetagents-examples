using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace SalesArena.Meeting.Tests;

public class MeetingBrieferTests
{
    private static readonly ProspectId Prospect = new("greybridge");
    private static readonly Persona Persona = new("moss", "Dave Moss");

    private static MeetingBriefer Build(
        ICompanyNewsProvider? news = null,
        IMutualConnectionProvider? connections = null,
        ICrmHistoryProvider? history = null,
        IKnowledgeTalkingPointProvider? talkingPoints = null)
        => new(
            news ?? new EmptyCompanyNewsProvider(),
            connections ?? new EmptyMutualConnectionProvider(),
            history ?? new EmptyCrmHistoryProvider(),
            talkingPoints ?? new EmptyKnowledgeTalkingPointProvider());

    [Fact]
    public async Task AssembleBrief_HappyPath_AggregatesProviders()
    {
        var briefer = Build(
            news: new StaticNews(new CompanyNews("Greybridge raises Series B", DateTimeOffset.UtcNow, "Stratham Wire")),
            connections: new StaticConnections(new MutualConnection("Sheila Kenmore", "VP Ops", "linkedin")),
            history: new StaticHistory(new CrmHistoryEntry("Discovery", DateTimeOffset.UtcNow.AddDays(-7), "intro call")),
            talkingPoints: new StaticTalkingPoints(new TalkingPoint("budget", "Q3 budget window confirmed")));

        var brief = await briefer.AssembleBriefAsync(Prospect, Persona);

        brief.ProspectId.Should().Be(Prospect);
        brief.Persona.Should().Be(Persona);
        brief.CompanyNews.Should().ContainSingle().Which.Headline.Should().Contain("Series B");
        brief.MutualConnections.Should().ContainSingle().Which.Name.Should().Be("Sheila Kenmore");
        brief.CrmHistory.Should().ContainSingle().Which.Stage.Should().Be("Discovery");
        brief.TalkingPoints.Should().ContainSingle().Which.Topic.Should().Be("budget");
    }

    [Fact]
    public async Task AssembleBrief_EmptyProviders_ReturnsEmptyCollections()
    {
        var briefer = Build();

        var brief = await briefer.AssembleBriefAsync(Prospect, Persona);

        brief.CompanyNews.Should().BeEmpty();
        brief.MutualConnections.Should().BeEmpty();
        brief.CrmHistory.Should().BeEmpty();
        brief.TalkingPoints.Should().BeEmpty();
    }

    [Fact]
    public async Task AssembleBrief_NullProspectId_Throws()
    {
        var briefer = Build();

        Func<Task> act = () => briefer.AssembleBriefAsync(null!, Persona);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private sealed class StaticNews : ICompanyNewsProvider
    {
        private readonly IReadOnlyList<CompanyNews> _items;
        public StaticNews(params CompanyNews[] items) => _items = items;
        public Task<IReadOnlyList<CompanyNews>> GetRecentAsync(ProspectId prospectId, CancellationToken ct)
            => Task.FromResult(_items);
    }

    private sealed class StaticConnections : IMutualConnectionProvider
    {
        private readonly IReadOnlyList<MutualConnection> _items;
        public StaticConnections(params MutualConnection[] items) => _items = items;
        public Task<IReadOnlyList<MutualConnection>> GetForAsync(ProspectId prospectId, Persona persona, CancellationToken ct)
            => Task.FromResult(_items);
    }

    private sealed class StaticHistory : ICrmHistoryProvider
    {
        private readonly IReadOnlyList<CrmHistoryEntry> _items;
        public StaticHistory(params CrmHistoryEntry[] items) => _items = items;
        public Task<IReadOnlyList<CrmHistoryEntry>> GetHistoryAsync(ProspectId prospectId, CancellationToken ct)
            => Task.FromResult(_items);
    }

    private sealed class StaticTalkingPoints : IKnowledgeTalkingPointProvider
    {
        private readonly IReadOnlyList<TalkingPoint> _items;
        public StaticTalkingPoints(params TalkingPoint[] items) => _items = items;
        public Task<IReadOnlyList<TalkingPoint>> GetTalkingPointsAsync(ProspectId prospectId, Persona persona, CancellationToken ct)
            => Task.FromResult(_items);
    }
}
