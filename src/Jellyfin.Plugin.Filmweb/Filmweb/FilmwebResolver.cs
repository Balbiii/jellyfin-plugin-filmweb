namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebResolver
{
    public static async Task<ResolveResult> ResolveAsync(
        IFilmwebApi api,
        MatchRequest request,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        var queries = new List<string>();
        AddQuery(queries, request.Name);
        AddQuery(queries, request.OriginalTitle);
        if (queries.Count == 0)
        {
            return new ResolveResult(null, true);
        }

        var expectedType = request.IsSeries ? "serial" : "film";
        var hits = new List<FilmwebSearchHit>();
        var seen = new HashSet<int>();
        var anySuccess = false;
        var anyFailure = false;
        var limit = Math.Clamp(maxCandidates, 1, 10);

        foreach (var query in queries)
        {
            IReadOnlyList<FilmwebSearchHit> found;
            try
            {
                found = await api.SearchAsync(query, cancellationToken).ConfigureAwait(false);
                anySuccess = true;
            }
            catch (FilmwebUnavailableException)
            {
                anyFailure = true;
                continue;
            }

            foreach (var hit in found)
            {
                if (!string.Equals(hit.Type, expectedType, StringComparison.OrdinalIgnoreCase) || !seen.Add(hit.Id))
                {
                    continue;
                }

                hits.Add(hit);
                if (hits.Count >= limit)
                {
                    break;
                }
            }

            if (hits.Count >= limit)
            {
                break;
            }
        }

        if (!anySuccess)
        {
            return new ResolveResult(null, false);
        }

        var candidates = new List<FilmwebCandidate>(hits.Count);
        foreach (var hit in hits)
        {
            FilmwebTitle? title;
            try
            {
                title = await api.GetTitleAsync(hit.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (FilmwebUnavailableException)
            {
                anyFailure = true;
                continue;
            }

            if (title is null || !string.Equals(title.Type, expectedType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            candidates.Add(new FilmwebCandidate(
                title.Id,
                title.Type ?? expectedType,
                title.Title,
                title.OriginalTitle,
                title.Year,
                title.EndYear,
                hit.Cast));
        }

        var match = FilmwebMatcher.Select(request, candidates);
        if (match is not null)
        {
            return new ResolveResult(match.Id, true);
        }

        return new ResolveResult(null, !anyFailure);
    }

    private static void AddQuery(List<string> queries, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed.Length > 120)
        {
            return;
        }

        if (queries.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        queries.Add(trimmed);
    }
}
