using Jellyfin.Plugin.Filmweb.Filmweb;
using Jellyfin.Plugin.Filmweb.Services;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.Filmweb.Providers;

public sealed class FilmwebMovieProvider : ICustomMetadataProvider<Movie>, IHasOrder
{
    private readonly FilmwebRatingService _service;

    public FilmwebMovieProvider(FilmwebRatingService service)
    {
        _service = service;
    }

    public string Name => FilmwebNames.ProviderName;

    public int Order => 1000;

    public async Task<ItemUpdateType> FetchAsync(Movie item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var status = await _service.ApplyAsync(item, cancellationToken).ConfigureAwait(false);
        return status == ApplyStatus.Updated ? ItemUpdateType.MetadataEdit : ItemUpdateType.None;
    }
}

public sealed class FilmwebSeriesProvider : ICustomMetadataProvider<Series>, IHasOrder
{
    private readonly FilmwebRatingService _service;

    public FilmwebSeriesProvider(FilmwebRatingService service)
    {
        _service = service;
    }

    public string Name => FilmwebNames.ProviderName;

    public int Order => 1000;

    public async Task<ItemUpdateType> FetchAsync(Series item, MetadataRefreshOptions options, CancellationToken cancellationToken)
    {
        var status = await _service.ApplyAsync(item, cancellationToken).ConfigureAwait(false);
        return status == ApplyStatus.Updated ? ItemUpdateType.MetadataEdit : ItemUpdateType.None;
    }
}
