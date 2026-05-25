using System.Collections.Concurrent;

namespace SalesArena.Training.Diary;

/// <summary>Process-scoped store; the test default + lightweight demo path.</summary>
public sealed class InMemoryDiaryStore : IDiaryStore
{
    private readonly ConcurrentDictionary<string, List<DiaryEntry>> _entries = new(StringComparer.Ordinal);

    public Task<string> SaveAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var key = $"{entry.ContestId}/{entry.Persona}";
        var list = _entries.GetOrAdd(key, _ => new List<DiaryEntry>());
        lock (list)
        {
            list.RemoveAll(e => e.Day == entry.Day);
            list.Add(entry);
            list.Sort((a, b) => a.Day.CompareTo(b.Day));
        }
        var path = $"diary/{entry.ContestId}/{entry.Persona}/{entry.Day:D3}.md";
        return Task.FromResult(path);
    }

    public Task<IReadOnlyList<DiaryEntry>> LoadAsync(string contestId, string persona, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(persona);
        var key = $"{contestId}/{persona}";
        if (!_entries.TryGetValue(key, out var list))
        {
            return Task.FromResult<IReadOnlyList<DiaryEntry>>(Array.Empty<DiaryEntry>());
        }
        lock (list)
        {
            return Task.FromResult<IReadOnlyList<DiaryEntry>>(list.ToList());
        }
    }
}
