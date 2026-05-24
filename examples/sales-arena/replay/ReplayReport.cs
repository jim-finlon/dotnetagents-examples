namespace SalesArena.Replay;

/// <summary>
/// Output of <see cref="IReplayGenerator.GenerateAsync"/>. Markdown is the
/// full assembled report; <see cref="Sections"/> exposes each rendered block
/// individually for downstream consumers (UI tabs, partial republish, etc.).
/// </summary>
public sealed record ReplayReport(
    string ContestId,
    DateTimeOffset GeneratedAtUtc,
    string Markdown,
    IReadOnlyList<ReplaySection> Sections,
    IReadOnlyList<ReplayHighlight> Highlights);

/// <summary>One rendered section inside a <see cref="ReplayReport"/>.</summary>
public sealed record ReplaySection(
    ReplaySectionKind Kind,
    string Title,
    string Markdown);

/// <summary>
/// A noteworthy moment surfaced during replay generation. Used by the
/// narrative-mode rewriter (SA-04-04) as anchor events.
/// </summary>
public sealed record ReplayHighlight(
    ReplaySectionKind Source,
    string Headline,
    string? Persona,
    string? LeadId,
    decimal? ValueUsd,
    DateTimeOffset? OccurredAtUtc);
