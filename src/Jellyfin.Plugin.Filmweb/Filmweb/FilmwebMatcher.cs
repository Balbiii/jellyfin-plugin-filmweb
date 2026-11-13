namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebMatcher
{
    public static FilmwebCandidate? Select(MatchRequest request, IReadOnlyList<FilmwebCandidate> candidates)
    {
        var expectedType = request.IsSeries ? "serial" : "film";
        var scored = new List<Scored>();
        foreach (var candidate in candidates)
        {
            if (!string.Equals(candidate.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TitleMatches(request, candidate))
            {
                continue;
            }

            if (!TryYearPoints(request, candidate, out var yearPoints))
            {
                continue;
            }

            scored.Add(new Scored(candidate, 100 + yearPoints, CastOverlaps(request, candidate)));
        }

        if (scored.Count == 0)
        {
            return null;
        }

        var best = scored.Max(item => item.Points);
        var top = scored.Where(item => item.Points == best).ToList();
        if (top.Count == 1)
        {
            return top[0].Candidate;
        }

        var sameId = top.Select(item => item.Candidate.Id).Distinct().ToList();
        if (sameId.Count == 1)
        {
            return top[0].Candidate;
        }

        var withCast = top.Where(item => item.CastOverlap).ToList();
        return withCast.Count == 1 ? withCast[0].Candidate : null;
    }

    private static bool TitleMatches(MatchRequest request, FilmwebCandidate candidate)
    {
        return FilmwebText.TitlesEquivalent(request.Name, candidate.Title)
            || FilmwebText.TitlesEquivalent(request.Name, candidate.OriginalTitle)
            || FilmwebText.TitlesEquivalent(request.OriginalTitle, candidate.Title)
            || FilmwebText.TitlesEquivalent(request.OriginalTitle, candidate.OriginalTitle);
    }

    private static bool TryYearPoints(MatchRequest request, FilmwebCandidate candidate, out int points)
    {
        points = 0;
        if (request.Year is not int wanted || candidate.Year is not int start)
        {
            return true;
        }

        if (!request.IsSeries)
        {
            var difference = Math.Abs(wanted - start);
            if (difference == 0)
            {
                points = 50;
                return true;
            }

            if (difference == 1)
            {
                points = 30;
                return true;
            }

            return false;
        }

        var end = candidate.EndYear is int other && other >= start ? other : start;
        if (wanted >= start && wanted <= end)
        {
            points = 50;
            return true;
        }

        if (Math.Abs(wanted - start) <= 1)
        {
            points = 30;
            return true;
        }

        return false;
    }

    private static bool CastOverlaps(MatchRequest request, FilmwebCandidate candidate)
    {
        if (request.People.Count == 0 || candidate.Cast.Count == 0)
        {
            return false;
        }

        var people = request.People.Select(FilmwebText.Normalize).Where(name => name.Length >= 4).ToHashSet(StringComparer.Ordinal);
        foreach (var name in candidate.Cast)
        {
            var normalized = FilmwebText.Normalize(name);
            if (normalized.Length >= 4 && people.Contains(normalized))
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct Scored(FilmwebCandidate Candidate, int Points, bool CastOverlap);
}
