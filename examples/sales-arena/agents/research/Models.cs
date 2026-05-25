using System;
using System.Collections.Generic;

namespace SalesArena.Research;

public sealed record ResearchRequest(string ProspectId, string PersonaId, IReadOnlyList<string> AllowedFeedHosts);

public sealed record PublicFeedItem(string Title, string Url, DateTimeOffset PublishedAtUtc, string Source);

public sealed record CompanyFact(string Label, string Value, string? Source = null);

public sealed record KnownContact(string Name, string Role, string ContactPath);

public sealed record Citation(int Index, string Source, string? Url);

public sealed record ResearchOnePager(
    string ProspectId,
    string PersonaId,
    IReadOnlyList<CompanyFact> CompanySnapshot,
    IReadOnlyList<PublicFeedItem> RecentSignals,
    IReadOnlyList<KnownContact> KnownContacts,
    IReadOnlyList<string> SuggestedAngles,
    IReadOnlyList<Citation> Citations);
