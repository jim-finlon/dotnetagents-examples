using System.Text.Json;

namespace SalesArena.Communications.Inbound;

public static class CrmProspectIndexLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<CrmProspectIndexEntry> LoadFromJson(string fixturePath)
    {
        if (!File.Exists(fixturePath))
        {
            return [];
        }

        var raw = File.ReadAllText(fixturePath);
        var envelope = JsonSerializer.Deserialize<Envelope>(raw, JsonOptions)
            ?? throw new InvalidOperationException($"Invalid CRM prospect fixture: {fixturePath}");
        return envelope.Prospects?
            .Select(p => new CrmProspectIndexEntry(p.LeadId, p.FirstName, p.LastName, p.Company, p.Email))
            .ToList() ?? [];
    }

    private sealed record Envelope(IReadOnlyList<ProspectDto>? Prospects);

    private sealed record ProspectDto(
        string LeadId,
        string? FirstName,
        string? LastName,
        string Company,
        string? Email);
}
