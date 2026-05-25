using SalesArena.Manager.Web.Models;

namespace SalesArena.Manager.Web.Services.Bullpen;

public sealed class BullpenTileModel
{
    public required string PersonaId { get; init; }
    public required string DisplayName { get; init; }
    public required string AvatarGlyph { get; init; }
    public FloorActivity Activity { get; init; } = FloorActivity.Idle;
    public string CurrentThought { get; init; } = "Watching the board for the next move.";
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public BullpenTileModel With(
        FloorActivity? activity = null,
        string? currentThought = null,
        DateTimeOffset? updatedAtUtc = null) =>
        new()
        {
            PersonaId = PersonaId,
            DisplayName = DisplayName,
            AvatarGlyph = AvatarGlyph,
            Activity = activity ?? Activity,
            CurrentThought = currentThought ?? CurrentThought,
            UpdatedAtUtc = updatedAtUtc ?? UpdatedAtUtc,
        };
}
