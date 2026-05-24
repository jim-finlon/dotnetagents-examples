using System.Collections.Concurrent;
using System.Text.Json;

namespace SalesArena.Orchestrator.LeadPool;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="ILeadPool"/>. Single
/// <see cref="SemaphoreSlim"/> serializes assign/release so the per-lead
/// transition (available → assigned → released → available) is atomic across
/// concurrent persona pods.
///
/// <para>Backed by three dictionaries (the pack, the assignment map, and the
/// release set) so reads are lock-free for the snapshot query path.</para>
/// </summary>
public sealed class InMemoryLeadPool : ILeadPool
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    // Loaded pack (immutable after Load).
    private LeadPack? _pack;
    private IReadOnlyList<Lead>? _leadsInOrder;
    private IReadOnlyDictionary<string, Lead>? _byId;

    // Assignment state. Lead-id → pod-id.
    private readonly ConcurrentDictionary<string, string> _assigned = new(StringComparer.Ordinal);
    // Released leads (id) — available again, tracked separately for stats.
    private readonly ConcurrentDictionary<string, byte> _released = new(StringComparer.Ordinal);

    public async Task<LeadPack> LoadAsync(string packJsonPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packJsonPath);

        if (!File.Exists(packJsonPath))
        {
            throw new LeadPoolException(
                $"Lead-pack file not found: '{packJsonPath}'.",
                LeadPoolException.Codes.PackInvalid);
        }

        string raw;
        try
        {
            raw = await File.ReadAllTextAsync(packJsonPath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new LeadPoolException(
                $"Failed to read lead-pack file '{packJsonPath}': {ex.Message}",
                LeadPoolException.Codes.PackInvalid);
        }

        var raw_pack = ParsePack(raw);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _pack = raw_pack;
            _leadsInOrder = raw_pack.Leads;
            _byId = raw_pack.Leads.ToDictionary(l => l.Id, StringComparer.Ordinal);
            _assigned.Clear();
            _released.Clear();
        }
        finally
        {
            _writeLock.Release();
        }

        return raw_pack;
    }

    public async Task<IReadOnlyList<Lead>> AssignAsync(
        string podId,
        int count,
        string? tier = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureLoaded();

            var candidates = new List<Lead>(count);
            foreach (var lead in _leadsInOrder!)
            {
                if (candidates.Count == count) break;
                if (_assigned.ContainsKey(lead.Id)) continue;
                if (tier is not null && !string.Equals(lead.Tier, tier, StringComparison.OrdinalIgnoreCase)) continue;
                candidates.Add(lead);
            }

            if (candidates.Count < count)
            {
                throw new LeadPoolException(
                    $"Pod '{podId}' requested {count} leads (tier='{tier ?? "any"}') but only {candidates.Count} are available.",
                    LeadPoolException.Codes.InsufficientAvailable);
            }

            foreach (var lead in candidates)
            {
                _assigned[lead.Id] = podId;
                _released.TryRemove(lead.Id, out _);
            }

            return candidates;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ReleaseAsync(string podId, IEnumerable<string> leadIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podId);
        ArgumentNullException.ThrowIfNull(leadIds);

        var batch = leadIds.ToList();
        if (batch.Count == 0) return;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureLoaded();

            // Validate everything before mutating.
            foreach (var id in batch)
            {
                if (!_byId!.ContainsKey(id))
                {
                    throw new LeadPoolException(
                        $"Lead '{id}' is not in the loaded pack.",
                        LeadPoolException.Codes.LeadUnknown);
                }
                if (!_assigned.TryGetValue(id, out var owner) || !string.Equals(owner, podId, StringComparison.Ordinal))
                {
                    throw new LeadPoolException(
                        $"Lead '{id}' is not currently assigned to pod '{podId}' (owner='{owner ?? "<unassigned>"}').",
                        LeadPoolException.Codes.LeadNotAssignedToPod);
                }
            }

            foreach (var id in batch)
            {
                _assigned.TryRemove(id, out _);
                _released[id] = 0;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public LeadPoolSnapshot Snapshot()
    {
        // Lock-free read path. Total is fixed by the loaded pack; assigned + released
        // are tracked dictionaries; available is derived.
        var total = _leadsInOrder?.Count ?? 0;
        var assigned = _assigned.Count;
        var released = _released.Count;
        var available = total - assigned - released;

        var assignedByPod = _assigned
            .GroupBy(kv => kv.Value, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        var availableByTier = _leadsInOrder is null
            ? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            : _leadsInOrder
                .Where(l => !_assigned.ContainsKey(l.Id) && !_released.ContainsKey(l.Id))
                .GroupBy(l => l.Tier, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return new LeadPoolSnapshot(total, available, assigned, released, assignedByPod, availableByTier);
    }

    public string? GetAssignedPod(string leadId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(leadId);
        return _assigned.TryGetValue(leadId, out var pod) ? pod : null;
    }

    public IReadOnlyList<string> GetAssignedLeads(string podId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(podId);
        // Walk leads in load-order so the result is deterministic for
        // Glengarry-drip + replay reproducibility.
        if (_leadsInOrder is null) return Array.Empty<string>();
        var owned = new List<string>();
        foreach (var lead in _leadsInOrder)
        {
            if (_assigned.TryGetValue(lead.Id, out var owner) && string.Equals(owner, podId, StringComparison.Ordinal))
            {
                owned.Add(lead.Id);
            }
        }
        return owned;
    }

    // ---- internals -------------------------------------------------------

    private void EnsureLoaded()
    {
        if (_leadsInOrder is null)
        {
            throw new LeadPoolException(
                "No lead pack loaded. Call LoadAsync before assigning.",
                LeadPoolException.Codes.PackNotLoaded);
        }
    }

    private static LeadPack ParsePack(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            string version = root.TryGetProperty("version", out var v) ? v.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            string description = root.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            bool synthetic = root.TryGetProperty("synthetic", out var s) && s.ValueKind == JsonValueKind.True;

            if (version is not ("v1" or "v2"))
            {
                throw new LeadPoolException(
                    $"Unsupported lead-pack version '{version}'. Expected 'v1' or 'v2'.",
                    LeadPoolException.Codes.PackInvalid);
            }
            if (!synthetic)
            {
                throw new LeadPoolException(
                    "Lead pack must declare 'synthetic: true'. Real-data packs use a separate schema (see SA-06-06).",
                    LeadPoolException.Codes.PackInvalid);
            }

            if (!root.TryGetProperty("leads", out var leadsEl) || leadsEl.ValueKind != JsonValueKind.Array)
            {
                throw new LeadPoolException(
                    "Lead pack missing 'leads' array.",
                    LeadPoolException.Codes.PackInvalid);
            }

            var leads = new List<Lead>(leadsEl.GetArrayLength());
            foreach (var leadEl in leadsEl.EnumerateArray())
            {
                var lead = ParseLead(leadEl);
                leads.Add(lead);
            }

            // Lead-id uniqueness check.
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lead in leads)
            {
                if (!ids.Add(lead.Id))
                {
                    throw new LeadPoolException(
                        $"Duplicate lead id in pack: '{lead.Id}'.",
                        LeadPoolException.Codes.PackInvalid);
                }
            }

            return new LeadPack(version, name, description, synthetic, leads);
        }
        catch (LeadPoolException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new LeadPoolException(
                $"Lead pack JSON is malformed: {ex.Message}",
                LeadPoolException.Codes.PackInvalid);
        }
    }

    private static Lead ParseLead(JsonElement el)
    {
        string GetReq(string name)
        {
            if (!el.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            {
                throw new LeadPoolException(
                    $"Lead is missing required string field '{name}'.",
                    LeadPoolException.Codes.PackInvalid);
            }
            return v.GetString()!;
        }

        string? GetOpt(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String) return null;
            return v.GetString();
        }

        int? GetOptInt(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number) return null;
            return v.GetInt32();
        }

        decimal? GetOptDecimal(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number) return null;
            return v.GetDecimal();
        }

        double? GetOptDouble(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number) return null;
            return v.GetDouble();
        }

        IReadOnlyList<string>? GetOptStringArray(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string>();
            foreach (var item in v.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
            }
            return list.Count == 0 ? null : list;
        }

        var id = GetReq("id");
        var tier = GetReq("tier");
        if (!el.TryGetProperty("company", out var company) || company.ValueKind != JsonValueKind.Object)
        {
            throw new LeadPoolException(
                $"Lead '{id}' missing required object 'company'.",
                LeadPoolException.Codes.PackInvalid);
        }
        var companyName = GetOpt(company, "name") ?? throw new LeadPoolException(
            $"Lead '{id}' missing required 'company.name'.",
            LeadPoolException.Codes.PackInvalid);

        el.TryGetProperty("contact", out var contact);
        var hasContact = contact.ValueKind == JsonValueKind.Object;

        return new Lead
        {
            Id = id,
            Tier = tier,
            CompanyName = companyName,
            Industry = GetOpt(company, "industry"),
            Size = GetOpt(company, "size"),
            Region = GetOpt(company, "region"),
            Domain = GetOpt(company, "domain"),
            Headcount = GetOptInt(company, "headcount"),
            ContactFirstName = hasContact ? GetOpt(contact, "firstName") : null,
            ContactLastName = hasContact ? GetOpt(contact, "lastName") : null,
            ContactRole = hasContact ? GetOpt(contact, "role") : null,
            ContactEmail = hasContact ? GetOpt(contact, "email") : null,
            ContactPhone = hasContact ? GetOpt(contact, "phone") : null,
            Notes = GetOpt(el, "notes"),
            CustomerTier = GetOpt(el, "customer_tier"),
            Mrr = GetOptDecimal(el, "mrr"),
            RenewalDate = GetOpt(el, "renewal_date"),
            ChurnRiskScore = GetOptDouble(el, "churn_risk_score"),
            ExpansionSignals = GetOptStringArray(el, "expansion_signal"),
        };
    }
}
