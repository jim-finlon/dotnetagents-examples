using System.Globalization;
using System.Text;
using SalesArena.Orchestrator.Ledger;

namespace SalesArena.Replay.TrophyRoom;

/// <summary>
/// Default <see cref="ITrophyRoomBuilder"/>. Scans the entire ledger via a
/// no-contest-filter query, groups events by ContestId, picks the winner per
/// contest from the final LeaderboardSnapshot (or the last DealClosed Won
/// fallback), aggregates lifetime stats per persona, and renders Markdown.
///
/// <para>Reads are O(events). Sqlite indexes (contest_id, persona, kind)
/// from SA-02-03 keep this fast even for hundreds of contests.</para>
/// </summary>
public sealed class TrophyRoomBuilder : ITrophyRoomBuilder
{
    private readonly IArenaLedger _ledger;
    private readonly TimeProvider _time;

    public TrophyRoomBuilder(IArenaLedger ledger, TimeProvider? time = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _time = time ?? TimeProvider.System;
    }

    public async Task<TrophyRoomReport> BuildAsync(
        TrophyRoomOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new TrophyRoomOptions();

        // Scan all DealClosed + LeaderboardSnapshot events. Other kinds aren't
        // needed for the Trophy Room view.
        var byContest = new Dictionary<string, List<ArenaEvent>>(StringComparer.Ordinal);
        var filter = new ArenaEventFilter(FromUtc: options.SinceUtc, ToUtc: options.UntilUtc);

        await foreach (var evt in _ledger.QueryAsync(filter, cancellationToken).ConfigureAwait(false))
        {
            if (evt.Kind != ArenaEventKinds.DealClosed && evt.Kind != ArenaEventKinds.LeaderboardSnapshot)
                continue;
            if (!byContest.TryGetValue(evt.ContestId, out var list))
            {
                list = new List<ArenaEvent>();
                byContest[evt.ContestId] = list;
            }
            list.Add(evt);
        }

        var trophies = new List<TrophyEntry>();
        foreach (var (contestId, events) in byContest)
        {
            var trophy = BuildTrophy(contestId, events);
            if (trophy is not null) trophies.Add(trophy);
        }

        trophies = trophies
            .OrderByDescending(t => t.ClosedAtUtc)
            .Take(options.MaxTrophies ?? int.MaxValue)
            .ToList();

        var baseballCards = BuildBaseballCards(byContest, trophies, options.OnlyPersonas);

        var generatedAt = _time.GetUtcNow();
        var markdown = RenderMarkdown(generatedAt, byContest.Count, trophies, baseballCards);
        return new TrophyRoomReport(
            GeneratedAtUtc: generatedAt,
            TotalContests: byContest.Count,
            Trophies: trophies,
            BaseballCards: baseballCards,
            Markdown: markdown);
    }

    // ---- per-contest winner ---------------------------------------------

    private static TrophyEntry? BuildTrophy(string contestId, IReadOnlyList<ArenaEvent> events)
    {
        var lastSnapshot = events
            .Where(e => e.Kind == ArenaEventKinds.LeaderboardSnapshot)
            .OrderByDescending(e => e.OccurredAtUtc)
            .FirstOrDefault();

        if (lastSnapshot is not null)
        {
            var payload = lastSnapshot.GetPayload<LeaderboardSnapshotPayload>();
            var winner = payload?.Entries.FirstOrDefault(e => e.Position == 1);
            if (winner is not null)
            {
                return new TrophyEntry(
                    ContestId: contestId,
                    ContestDisplayName: null,
                    ClosedAtUtc: lastSnapshot.OccurredAtUtc,
                    WinnerPersona: winner.Persona,
                    WinnerRevenueUsd: winner.RevenueUsd,
                    WinnerDealsWon: winner.DealsWon);
            }
        }

        // Fallback: highest-revenue persona by summing DealClosed Won events.
        var revenueByPersona = new Dictionary<string, (decimal Revenue, int Wins, DateTimeOffset Latest)>(StringComparer.Ordinal);
        foreach (var evt in events.Where(e => e.Kind == ArenaEventKinds.DealClosed && !string.IsNullOrEmpty(e.Persona)))
        {
            var payload = evt.GetPayload<DealClosedPayload>();
            if (payload is null || !string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = payload.ValueUsd ?? 0m;
            if (revenueByPersona.TryGetValue(evt.Persona!, out var cur))
            {
                revenueByPersona[evt.Persona!] = (cur.Revenue + value, cur.Wins + 1, evt.OccurredAtUtc > cur.Latest ? evt.OccurredAtUtc : cur.Latest);
            }
            else
            {
                revenueByPersona[evt.Persona!] = (value, 1, evt.OccurredAtUtc);
            }
        }

        if (revenueByPersona.Count == 0) return null;
        var champion = revenueByPersona.OrderByDescending(kv => kv.Value.Revenue).First();
        return new TrophyEntry(
            ContestId: contestId,
            ContestDisplayName: null,
            ClosedAtUtc: champion.Value.Latest,
            WinnerPersona: champion.Key,
            WinnerRevenueUsd: champion.Value.Revenue,
            WinnerDealsWon: champion.Value.Wins);
    }

    // ---- baseball cards --------------------------------------------------

    private static IReadOnlyList<PersonaBaseballCard> BuildBaseballCards(
        Dictionary<string, List<ArenaEvent>> byContest,
        IReadOnlyList<TrophyEntry> trophies,
        IReadOnlyList<string>? onlyPersonas)
    {
        var personas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (_, events) in byContest)
        {
            foreach (var evt in events.Where(e => !string.IsNullOrEmpty(e.Persona)))
            {
                personas.Add(evt.Persona!);
            }
        }
        if (onlyPersonas is not null)
        {
            personas.IntersectWith(onlyPersonas);
        }

        var cards = new List<PersonaBaseballCard>();
        foreach (var persona in personas)
        {
            cards.Add(BuildOneCard(persona, byContest, trophies));
        }

        return cards
            .OrderByDescending(c => c.CadillacWins)
            .ThenByDescending(c => c.LifetimeRevenueUsd)
            .ThenBy(c => c.Persona, StringComparer.Ordinal)
            .ToList();
    }

    private static PersonaBaseballCard BuildOneCard(
        string persona,
        Dictionary<string, List<ArenaEvent>> byContest,
        IReadOnlyList<TrophyEntry> trophies)
    {
        int contestsEntered = 0;
        int cadillacWins = 0;
        int steakKnives = 0;
        int youAreFired = 0;
        decimal lifetimeRevenue = 0m;
        int lifetimeWins = 0;
        int lifetimeLosses = 0;
        decimal signatureValue = 0m;
        string? signatureLeadId = null;
        DateTimeOffset? signatureAt = null;
        decimal bestContestRevenue = 0m;
        string? bestContestId = null;
        DateTimeOffset? firstAt = null;
        DateTimeOffset? mostRecentAt = null;

        foreach (var (contestId, events) in byContest)
        {
            var personaEvents = events.Where(e => string.Equals(e.Persona, persona, StringComparison.Ordinal)).ToList();
            if (personaEvents.Count == 0) continue;

            contestsEntered++;
            var contestStart = personaEvents.Min(e => e.OccurredAtUtc);
            var contestEnd = personaEvents.Max(e => e.OccurredAtUtc);
            if (firstAt is null || contestStart < firstAt) firstAt = contestStart;
            if (mostRecentAt is null || contestEnd > mostRecentAt) mostRecentAt = contestEnd;

            // Per-contest deal stats
            decimal contestRevenue = 0m;
            int contestWins = 0;
            int contestLosses = 0;
            foreach (var evt in personaEvents.Where(e => e.Kind == ArenaEventKinds.DealClosed))
            {
                var payload = evt.GetPayload<DealClosedPayload>();
                if (payload is null) continue;
                if (string.Equals(payload.Outcome, "Won", StringComparison.OrdinalIgnoreCase))
                {
                    contestWins++;
                    var v = payload.ValueUsd ?? 0m;
                    contestRevenue += v;
                    if (v > signatureValue)
                    {
                        signatureValue = v;
                        signatureLeadId = payload.LeadId;
                        signatureAt = evt.OccurredAtUtc;
                    }
                }
                else if (string.Equals(payload.Outcome, "Lost", StringComparison.OrdinalIgnoreCase))
                {
                    contestLosses++;
                }
            }
            lifetimeRevenue += contestRevenue;
            lifetimeWins += contestWins;
            lifetimeLosses += contestLosses;
            if (contestRevenue > bestContestRevenue)
            {
                bestContestRevenue = contestRevenue;
                bestContestId = contestId;
            }

            // Tier finish — read last LeaderboardSnapshot's entry for this persona.
            var lastSnapshot = events
                .Where(e => e.Kind == ArenaEventKinds.LeaderboardSnapshot)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Select(e => e.GetPayload<LeaderboardSnapshotPayload>())
                .FirstOrDefault(p => p is not null);
            if (lastSnapshot is not null)
            {
                var row = lastSnapshot.Entries.FirstOrDefault(r => string.Equals(r.Persona, persona, StringComparison.Ordinal));
                if (row is not null)
                {
                    switch (row.Tier)
                    {
                        case "Cadillac": cadillacWins++; break;
                        case "SteakKnives": steakKnives++; break;
                        case "YouAreFired": youAreFired++; break;
                    }
                }
            }
            else
            {
                // Snapshot-less contests: use the trophy entry if it matches.
                var trophy = trophies.FirstOrDefault(t => t.ContestId == contestId);
                if (trophy is not null && string.Equals(trophy.WinnerPersona, persona, StringComparison.Ordinal))
                {
                    cadillacWins++;
                }
            }
        }

        return new PersonaBaseballCard(
            Persona: persona,
            ContestsEntered: contestsEntered,
            CadillacWins: cadillacWins,
            SteakKnivesPlaces: steakKnives,
            YouAreFiredFinishes: youAreFired,
            LifetimeRevenueUsd: lifetimeRevenue,
            LifetimeDealsWon: lifetimeWins,
            LifetimeDealsLost: lifetimeLosses,
            SignatureCloseUsd: signatureValue,
            SignatureCloseLeadId: signatureLeadId,
            SignatureCloseAtUtc: signatureAt,
            BestContestRevenueUsd: bestContestRevenue,
            BestContestId: bestContestId,
            FirstContestAtUtc: firstAt,
            MostRecentContestAtUtc: mostRecentAt);
    }

    // ---- rendering -------------------------------------------------------

    private static string RenderMarkdown(
        DateTimeOffset generatedAt,
        int totalContests,
        IReadOnlyList<TrophyEntry> trophies,
        IReadOnlyList<PersonaBaseballCard> cards)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# 🏆 Trophy Room");
        sb.AppendLine();
        sb.AppendLine($"> *Generated {generatedAt:u}. {totalContests} contest(s) on record. Coffee's for closers.*");
        sb.AppendLine();

        // Trophy table
        sb.AppendLine("## Cadillac Hall of Fame");
        sb.AppendLine();
        if (trophies.Count == 0)
        {
            sb.AppendLine("*No contests have been recorded yet.*");
        }
        else
        {
            sb.AppendLine("| Closed | Contest | Winner | Revenue | Wins |");
            sb.AppendLine("|---|---|---|---:|---:|");
            foreach (var t in trophies)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "| {0:u} | `{1}` | **{2}** | ${3:N0} | {4} |",
                    t.ClosedAtUtc, t.ContestId, t.WinnerPersona, t.WinnerRevenueUsd, t.WinnerDealsWon));
            }
        }
        sb.AppendLine();

        // Baseball cards
        sb.AppendLine("## Persona Baseball Cards");
        sb.AppendLine();
        if (cards.Count == 0)
        {
            sb.AppendLine("*No persona data on record.*");
        }
        foreach (var card in cards)
        {
            sb.AppendLine($"### {card.Persona}");
            sb.AppendLine();
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Contests entered:** {0}", card.ContestsEntered));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **🚗 Cadillac wins:** {0} ({1:P0})", card.CadillacWins, card.CadillacRate));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **🔪 Steak Knives places:** {0}", card.SteakKnivesPlaces));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **📦 You're Fired finishes:** {0}", card.YouAreFiredFinishes));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Lifetime revenue:** ${0:N0}", card.LifetimeRevenueUsd));
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Lifetime deals:** {0} won / {1} lost ({2:P0} win rate)", card.LifetimeDealsWon, card.LifetimeDealsLost, card.LifetimeWinRate));
            if (card.SignatureCloseLeadId is not null)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Signature close:** {0} for ${1:N0} ({2:u})", card.SignatureCloseLeadId, card.SignatureCloseUsd, card.SignatureCloseAtUtc ?? DateTimeOffset.MinValue));
            }
            if (card.BestContestId is not null)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "- **Best contest:** `{0}` (${1:N0})", card.BestContestId, card.BestContestRevenueUsd));
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + "\n";
    }
}
