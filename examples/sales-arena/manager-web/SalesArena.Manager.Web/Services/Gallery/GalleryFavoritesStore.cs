namespace SalesArena.Manager.Web.Services.Gallery;

public sealed class GalleryFavoritesStore
{
    private readonly HashSet<string> _favorites = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Favorites => _favorites;

    public bool IsFavorite(string slug) => _favorites.Contains(slug);

    public void Toggle(string slug)
    {
        if (!_favorites.Add(slug))
        {
            _favorites.Remove(slug);
        }
    }
}
