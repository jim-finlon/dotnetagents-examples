namespace SalesArena.Training.Diary;

/// <summary>
/// Persistence seam for diary entries. Each implementer chooses how to
/// physically lay out entries (in-memory dictionary, file-system Markdown
/// tree, sqlite blob, etc.).
/// </summary>
public interface IDiaryStore
{
    /// <summary>Save (or overwrite) a diary entry. Returns the canonical reference.</summary>
    Task<string> SaveAsync(DiaryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Load the chronological persona arc for one contest.</summary>
    Task<IReadOnlyList<DiaryEntry>> LoadAsync(
        string contestId,
        string persona,
        CancellationToken cancellationToken = default);
}
