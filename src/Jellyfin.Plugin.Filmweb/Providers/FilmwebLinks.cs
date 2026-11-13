using System.Globalization;
using Jellyfin.Plugin.Filmweb.Filmweb;
using Jellyfin.Plugin.Filmweb.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Filmweb.Providers;

public sealed class FilmwebMovieExternalId : IExternalId
{
    public string ProviderName => FilmwebNames.ProviderName;

    public string Key => FilmwebNames.Key;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

    public bool Supports(IHasProviderIds item) => item is Movie;
}

public sealed class FilmwebSeriesExternalId : IExternalId
{
    public string ProviderName => FilmwebNames.ProviderName;

    public string Key => FilmwebNames.Key;

    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;

    public bool Supports(IHasProviderIds item) => item is Series;
}

public sealed class FilmwebExternalUrlProvider : IExternalUrlProvider
{
    private readonly FilmwebCache _cache;

    public FilmwebExternalUrlProvider(FilmwebCache cache)
    {
        _cache = cache;
    }

    public string Name => FilmwebNames.ProviderName;

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is not (Movie or Series))
        {
            yield break;
        }

        if (!item.ProviderIds.TryGetValue(FilmwebNames.Key, out var raw)
            || !int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            yield break;
        }

        if (_cache.TryGetTitle(id, TimeSpan.FromDays(3650), out var title) && title is not null)
        {
            yield return FilmwebUrls.TitlePage(title);
        }
    }
}
