namespace SalesArena.Manager.Web.Services.Gallery;

public interface ICommunityPersonaGalleryCatalog
{
    string CommunityRootPath { get; }

    Task<CommunityPersonaGalleryIndex> LoadIndexAsync(CancellationToken cancellationToken = default);

    Task<CommunityPersonaDetail?> GetDetailAsync(string slug, CancellationToken cancellationToken = default);

    string? QueueChallenge(string slug);
}
