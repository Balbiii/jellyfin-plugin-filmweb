using Jellyfin.Plugin.Filmweb.Filmweb;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.Filmweb.Tests;

public class FilmwebLiveApiTests
{
    [Fact]
    public async Task Filmweb_returns_inception_and_game_of_thrones()
    {
        using var http = new HttpClient
        {
            BaseAddress = new Uri("https://www.filmweb.pl/api/v1/"),
            Timeout = TimeSpan.FromSeconds(20)
        };
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Filmweb-Plugin/1.0");
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        http.DefaultRequestHeaders.TryAddWithoutValidation("x-locale", "pl_PL");

        var client = new FilmwebClient(http, NullLogger<FilmwebClient>.Instance);
        var hits = await client.SearchAsync("Incepcja", CancellationToken.None);
        Assert.Contains(hits, hit => hit.Id == 500891 && hit.Type == "film");

        var title = await client.GetTitleAsync(500891, CancellationToken.None);
        Assert.NotNull(title);
        Assert.Equal("Incepcja", title.Title);
        Assert.Equal("Inception", title.OriginalTitle);
        Assert.Equal(2010, title.Year);

        var rating = await client.GetRatingAsync(500891, CancellationToken.None);
        Assert.NotNull(rating);
        Assert.InRange(rating.Rate, 1, 10);
        Assert.True(rating.Count > 1000);

        var movie = await FilmwebResolver.ResolveAsync(
            client,
            new MatchRequest("Inception", null, 2010, false, []),
            5,
            CancellationToken.None);
        Assert.Equal(500891, movie.FilmwebId);

        var series = await FilmwebResolver.ResolveAsync(
            client,
            new MatchRequest("Game of Thrones", null, 2011, true, []),
            5,
            CancellationToken.None);
        Assert.Equal(476848, series.FilmwebId);
    }
}
