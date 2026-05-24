using SalesArena.Orchestrator.Leaderboard;
using SalesArena.Orchestrator.Ledger;
using SalesArena.Orchestrator.LeadPool;

namespace SalesArena.Orchestrator.Glengarry;

/// <summary>
/// Default <see cref="IGlengarryDripPolicyRunner"/>. Reads the supplied
/// leaderboard, identifies the top-tier (Cadillac) and bottom-tier
/// (YouAreFired) personas, drips premium leads to the top + revokes leads
/// from the bottom, and emits <see cref="ArenaEventKinds.GlengarryLeadDripped"/>
/// + <see cref="ArenaEventKinds.LeadsRevoked"/> events to the ledger.
///
/// <para>Cooldowns: per policy, a persona can only receive a drip once per
/// <see cref="GlengarryDripPolicy.DripWindow"/>; a persona's leads can only
/// be revoked once per window when <see cref="GlengarryDripPolicy.HonorBottomCooldown"/>
/// is true. State is in-memory — callers that want durable cooldown should
/// hydrate from the ledger (read recent GlengarryLeadDripped events).</para>
/// </summary>
public sealed class GlengarryDripPolicyRunner : IGlengarryDripPolicyRunner
{
    private readonly ILeadPool _leadPool;
    private readonly IArenaLedger _ledger;
    private readonly GlengarryDripPolicy _policy;

    // Cooldown tracking (in-memory; per-process).
    private readonly Dictionary<string, DateTimeOffset> _lastDripByPersona = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastRevokeByPersona = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public GlengarryDripPolicyRunner(ILeadPool leadPool, IArenaLedger ledger, GlengarryDripPolicy? policy = null)
    {
        _leadPool = leadPool ?? throw new ArgumentNullException(nameof(leadPool));
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _policy = policy ?? GlengarryDripPolicy.Default;
    }

    public async Task<GlengarryDripDecision> RunDripCycleAsync(
        Leaderboard.Leaderboard leaderboard,
        DateTimeOffset asOfUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(leaderboard);

        await _runLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var topRow = leaderboard.Entries.FirstOrDefault(r => r.Tier == LeaderboardTier.Cadillac);
            if (topRow is null)
            {
                return GlengarryDripDecision.Skipped(GlengarryDripSkipReasons.NoTopPersona, asOfUtc);
            }

            // Cooldown: top persona must wait one full window between drips.
            if (_lastDripByPersona.TryGetValue(topRow.Persona, out var lastTop)
                && asOfUtc - lastTop < _policy.DripWindow)
            {
                return GlengarryDripDecision.Skipped(GlengarryDripSkipReasons.NotDueYet, asOfUtc);
            }

            // Attempt the drip first — non-mutating if it fails.
            IReadOnlyList<Lead> drippedLeads;
            try
            {
                drippedLeads = await _leadPool.AssignAsync(
                    topRow.Persona, _policy.DripCount, tier: _policy.PremiumTierName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (LeadPoolException ex) when (ex.Code == LeadPoolException.Codes.InsufficientAvailable)
            {
                return GlengarryDripDecision.Skipped(GlengarryDripSkipReasons.NoPremiumLeadsAvailable, asOfUtc);
            }

            var drippedIds = drippedLeads.Select(l => l.Id).ToList();
            await EmitDripEventAsync(leaderboard.ContestId, topRow.Persona, drippedIds, asOfUtc, cancellationToken).ConfigureAwait(false);
            _lastDripByPersona[topRow.Persona] = asOfUtc;

            // Revoke from the bottom-tier persona (if there is one + cooldown allows + they have leads).
            var bottomRow = leaderboard.Entries.LastOrDefault(r => r.Tier == LeaderboardTier.YouAreFired);
            if (bottomRow is null || string.Equals(bottomRow.Persona, topRow.Persona, StringComparison.Ordinal))
            {
                return new GlengarryDripDecision(
                    Reason: "drip_only_no_bottom_tier",
                    TopPersona: topRow.Persona,
                    DrippedLeadIds: drippedIds,
                    BottomPersona: null,
                    RevokedLeadIds: Array.Empty<string>(),
                    RunAtUtc: asOfUtc);
            }

            if (_policy.HonorBottomCooldown
                && _lastRevokeByPersona.TryGetValue(bottomRow.Persona, out var lastBottom)
                && asOfUtc - lastBottom < _policy.DripWindow)
            {
                return new GlengarryDripDecision(
                    Reason: "drip_only_bottom_in_cooldown",
                    TopPersona: topRow.Persona,
                    DrippedLeadIds: drippedIds,
                    BottomPersona: bottomRow.Persona,
                    RevokedLeadIds: Array.Empty<string>(),
                    RunAtUtc: asOfUtc);
            }

            var bottomLeads = _leadPool.GetAssignedLeads(bottomRow.Persona);
            if (bottomLeads.Count == 0)
            {
                return new GlengarryDripDecision(
                    Reason: "drip_only_bottom_empty",
                    TopPersona: topRow.Persona,
                    DrippedLeadIds: drippedIds,
                    BottomPersona: bottomRow.Persona,
                    RevokedLeadIds: Array.Empty<string>(),
                    RunAtUtc: asOfUtc);
            }

            var toRevoke = bottomLeads.Take(_policy.BottomRevokeCount).ToList();
            await _leadPool.ReleaseAsync(bottomRow.Persona, toRevoke, cancellationToken).ConfigureAwait(false);
            await EmitRevokeEventAsync(leaderboard.ContestId, bottomRow.Persona, toRevoke, asOfUtc, cancellationToken).ConfigureAwait(false);
            _lastRevokeByPersona[bottomRow.Persona] = asOfUtc;

            return new GlengarryDripDecision(
                Reason: "drip_and_revoke",
                TopPersona: topRow.Persona,
                DrippedLeadIds: drippedIds,
                BottomPersona: bottomRow.Persona,
                RevokedLeadIds: toRevoke,
                RunAtUtc: asOfUtc);
        }
        finally
        {
            _runLock.Release();
        }
    }

    private Task EmitDripEventAsync(string contestId, string persona, IReadOnlyList<string> leadIds, DateTimeOffset at, CancellationToken ct)
    {
        var payload = new GlengarryLeadDrippedPayload(
            Persona: persona,
            LeadIds: leadIds,
            Reason: $"tier-1-window-{at:O}");
        return _ledger.AppendAsync(new ArenaEvent
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.GlengarryLeadDripped,
            OccurredAtUtc = at,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        }, ct);
    }

    private Task EmitRevokeEventAsync(string contestId, string persona, IReadOnlyList<string> leadIds, DateTimeOffset at, CancellationToken ct)
    {
        var payload = new LeadsRevokedPayload(
            Persona: persona,
            LeadIds: leadIds,
            Reason: $"bottom-tier-window-{at:O}");
        return _ledger.AppendAsync(new ArenaEvent
        {
            ContestId = contestId,
            Kind = ArenaEventKinds.LeadsRevoked,
            OccurredAtUtc = at,
            Persona = persona,
            PayloadJson = ArenaEvent.SerializePayload(payload),
        }, ct);
    }
}
