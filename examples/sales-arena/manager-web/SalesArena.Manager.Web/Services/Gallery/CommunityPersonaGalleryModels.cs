namespace SalesArena.Manager.Web.Services.Gallery;

public enum GalleryEloBracket
{
    All,
    Bronze,
    Silver,
    Gold,
}

public enum GallerySignedFilter
{
    All,
    Signed,
    Unsigned,
}

public sealed record CommunityPersonaCard(
    string Slug,
    string DisplayName,
    string Author,
    string BioExcerpt,
    IReadOnlyList<string> Tags,
    int ContestsRun,
    int Wins,
    int DealsClosed,
    double AvgConversionPercent,
    int Elo,
    bool IsSigned,
    string AvatarUrl);

public sealed record CommunityPersonaContestSummary(
    string ContestName,
    string Outcome,
    DateTimeOffset EndedAtUtc);

public sealed record CommunityPersonaDetail(
    CommunityPersonaCard Card,
    string BioMarkdown,
    string PromptPreview,
    IReadOnlyList<CommunityPersonaContestSummary> RecentContests);

public sealed record CommunityPersonaGalleryIndex(
    IReadOnlyList<CommunityPersonaCard> Cards,
    IReadOnlyList<string> AllTags);
