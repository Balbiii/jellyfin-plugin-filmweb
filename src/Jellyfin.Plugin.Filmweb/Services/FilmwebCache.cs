using System.Text.Json;
using Jellyfin.Plugin.Filmweb.Filmweb;

namespace Jellyfin.Plugin.Filmweb.Services;

public sealed class FilmwebCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly object _gate = new();
    private CacheDocument _document = new();
    private bool _loaded;

    public bool TryGetTitle(int id, TimeSpan ttl, out FilmwebTitle? title)
    {
        lock (_gate)
        {
            EnsureLoaded();
            if (_document.Titles.TryGetValue(Key(id), out var record) && IsFresh(record.FetchedAt, ttl))
            {
                title = record.Missing
                    ? null
                    : new FilmwebTitle(record.Id, record.Title, record.OriginalTitle, record.Year, record.EndYear, record.Type);
                return true;
            }
        }

        title = null;
        return false;
    }

    public void PutTitle(FilmwebTitle title)
    {
        lock (_gate)
        {
            EnsureLoaded();
            _document.Titles[Key(title.Id)] = new TitleRecord
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Id = title.Id,
                Title = title.Title,
                OriginalTitle = title.OriginalTitle,
                Year = title.Year,
                EndYear = title.EndYear,
                Type = title.Type
            };
            SaveLocked();
        }
    }

    public void PutMissingTitle(int id)
    {
        lock (_gate)
        {
            EnsureLoaded();
            _document.Titles[Key(id)] = new TitleRecord
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Missing = true,
                Id = id
            };
            SaveLocked();
        }
    }

    public bool TryGetRating(int id, bool critics, TimeSpan ttl, out FilmwebRating? rating)
    {
        lock (_gate)
        {
            EnsureLoaded();
            var map = critics ? _document.Critics : _document.Ratings;
            if (map.TryGetValue(Key(id), out var record) && IsFresh(record.FetchedAt, ttl))
            {
                rating = record.Missing ? null : new FilmwebRating(record.Rate, record.Count);
                return true;
            }
        }

        rating = null;
        return false;
    }

    public void PutRating(int id, bool critics, FilmwebRating? rating)
    {
        lock (_gate)
        {
            EnsureLoaded();
            var map = critics ? _document.Critics : _document.Ratings;
            map[Key(id)] = rating is null
                ? new RatingRecord { FetchedAt = DateTimeOffset.UtcNow, Missing = true }
                : new RatingRecord { FetchedAt = DateTimeOffset.UtcNow, Rate = rating.Rate, Count = rating.Count };
            SaveLocked();
        }
    }

    public bool TryGetMatch(string key, TimeSpan ttl, out int? filmwebId)
    {
        lock (_gate)
        {
            EnsureLoaded();
            if (_document.Matches.TryGetValue(key, out var record) && IsFresh(record.FetchedAt, ttl))
            {
                filmwebId = record.FilmwebId;
                return true;
            }
        }

        filmwebId = null;
        return false;
    }

    public void PutMatch(string key, int? filmwebId)
    {
        lock (_gate)
        {
            EnsureLoaded();
            _document.Matches[key] = new MatchRecord
            {
                FetchedAt = DateTimeOffset.UtcNow,
                FilmwebId = filmwebId
            };
            SaveLocked();
        }
    }

    private static bool IsFresh(DateTimeOffset fetchedAt, TimeSpan ttl) =>
        DateTimeOffset.UtcNow - fetchedAt < ttl;

    private static string Key(int id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var path = CachePath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<CacheDocument>(json, JsonOptions);
            if (loaded is { Version: 1 })
            {
                loaded.Titles ??= new Dictionary<string, TitleRecord>();
                loaded.Ratings ??= new Dictionary<string, RatingRecord>();
                loaded.Critics ??= new Dictionary<string, RatingRecord>();
                loaded.Matches ??= new Dictionary<string, MatchRecord>();
                _document = loaded;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _document = new CacheDocument();
        }
    }

    private void SaveLocked()
    {
        var path = CachePath();
        var folder = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var temp = path + ".tmp";
        var json = JsonSerializer.Serialize(_document, JsonOptions);
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    private static string CachePath()
    {
        var folder = Plugin.Instance?.DataFolderPath;
        if (string.IsNullOrWhiteSpace(folder))
        {
            folder = Path.Combine(Path.GetTempPath(), "jellyfin-filmweb");
        }

        return Path.Combine(folder, "cache.json");
    }

    private sealed class CacheDocument
    {
        public int Version { get; set; } = 1;

        public Dictionary<string, TitleRecord> Titles { get; set; } = new();

        public Dictionary<string, RatingRecord> Ratings { get; set; } = new();

        public Dictionary<string, RatingRecord> Critics { get; set; } = new();

        public Dictionary<string, MatchRecord> Matches { get; set; } = new();
    }

    private sealed class TitleRecord
    {
        public DateTimeOffset FetchedAt { get; set; }

        public bool Missing { get; set; }

        public int Id { get; set; }

        public string? Title { get; set; }

        public string? OriginalTitle { get; set; }

        public int? Year { get; set; }

        public int? EndYear { get; set; }

        public string? Type { get; set; }
    }

    private sealed class RatingRecord
    {
        public DateTimeOffset FetchedAt { get; set; }

        public bool Missing { get; set; }

        public double Rate { get; set; }

        public long Count { get; set; }
    }

    private sealed class MatchRecord
    {
        public DateTimeOffset FetchedAt { get; set; }

        public int? FilmwebId { get; set; }
    }
}
