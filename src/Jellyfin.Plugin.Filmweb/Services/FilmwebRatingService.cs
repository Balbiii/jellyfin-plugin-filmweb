using System.Globalization;
using Jellyfin.Plugin.Filmweb.Configuration;
using Jellyfin.Plugin.Filmweb.Filmweb;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Filmweb.Services;

public enum ApplyStatus
{
    Updated,
    Unchanged,
    NotMatched,
    Disabled,
    Unavailable,
    Failed
}

public sealed class FilmwebRatingService
{
    private readonly FilmwebClient _client;
    private readonly FilmwebCache _cache;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FilmwebRatingService> _logger;

    public FilmwebRatingService(
        FilmwebClient client,
        FilmwebCache cache,
        ILibraryManager libraryManager,
        ILogger<FilmwebRatingService> logger)
    {
        _client = client;
        _cache = cache;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public bool IsCircuitOpen => _client.IsCircuitOpen;

    public async Task<ApplyStatus> ApplyAsync(BaseItem item, CancellationToken cancellationToken)
    {
        try
        {
            return await ApplyCoreAsync(item, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Filmweb: błąd przy {Name}", item.Name);
            return ApplyStatus.Failed;
        }
    }

    private async Task<ApplyStatus> ApplyCoreAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (item.IsLocked || item is not (Movie or Series))
        {
            return ApplyStatus.Disabled;
        }

        var config = EffectiveConfig.From(Plugin.Instance?.Configuration);
        if (item is Movie && !config.EnableMovies)
        {
            return ApplyStatus.Disabled;
        }

        if (item is Series && !config.EnableSeries)
        {
            return ApplyStatus.Disabled;
        }

        if (_client.IsCircuitOpen)
        {
            return ApplyStatus.Unavailable;
        }

        var id = ReadProviderId(item);
        if (id is null)
        {
            var request = CreateRequest(item);
            if (string.IsNullOrWhiteSpace(request.Name) && string.IsNullOrWhiteSpace(request.OriginalTitle))
            {
                return ApplyStatus.NotMatched;
            }

            var key = MatchKey(request);
            if (_cache.TryGetMatch(key, config.CacheTtl, out var cachedId))
            {
                if (cachedId is null)
                {
                    return ApplyStatus.NotMatched;
                }

                id = cachedId;
            }
            else
            {
                ResolveResult resolved;
                try
                {
                    resolved = await FilmwebResolver.ResolveAsync(new CachingApi(_client, _cache, config.CacheTtl), request, config.MaxCandidates, cancellationToken).ConfigureAwait(false);
                }
                catch (FilmwebUnavailableException ex)
                {
                    _logger.LogWarning(ex, "Filmweb niedostępny podczas dopasowania {Name}", item.Name);
                    return ApplyStatus.Unavailable;
                }

                if (!resolved.Definitive)
                {
                    return ApplyStatus.Unavailable;
                }

                _cache.PutMatch(key, resolved.FilmwebId);
                id = resolved.FilmwebId;
                if (id is null)
                {
                    _logger.LogInformation("Filmweb: pominięto {Name} ({Year}) — brak pewnego dopasowania", item.Name, item.ProductionYear);
                    return ApplyStatus.NotMatched;
                }
            }
        }

        FilmwebRating? rating;
        try
        {
            rating = await GetRatingAsync(id.Value, critics: false, config.CacheTtl, cancellationToken).ConfigureAwait(false);
        }
        catch (FilmwebUnavailableException ex)
        {
            _logger.LogWarning(ex, "Filmweb: nie pobrano oceny {Name}", item.Name);
            return ApplyStatus.Unavailable;
        }

        FilmwebRating? critics = null;
        if (config.CriticRating != RatingWriteMode.Disabled)
        {
            try
            {
                critics = await GetRatingAsync(id.Value, critics: true, config.CacheTtl, cancellationToken).ConfigureAwait(false);
            }
            catch (FilmwebUnavailableException ex)
            {
                _logger.LogWarning(ex, "Filmweb: nie pobrano oceny krytyków {Name}", item.Name);
            }
        }

        var snapshot = new RatingSnapshot
        {
            ProviderId = ReadProviderId(item)?.ToString(CultureInfo.InvariantCulture),
            CommunityRating = item.CommunityRating,
            CriticRating = item.CriticRating,
            Overview = item.Overview,
            OverviewLocked = item.LockedFields?.Contains(MetadataField.Overview) == true
        };

        if (!RatingApplier.Apply(snapshot, id.Value, rating, critics, config))
        {
            return ApplyStatus.Unchanged;
        }

        item.ProviderIds[FilmwebNames.Key] = snapshot.ProviderId ?? id.Value.ToString(CultureInfo.InvariantCulture);
        item.CommunityRating = snapshot.CommunityRating;
        item.CriticRating = snapshot.CriticRating;
        item.Overview = snapshot.Overview;
        _logger.LogInformation(
            "Filmweb: {Name} ({Year}) → {Id}, ocena {Rating}",
            item.Name,
            item.ProductionYear,
            id.Value,
            snapshot.CommunityRating);
        return ApplyStatus.Updated;
    }

    private async Task<FilmwebRating?> GetRatingAsync(int id, bool critics, TimeSpan ttl, CancellationToken cancellationToken)
    {
        if (_cache.TryGetRating(id, critics, ttl, out var cached))
        {
            return cached;
        }

        var rating = critics
            ? await _client.GetCriticsRatingAsync(id, cancellationToken).ConfigureAwait(false)
            : await _client.GetRatingAsync(id, cancellationToken).ConfigureAwait(false);
        _cache.PutRating(id, critics, rating);
        return rating;
    }

    private MatchRequest CreateRequest(BaseItem item)
    {
        var (name, year) = FilmwebText.SplitTrailingYear(item.Name, item.ProductionYear);
        var (original, _) = FilmwebText.SplitTrailingYear(item.OriginalTitle, year);
        if (string.IsNullOrWhiteSpace(original) || string.Equals(original, name, StringComparison.OrdinalIgnoreCase))
        {
            original = null;
        }

        return new MatchRequest(name, original, year, item is Series, PeopleNames(item));
    }

    private IReadOnlyList<string> PeopleNames(BaseItem item)
    {
        try
        {
            return _libraryManager.GetPeople(item)
                .Select(person => person.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Take(15)
                .ToArray()!;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Filmweb: nie udało się odczytać obsady {Name}", item.Name);
            return [];
        }
    }

    private static int? ReadProviderId(BaseItem item)
    {
        if (!item.ProviderIds.TryGetValue(FilmwebNames.Key, out var raw))
        {
            return null;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) && id > 0
            ? id
            : null;
    }

    public static string MatchKey(MatchRequest request)
    {
        var people = string.Join(
            ',',
            request.People
                .Select(FilmwebText.Normalize)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Take(12));
        return string.Concat(
            request.IsSeries ? "s|" : "f|",
            FilmwebText.Normalize(request.Name),
            "|",
            FilmwebText.Normalize(request.OriginalTitle),
            "|",
            request.Year?.ToString(CultureInfo.InvariantCulture) ?? "-",
            "|",
            people);
    }

    private sealed class CachingApi : IFilmwebApi
    {
        private readonly FilmwebClient _client;
        private readonly FilmwebCache _cache;
        private readonly TimeSpan _ttl;

        public CachingApi(FilmwebClient client, FilmwebCache cache, TimeSpan ttl)
        {
            _client = client;
            _cache = cache;
            _ttl = ttl;
        }

        public Task<IReadOnlyList<FilmwebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken) =>
            _client.SearchAsync(query, cancellationToken);

        public async Task<FilmwebTitle?> GetTitleAsync(int id, CancellationToken cancellationToken)
        {
            if (_cache.TryGetTitle(id, _ttl, out var cached))
            {
                return cached;
            }

            var title = await _client.GetTitleAsync(id, cancellationToken).ConfigureAwait(false);
            if (title is null)
            {
                _cache.PutMissingTitle(id);
            }
            else
            {
                _cache.PutTitle(title);
            }

            return title;
        }

        public Task<FilmwebRating?> GetRatingAsync(int id, CancellationToken cancellationToken) =>
            _client.GetRatingAsync(id, cancellationToken);

        public Task<FilmwebRating?> GetCriticsRatingAsync(int id, CancellationToken cancellationToken) =>
            _client.GetCriticsRatingAsync(id, cancellationToken);
    }
}
