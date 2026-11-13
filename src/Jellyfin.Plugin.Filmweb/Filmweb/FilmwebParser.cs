using System.Text.Json;

namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebParser
{
    public static IReadOnlyList<FilmwebSearchHit> ParseSearch(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("searchHits", out var hits) || hits.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<FilmwebSearchHit>();
        foreach (var hit in hits.EnumerateArray())
        {
            if (!TryGetInt(hit, "id", out var id) || id <= 0)
            {
                continue;
            }

            if (!hit.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var type = typeElement.GetString();
            if (type is not ("film" or "serial"))
            {
                continue;
            }

            var title = hit.TryGetProperty("matchedTitle", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
                ? titleElement.GetString() ?? string.Empty
                : string.Empty;

            results.Add(new FilmwebSearchHit(id, type, title, ReadCast(hit)));
        }

        return results;
    }

    public static FilmwebTitle? ParseTitle(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!TryGetInt(root, "id", out var id) || id <= 0)
        {
            return null;
        }

        return new FilmwebTitle(
            id,
            ReadString(root, "title"),
            ReadString(root, "originalTitle"),
            TryGetInt(root, "year", out var year) ? year : null,
            TryGetInt(root, "otherYear", out var endYear) ? endYear : null,
            ReadString(root, "type"));
    }

    public static FilmwebRating? ParseRating(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("rate", out var rateElement) || !TryGetDouble(rateElement, out var rate))
        {
            return null;
        }

        long count = 0;
        if (root.TryGetProperty("count", out var countElement))
        {
            if (!countElement.TryGetInt64(out count))
            {
                if (TryGetDouble(countElement, out var countDouble))
                {
                    count = (long)countDouble;
                }
            }
        }

        return new FilmwebRating(rate, count);
    }

    private static IReadOnlyList<string> ReadCast(JsonElement hit)
    {
        if (!hit.TryGetProperty("filmMainCast", out var cast) || cast.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var names = new List<string>();
        foreach (var person in cast.EnumerateArray())
        {
            var name = ReadString(person, "name");
            if (!string.IsNullOrWhiteSpace(name))
            {
                names.Add(name);
            }
        }

        return names;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryGetInt(JsonElement element, string name, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.TryGetInt32(out value))
        {
            return true;
        }

        if (!TryGetDouble(property, out var number) || number < int.MinValue || number > int.MaxValue)
        {
            return false;
        }

        var truncated = (int)number;
        if (truncated != number)
        {
            return false;
        }

        value = truncated;
        return true;
    }

    private static bool TryGetDouble(JsonElement element, out double value)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value))
        {
            return true;
        }

        if (element.ValueKind == JsonValueKind.String &&
            double.TryParse(element.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}
