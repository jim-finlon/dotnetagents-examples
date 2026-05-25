using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SalesArena.Research;

public interface IResearchAgent
{
    Task<ResearchOnePager> AssembleOnePagerAsync(ResearchRequest request, CancellationToken ct = default);
}

public sealed class ResearchAgent : IResearchAgent
{
    private readonly IPublicFeedAdapter _feed;
    private readonly ICompanyFactProvider _facts;
    private readonly IKnownContactProvider _contacts;

    public ResearchAgent(IPublicFeedAdapter feed, ICompanyFactProvider facts, IKnownContactProvider contacts)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _facts = facts ?? throw new ArgumentNullException(nameof(facts));
        _contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
    }

    public async Task<ResearchOnePager> AssembleOnePagerAsync(ResearchRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProspectId))
            throw new ArgumentException("ProspectId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.PersonaId))
            throw new ArgumentException("PersonaId is required.", nameof(request));

        var allowedHosts = request.AllowedFeedHosts ?? Array.Empty<string>();

        var feedTask = _feed.FetchAsync(request.ProspectId, allowedHosts, ct);
        var factsTask = _facts.GetFactsAsync(request.ProspectId, ct);
        var contactsTask = _contacts.GetContactsAsync(request.ProspectId, ct);

        await Task.WhenAll(feedTask, factsTask, contactsTask).ConfigureAwait(false);

        var signals = feedTask.Result
            .OrderByDescending(s => s.PublishedAtUtc)
            .ThenBy(s => s.Title, StringComparer.Ordinal)
            .ToArray();

        var citations = BuildCitations(signals, factsTask.Result);

        var angles = SuggestAngles(factsTask.Result, signals);

        return new ResearchOnePager(
            ProspectId: request.ProspectId.Trim(),
            PersonaId: request.PersonaId.Trim(),
            CompanySnapshot: factsTask.Result,
            RecentSignals: signals,
            KnownContacts: contactsTask.Result,
            SuggestedAngles: angles,
            Citations: citations);
    }

    private static IReadOnlyList<Citation> BuildCitations(IReadOnlyList<PublicFeedItem> signals, IReadOnlyList<CompanyFact> facts)
    {
        var citations = new List<Citation>();
        int idx = 1;
        foreach (var signal in signals)
            citations.Add(new Citation(idx++, signal.Source, signal.Url));
        foreach (var fact in facts)
        {
            if (!string.IsNullOrWhiteSpace(fact.Source))
                citations.Add(new Citation(idx++, fact.Source, null));
        }
        return citations;
    }

    private static IReadOnlyList<string> SuggestAngles(IReadOnlyList<CompanyFact> facts, IReadOnlyList<PublicFeedItem> signals)
    {
        var angles = new List<string>();
        if (signals.Count > 0)
            angles.Add($"Lead with the most recent signal: \"{signals[0].Title}\".");
        foreach (var fact in facts.Take(2))
            angles.Add($"Anchor value to {fact.Label}: {fact.Value}.");
        if (angles.Count == 0)
            angles.Add("No public signals or facts available — open with discovery questions.");
        return angles;
    }
}

public static class ResearchOnePagerMarkdownExtensions
{
    public static string ToMarkdown(this ResearchOnePager pager)
    {
        if (pager is null) throw new ArgumentNullException(nameof(pager));
        var sb = new StringBuilder();
        sb.Append("# Research One-Pager — ").Append(pager.ProspectId).Append(" / ").AppendLine(pager.PersonaId);
        sb.AppendLine();

        sb.AppendLine("## Company Snapshot");
        if (pager.CompanySnapshot.Count == 0) sb.AppendLine("_None._");
        else foreach (var f in pager.CompanySnapshot) sb.Append("- **").Append(f.Label).Append(":** ").AppendLine(f.Value);
        sb.AppendLine();

        sb.AppendLine("## Recent Signals");
        if (pager.RecentSignals.Count == 0) sb.AppendLine("_None._");
        else foreach (var s in pager.RecentSignals) sb.Append("- ").Append(s.PublishedAtUtc.ToString("yyyy-MM-dd")).Append(" — ").Append(s.Title).Append(" (").Append(s.Source).AppendLine(")");
        sb.AppendLine();

        sb.AppendLine("## Known Contacts");
        if (pager.KnownContacts.Count == 0) sb.AppendLine("_None._");
        else foreach (var c in pager.KnownContacts) sb.Append("- ").Append(c.Name).Append(" (").Append(c.Role).Append(") — ").AppendLine(c.ContactPath);
        sb.AppendLine();

        sb.AppendLine("## Suggested Angles");
        foreach (var a in pager.SuggestedAngles) sb.Append("- ").AppendLine(a);
        sb.AppendLine();

        sb.AppendLine("## Citations");
        if (pager.Citations.Count == 0) sb.AppendLine("_None._");
        else foreach (var c in pager.Citations)
        {
            sb.Append("[").Append(c.Index).Append("] ").Append(c.Source);
            if (!string.IsNullOrWhiteSpace(c.Url)) sb.Append(" — ").Append(c.Url);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
