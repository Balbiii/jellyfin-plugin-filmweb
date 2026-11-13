using System.Globalization;
using Jellyfin.Plugin.Filmweb.Configuration;

namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebUrls
{
    public static string TitlePage(FilmwebTitle title)
    {
        var kind = string.Equals(title.Type, "serial", StringComparison.OrdinalIgnoreCase) ? "serial" : "film";
        var name = string.IsNullOrWhiteSpace(title.Title) ? "tytul" : title.Title;
        var encoded = Uri.EscapeDataString(name);
        var id = title.Id.ToString(CultureInfo.InvariantCulture);
        var slug = title.Year is int year
            ? string.Concat(encoded, "-", year.ToString(CultureInfo.InvariantCulture), "-", id)
            : string.Concat(encoded, "-", id);
        return string.Concat("https://www.filmweb.pl/", kind, "/", slug);
    }
}

public static class OverviewNote
{
    public const string Marker = "«Filmweb»";

    public static string? Apply(string? overview, string? line, bool enabled)
    {
        var stripped = Strip(overview);
        if (!enabled || string.IsNullOrWhiteSpace(line))
        {
            return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
        }

        return string.IsNullOrWhiteSpace(stripped) ? line : line + "\n\n" + stripped;
    }

    public static string? Strip(string? overview)
    {
        if (string.IsNullOrEmpty(overview))
        {
            return overview;
        }

        var trimmed = overview.TrimStart();
        if (!trimmed.StartsWith(Marker, StringComparison.Ordinal))
        {
            return overview;
        }

        var newlineSeparator = IndexOfSeparator(trimmed, "\r\n\r\n", 4);
        if (newlineSeparator < 0)
        {
            newlineSeparator = IndexOfSeparator(trimmed, "\n\n", 2);
        }

        if (newlineSeparator < 0)
        {
            return null;
        }

        var rest = trimmed[newlineSeparator..].TrimStart();
        return rest.Length == 0 ? null : rest;
    }

    public static string FormatLine(float rating, long votes)
    {
        var culture = Polish();
        return string.Concat(
            Marker,
            " ",
            rating.ToString("0.0", culture),
            "/10 (",
            votes.ToString("N0", culture),
            " ocen)");
    }

    private static int IndexOfSeparator(string text, string separator, int length)
    {
        var index = text.IndexOf(separator, StringComparison.Ordinal);
        return index < 0 ? -1 : index + length;
    }

    private static CultureInfo Polish()
    {
        try
        {
            return CultureInfo.GetCultureInfo("pl-PL");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}

public sealed class RatingSnapshot
{
    public string? ProviderId { get; set; }

    public float? CommunityRating { get; set; }

    public float? CriticRating { get; set; }

    public string? Overview { get; set; }

    public bool OverviewLocked { get; set; }
}

public static class RatingApplier
{
    public static bool Apply(
        RatingSnapshot snapshot,
        int filmwebId,
        FilmwebRating? userRating,
        FilmwebRating? critics,
        EffectiveConfig config)
    {
        var changed = false;
        var id = filmwebId.ToString(CultureInfo.InvariantCulture);
        if (!string.Equals(snapshot.ProviderId, id, StringComparison.Ordinal))
        {
            snapshot.ProviderId = id;
            changed = true;
        }

        var usableUser = userRating is not null && userRating.Rate > 0 && userRating.Count >= config.MinimumVotes;
        float? community = usableUser ? RoundCommunity(userRating!.Rate) : null;
        if (community is float communityValue && ShouldWrite(config.CommunityRating, snapshot.CommunityRating))
        {
            if (snapshot.CommunityRating is not float current || Math.Abs(current - communityValue) > 0.049f)
            {
                snapshot.CommunityRating = communityValue;
                changed = true;
            }
        }

        var usableCritics = critics is not null && critics.Rate > 0 && critics.Count > 0;
        if (usableCritics && ShouldWrite(config.CriticRating, snapshot.CriticRating))
        {
            var criticValue = ToCriticPercent(critics!.Rate);
            if (snapshot.CriticRating is not float current || Math.Abs(current - criticValue) > 0.49f)
            {
                snapshot.CriticRating = criticValue;
                changed = true;
            }
        }

        if (!snapshot.OverviewLocked)
        {
            var line = community is float shown && config.ShowInOverview
                ? OverviewNote.FormatLine(shown, userRating!.Count)
                : null;
            var overview = OverviewNote.Apply(snapshot.Overview, line, config.ShowInOverview && line is not null);
            if (!SameText(snapshot.Overview, overview))
            {
                snapshot.Overview = overview;
                changed = true;
            }
        }

        return changed;
    }

    public static float RoundCommunity(double rate) =>
        (float)Math.Round(rate, 1, MidpointRounding.AwayFromZero);

    public static float ToCriticPercent(double rate) =>
        (float)Math.Round(rate * 10d, 0, MidpointRounding.AwayFromZero);

    private static bool ShouldWrite(RatingWriteMode mode, float? current) => mode switch
    {
        RatingWriteMode.Replace => true,
        RatingWriteMode.FillIfEmpty => current is null,
        _ => false
    };

    private static bool SameText(string? left, string? right) =>
        string.IsNullOrEmpty(left) ? string.IsNullOrEmpty(right) : left == right;
}
