namespace SalesArena.Training.Diary;

/// <summary>
/// Saves entries as Markdown under
/// <c>&lt;rootDir&gt;/&lt;contestId&gt;/&lt;persona&gt;/&lt;day:D3&gt;.md</c>. Atomic
/// write via temp-then-rename so a crashed run never leaves a half-written
/// entry on disk.
/// </summary>
public sealed class FileSystemDiaryStore : IDiaryStore
{
    private readonly string _rootDir;
    private readonly SemaphoreSlim _ioLock = new(1, 1);

    public FileSystemDiaryStore(string rootDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDir);
        _rootDir = rootDir;
    }

    public async Task<string> SaveAsync(DiaryEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var dir = Path.Combine(_rootDir, SafeSegment(entry.ContestId), SafeSegment(entry.Persona));
        Directory.CreateDirectory(dir);

        var fileName = $"{entry.Day:D3}.md";
        var path = Path.Combine(dir, fileName);
        var tmpPath = path + ".tmp";

        await _ioLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await File.WriteAllTextAsync(tmpPath, entry.Markdown, cancellationToken).ConfigureAwait(false);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmpPath, path);
        }
        finally
        {
            _ioLock.Release();
        }

        return path;
    }

    public async Task<IReadOnlyList<DiaryEntry>> LoadAsync(string contestId, string persona, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(persona);

        var dir = Path.Combine(_rootDir, SafeSegment(contestId), SafeSegment(persona));
        if (!Directory.Exists(dir)) return Array.Empty<DiaryEntry>();

        var result = new List<DiaryEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.md").OrderBy(p => p, StringComparer.Ordinal))
        {
            var content = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
            var day = int.TryParse(Path.GetFileNameWithoutExtension(file), out var d) ? d : 0;
            // We intentionally don't re-derive WordCount + CitedEventIds on load
            // — those are generator-time gates, not load-time invariants.
            result.Add(new DiaryEntry(
                ContestId: contestId,
                Persona: persona,
                Day: day,
                GeneratedAtUtc: File.GetLastWriteTimeUtc(file),
                LeaderboardPosition: 0,
                WordCount: 0,
                CitedEventIds: Array.Empty<string>(),
                Markdown: content));
        }
        return result;
    }

    /// <summary>Reject path-traversal segments; allow only ASCII alnum + dash + underscore + dot.</summary>
    private static string SafeSegment(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);
        var sanitized = new string(raw.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' ? c : '_').ToArray());
        if (sanitized.Length == 0 || sanitized == "." || sanitized == "..")
        {
            throw new ArgumentException($"Segment '{raw}' is not safe for filesystem use.", nameof(raw));
        }
        return sanitized;
    }
}
