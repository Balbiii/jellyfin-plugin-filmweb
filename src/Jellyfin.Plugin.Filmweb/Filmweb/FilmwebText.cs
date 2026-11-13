using System.Globalization;
using System.Text;

namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebText
{
    private static readonly string[] Articles = ["the ", "a ", "an "];

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var decomposed = FoldPolish(value).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character is '\'' or '\u2019' or '`' or '-' or '\u2010' or '\u2013' or '\u2014')
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        var collapsed = string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return StripArticle(collapsed);
    }

    public static bool TitlesEquivalent(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a == b)
        {
            return true;
        }

        var compactA = a.Replace(" ", string.Empty, StringComparison.Ordinal);
        var compactB = b.Replace(" ", string.Empty, StringComparison.Ordinal);
        return compactA.Length >= 4 && compactA == compactB;
    }

    public static (string Name, int? Year) SplitTrailingYear(string? name, int? year)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return (string.Empty, year);
        }

        var trimmed = name.Trim();
        var match = System.Text.RegularExpressions.Regex.Match(
            trimmed,
            @"\s*[\(\[](?<y>\d{4})(?:\s*[-/–]\s*\d{4})?[\)\]]\s*$");
        if (!match.Success)
        {
            return (trimmed, year);
        }

        var withoutYear = trimmed[..match.Index].Trim();
        var parsed = int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture);
        return (withoutYear.Length == 0 ? trimmed : withoutYear, year ?? parsed);
    }

    private static string StripArticle(string value)
    {
        foreach (var article in Articles)
        {
            if (value.StartsWith(article, StringComparison.Ordinal) && value.Length > article.Length)
            {
                return value[article.Length..];
            }
        }

        return value;
    }

    private static string FoldPolish(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(character switch
            {
                'ą' or 'Ą' => 'a',
                'ć' or 'Ć' => 'c',
                'ę' or 'Ę' => 'e',
                'ł' or 'Ł' => 'l',
                'ń' or 'Ń' => 'n',
                'ó' or 'Ó' => 'o',
                'ś' or 'Ś' => 's',
                'ź' or 'Ź' => 'z',
                'ż' or 'Ż' => 'z',
                _ => character
            });
        }

        return builder.ToString();
    }
}
