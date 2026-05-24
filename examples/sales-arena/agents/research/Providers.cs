using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Research;

public interface IPublicFeedAdapter
{
    Task<IReadOnlyList<PublicFeedItem>> FetchAsync(string prospectId, IReadOnlyList<string> allowedHosts, CancellationToken ct);
}

public interface ICompanyFactProvider
{
    Task<IReadOnlyList<CompanyFact>> GetFactsAsync(string prospectId, CancellationToken ct);
}

public interface IKnownContactProvider
{
    Task<IReadOnlyList<KnownContact>> GetContactsAsync(string prospectId, CancellationToken ct);
}

public sealed class InMemoryPublicFeedAdapter : IPublicFeedAdapter
{
    private readonly IReadOnlyList<PublicFeedItem> _items;

    public InMemoryPublicFeedAdapter(IReadOnlyList<PublicFeedItem> items)
    {
        _items = items;
    }

    public Task<IReadOnlyList<PublicFeedItem>> FetchAsync(string prospectId, IReadOnlyList<string> allowedHosts, CancellationToken ct)
    {
        var allowed = new HashSet<string>(allowedHosts, System.StringComparer.OrdinalIgnoreCase);
        var filtered = new List<PublicFeedItem>(_items.Count);
        foreach (var item in _items)
        {
            if (!System.Uri.TryCreate(item.Url, System.UriKind.Absolute, out var uri))
                continue;
            if (allowed.Count > 0 && !allowed.Contains(uri.Host))
                continue;
            filtered.Add(item);
        }
        return Task.FromResult<IReadOnlyList<PublicFeedItem>>(filtered);
    }
}

public sealed class InMemoryCompanyFactProvider : ICompanyFactProvider
{
    private readonly IReadOnlyList<CompanyFact> _facts;
    public InMemoryCompanyFactProvider(IReadOnlyList<CompanyFact> facts) => _facts = facts;
    public Task<IReadOnlyList<CompanyFact>> GetFactsAsync(string prospectId, CancellationToken ct) => Task.FromResult(_facts);
}

public sealed class InMemoryKnownContactProvider : IKnownContactProvider
{
    private readonly IReadOnlyList<KnownContact> _contacts;
    public InMemoryKnownContactProvider(IReadOnlyList<KnownContact> contacts) => _contacts = contacts;
    public Task<IReadOnlyList<KnownContact>> GetContactsAsync(string prospectId, CancellationToken ct) => Task.FromResult(_contacts);
}
