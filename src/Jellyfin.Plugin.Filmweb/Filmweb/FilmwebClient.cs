using System.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Filmweb.Filmweb;

public sealed class FilmwebClient : IFilmwebApi
{
    public const string HttpClientName = "Filmweb";

    private readonly HttpClient _http;
    private readonly ILogger<FilmwebClient> _logger;
    private readonly Func<int> _delayMilliseconds;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _nextRequest = DateTimeOffset.MinValue;
    private DateTimeOffset _openUntil = DateTimeOffset.MinValue;
    private int _consecutiveFailures;

    public FilmwebClient(IHttpClientFactory httpClientFactory, ILogger<FilmwebClient> logger)
        : this(httpClientFactory.CreateClient(HttpClientName), logger, ReadConfiguredDelay)
    {
    }

    internal FilmwebClient(HttpClient http, ILogger<FilmwebClient> logger)
        : this(http, logger, static () => 0)
    {
    }

    private FilmwebClient(HttpClient http, ILogger<FilmwebClient> logger, Func<int> delayMilliseconds)
    {
        _http = http;
        _logger = logger;
        _delayMilliseconds = delayMilliseconds;
    }

    public bool IsCircuitOpen => DateTimeOffset.UtcNow < _openUntil;

    public Task<IReadOnlyList<FilmwebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var path = "live/search?query=" + Uri.EscapeDataString(query);
        return SendAsync(path, FilmwebParser.ParseSearch, static () => (IReadOnlyList<FilmwebSearchHit>)[], cancellationToken);
    }

    public Task<FilmwebTitle?> GetTitleAsync(int id, CancellationToken cancellationToken) =>
        SendAsync("title/" + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + "/info", FilmwebParser.ParseTitle, static () => (FilmwebTitle?)null, cancellationToken);

    public Task<FilmwebRating?> GetRatingAsync(int id, CancellationToken cancellationToken) =>
        SendAsync(FilmPath(id, "rating"), FilmwebParser.ParseRating, static () => (FilmwebRating?)null, cancellationToken);

    public Task<FilmwebRating?> GetCriticsRatingAsync(int id, CancellationToken cancellationToken) =>
        SendAsync(FilmPath(id, "critics/rating"), FilmwebParser.ParseRating, static () => (FilmwebRating?)null, cancellationToken);

    private static string FilmPath(int id, string resource) =>
        string.Concat("film/", id.ToString(System.Globalization.CultureInfo.InvariantCulture), "/", resource);

    private async Task<T> SendAsync<T>(string path, Func<string, T> parse, Func<T> ifMissing, CancellationToken cancellationToken)
    {
        var json = await GetStringAsync(path, cancellationToken).ConfigureAwait(false);
        if (json is null)
        {
            return ifMissing();
        }

        try
        {
            return parse(json);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Filmweb zwrócił nieczytelną odpowiedź dla {Path}", path);
            throw new FilmwebUnavailableException("Nieoczekiwana odpowiedź Filmweb.", ex);
        }
    }

    private async Task<string?> GetStringAsync(string path, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (DateTimeOffset.UtcNow < _openUntil)
            {
                throw new FilmwebUnavailableException("Filmweb jest chwilowo pomijany po serii błędów.");
            }

            var wait = _nextRequest - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            Exception? last = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using var response = await _http.GetAsync(path, cancellationToken).ConfigureAwait(false);
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        MarkSuccess();
                        return null;
                    }

                    if ((int)response.StatusCode == 429 || (int)response.StatusCode >= 500)
                    {
                        var retry = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMilliseconds(800 * attempt);
                        _logger.LogWarning("Filmweb odpowiedział {Status} dla {Path}, ponawiam", (int)response.StatusCode, path);
                        await Task.Delay(retry, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    MarkSuccess();
                    var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    ArmDelay();
                    return body;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
                {
                    last = ex;
                    _logger.LogWarning(ex, "Błąd połączenia z Filmweb ({Path}), próba {Attempt}", path, attempt);
                    await Task.Delay(TimeSpan.FromMilliseconds(800 * attempt), cancellationToken).ConfigureAwait(false);
                }
            }

            MarkFailure();
            throw new FilmwebUnavailableException("Nie udało się pobrać danych z Filmweb.", last);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void MarkSuccess()
    {
        _consecutiveFailures = 0;
        _openUntil = DateTimeOffset.MinValue;
        ArmDelay();
    }

    private void MarkFailure()
    {
        _consecutiveFailures++;
        ArmDelay();
        if (_consecutiveFailures >= 5)
        {
            _openUntil = DateTimeOffset.UtcNow.AddMinutes(15);
            _logger.LogError("Filmweb nie odpowiada. Kolejne zapytania wstrzymane na 15 minut.");
        }
    }

    private void ArmDelay()
    {
        var delay = Math.Clamp(_delayMilliseconds(), 0, 10_000);
        _nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(delay);
    }

    private static int ReadConfiguredDelay() => Plugin.Instance?.Configuration.RequestDelayMs ?? 0;
}
