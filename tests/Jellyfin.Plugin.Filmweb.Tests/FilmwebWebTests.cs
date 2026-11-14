using Jellyfin.Plugin.Filmweb.Web;

namespace Jellyfin.Plugin.Filmweb.Tests;

public class FilmwebWebTests
{
    [Fact]
    public void Style_block_contains_icon_and_targets_star_icon()
    {
        var style = FilmwebWebAssets.StyleBlock;
        Assert.StartsWith("<style id=\"filmweb-rating-icon\">", style, StringComparison.Ordinal);
        Assert.Contains(".starRatingContainer > .starIcon", style, StringComparison.Ordinal);
        Assert.Contains("data:image/svg+xml;base64,", style, StringComparison.Ordinal);
        Assert.DoesNotContain("{{FILMWEB_ICON}}", style, StringComparison.Ordinal);
        Assert.Contains("<svg", FilmwebWebAssets.IconSvg, StringComparison.Ordinal);
    }

    [Fact]
    public void InjectInto_adds_style_before_head_end_once()
    {
        const string html = "<html><head><title>Jellyfin</title></head><body></body></html>";
        var once = FilmwebWebAssets.InjectInto(html, FilmwebWebAssets.StyleBlock);
        Assert.Contains(FilmwebWebAssets.StyleBlock + "</head>", once, StringComparison.Ordinal);

        var twice = FilmwebWebAssets.InjectInto(once, FilmwebWebAssets.StyleBlock);
        Assert.Equal(once, twice);
    }

    [Fact]
    public void InjectInto_leaves_html_without_head_untouched()
    {
        const string html = "<p>brak head</p>";
        Assert.Same(html, FilmwebWebAssets.InjectInto(html, FilmwebWebAssets.StyleBlock));
    }

    [Theory]
    [InlineData("/web/", true)]
    [InlineData("/web/index.html", true)]
    [InlineData("/jellyfin/web/", true)]
    [InlineData("/jellyfin/Web/Index.html", true)]
    [InlineData("/web", false)]
    [InlineData("/web/main.bundle.js", false)]
    [InlineData("/Items/123", false)]
    [InlineData("", false)]
    public void IsWebIndexPath_matches_only_web_client_index(string path, bool expected)
    {
        Assert.Equal(expected, FilmwebWebMiddleware.IsWebIndexPath(path));
    }
}
