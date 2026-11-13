namespace Jellyfin.Plugin.Filmweb.Filmweb;

public static class FilmwebNames
{
    public const string ProviderName = "Filmweb";

    public const string Key = "Filmweb";
}

public sealed class FilmwebUnavailableException : Exception
{
    public FilmwebUnavailableException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public sealed record FilmwebSearchHit(int Id, string Type, string MatchedTitle, IReadOnlyList<string> Cast);

public sealed record FilmwebTitle(
    int Id,
    string? Title,
    string? OriginalTitle,
    int? Year,
    int? EndYear,
    string? Type);

public sealed record FilmwebRating(double Rate, long Count);

public sealed record MatchRequest(
    string Name,
    string? OriginalTitle,
    int? Year,
    bool IsSeries,
    IReadOnlyList<string> People);

public sealed record FilmwebCandidate(
    int Id,
    string Type,
    string? Title,
    string? OriginalTitle,
    int? Year,
    int? EndYear,
    IReadOnlyList<string> Cast);

public readonly record struct ResolveResult(int? FilmwebId, bool Definitive);

public interface IFilmwebApi
{
    Task<IReadOnlyList<FilmwebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken);

    Task<FilmwebTitle?> GetTitleAsync(int id, CancellationToken cancellationToken);

    Task<FilmwebRating?> GetRatingAsync(int id, CancellationToken cancellationToken);

    Task<FilmwebRating?> GetCriticsRatingAsync(int id, CancellationToken cancellationToken);
}
