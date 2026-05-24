using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using SalesArena.Manager.Web.Services.Gallery;
using Xunit;

namespace SalesArena.Manager.Web.Tests;

public sealed class CommunityPersonaGalleryCatalogTests
{
    [Fact]
    public async Task LoadIndexAsync_finds_seeded_community_packs()
    {
        var catalog = CreateCatalogFromRepoLayout();
        Assert.True(Directory.Exists(catalog.CommunityRootPath), $"community root missing: {catalog.CommunityRootPath}");

        var index = await catalog.LoadIndexAsync();

        var silent = Assert.Single(index.Cards, c => c.Slug == "the-silent-one");
        Assert.Contains(silent.Tags, t => t.Contains("silent", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(index.AllTags);
    }

    [Fact]
    public async Task GetDetailAsync_returns_sanitized_preview_and_contests()
    {
        var catalog = CreateCatalogFromRepoLayout();
        Assert.True(Directory.Exists(catalog.CommunityRootPath));

        var detail = await catalog.GetDetailAsync("engineer");

        Assert.NotNull(detail);
        Assert.False(string.IsNullOrWhiteSpace(detail!.PromptPreview));
        Assert.NotEmpty(detail.RecentContests);
    }

    private static CommunityPersonaGalleryCatalog CreateCatalogFromRepoLayout()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "manager-web", "SalesArena.Manager.Web"));
        return new CommunityPersonaGalleryCatalog(new TestWebHostEnvironment { ContentRootPath = contentRoot });
    }

    [Fact]
    public void QueueChallenge_records_slug()
    {
        var catalog = new CommunityPersonaGalleryCatalog(new TestWebHostEnvironment());
        var slug = catalog.QueueChallenge("hardballer");
        Assert.Equal("hardballer", slug);
        var concrete = Assert.IsType<CommunityPersonaGalleryCatalog>(catalog);
        Assert.Contains("hardballer", concrete.PeekChallengeQueue());
    }

    [Fact]
    public void GalleryTextSanitizer_escapes_html()
    {
        var escaped = GalleryTextSanitizer.Escape("<script>alert(1)</script>");
        Assert.DoesNotContain("<script>", escaped, StringComparison.Ordinal);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "test";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
    }
}
