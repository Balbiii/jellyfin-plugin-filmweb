using Jellyfin.Plugin.Filmweb.Configuration;
using Jellyfin.Plugin.Filmweb.Filmweb;

namespace Jellyfin.Plugin.Filmweb.Tests;

public class FilmwebLogicTests
{
    [Fact]
    public void Normalize_folds_polish_letters_articles_and_punctuation()
    {
        Assert.Equal("zolc", FilmwebText.Normalize("Żółć"));
        Assert.Equal("matrix", FilmwebText.Normalize("The Matrix"));
        Assert.True(FilmwebText.TitlesEquivalent("Spider-Man", "Spider Man"));
        Assert.False(FilmwebText.TitlesEquivalent("The Office", "The Office PL"));
        Assert.False(FilmwebText.TitlesEquivalent("Dune", "Dune: Part Two"));
    }

    [Fact]
    public void SplitTrailingYear_reads_year_from_the_title()
    {
        var (name, year) = FilmwebText.SplitTrailingYear("Incepcja (2010)", null);
        Assert.Equal("Incepcja", name);
        Assert.Equal(2010, year);
    }

    [Fact]
    public void Matcher_picks_inception_by_polish_or_original_title_and_year()
    {
        var inception = Film(500891, "Incepcja", "Inception", 2010);
        var decoy = Film(806912, "The Crack: Inception", "The Crack: Inception", 2010);
        var candidates = new[] { inception, decoy };

        Assert.Equal(500891, FilmwebMatcher.Select(Request("Incepcja", null, 2010, false), candidates)!.Id);
        Assert.Equal(500891, FilmwebMatcher.Select(Request("Inception", null, 2010, false), candidates)!.Id);
        Assert.Null(FilmwebMatcher.Select(Request("Inception", null, 1999, false), candidates));
    }

    [Fact]
    public void Matcher_uses_year_to_separate_dune_and_office()
    {
        var dune1984 = Film(1086, "Diuna", "Dune", 1984);
        var dune2021 = Film(469476, "Diuna", "Dune", 2021);
        var dunes = new[] { dune2021, dune1984 };
        Assert.Equal(1086, FilmwebMatcher.Select(Request("Dune", null, 1984, false), dunes)!.Id);
        Assert.Equal(469476, FilmwebMatcher.Select(Request("Diuna", null, 2021, false), dunes)!.Id);

        var us = Show(150481, "The Office", "The Office", 2005, 2013, "Steve Carell");
        var uk = Show(202887, "The Office", "The Office", 2001, 2003, "Ricky Gervais");
        var offices = new[] { us, uk };
        Assert.Null(FilmwebMatcher.Select(Request("The Office", null, null, true), offices));
        Assert.Equal(150481, FilmwebMatcher.Select(Request("The Office", null, 2005, true), offices)!.Id);
        Assert.Equal(150481, FilmwebMatcher.Select(Request("The Office", null, null, true, "Steve Carell"), offices)!.Id);
    }

    [Fact]
    public void Matcher_accepts_a_series_year_inside_the_run()
    {
        var thrones = Show(476848, "Gra o tron", "Game of Thrones", 2011, 2019, "Peter Dinklage");
        Assert.Equal(476848, FilmwebMatcher.Select(Request("Game of Thrones", null, 2019, true), [thrones])!.Id);
        Assert.Null(FilmwebMatcher.Select(Request("Game of Thrones", null, 1990, true), [thrones]));
    }

    [Fact]
    public void Matcher_allows_one_year_of_slack_for_a_single_title()
    {
        var movie = Film(1, "Przyklad", "Example", 2000);
        Assert.Equal(1, FilmwebMatcher.Select(Request("Example", null, 2001, false), [movie])!.Id);
        Assert.Null(FilmwebMatcher.Select(Request("Example", null, 2005, false), [movie]));
    }

    [Fact]
    public void Parser_reads_search_title_and_rating()
    {
        const string search = """
            {"searchHits":[
              {"id":500891,"type":"film","matchedTitle":"Incepcja","filmMainCast":[{"id":30,"name":"Leonardo DiCaprio"}]},
              {"id":18,"type":"world","matchedName":"Gra o tron"}
            ]}
            """;
        var hits = FilmwebParser.ParseSearch(search);
        var hit = Assert.Single(hits);
        Assert.Equal(500891, hit.Id);
        Assert.Equal("Leonardo DiCaprio", Assert.Single(hit.Cast));

        var title = FilmwebParser.ParseTitle("""
            {"id":476848,"title":"Gra o tron","originalTitle":"Game of Thrones","year":2011,"otherYear":2019,"type":"serial"}
            """);
        Assert.NotNull(title);
        Assert.Equal(2019, title.EndYear);
        Assert.Equal("serial", title.Type);

        var rating = FilmwebParser.ParseRating("""{"id":500891,"count":666510,"rate":8.24088}""");
        Assert.NotNull(rating);
        Assert.Equal(8.24088, rating.Rate);
        Assert.Equal(666510, rating.Count);
    }

    [Fact]
    public void Overview_line_is_idempotent_and_can_be_removed()
    {
        var line = OverviewNote.FormatLine(8.2f, 666510);
        var once = OverviewNote.Apply("Opis filmu.", line, true);
        var twice = OverviewNote.Apply(once, line, true);
        Assert.Equal(once, twice);
        Assert.StartsWith(OverviewNote.Marker, once, StringComparison.Ordinal);
        Assert.Contains("Opis filmu.", once, StringComparison.Ordinal);

        var removed = OverviewNote.Apply(once, null, false);
        Assert.Equal("Opis filmu.", removed);
    }

    [Fact]
    public void Applier_replaces_community_rating_and_respects_locks_and_minimum_votes()
    {
        var config = Config(RatingWriteMode.Replace, showOverview: true);
        var snapshot = new RatingSnapshot { CommunityRating = 6.1f, Overview = "Stary opis." };
        Assert.True(RatingApplier.Apply(snapshot, 500891, new FilmwebRating(8.24088, 1000), null, config));
        Assert.Equal("500891", snapshot.ProviderId);
        Assert.Equal(8.2f, snapshot.CommunityRating);
        Assert.Contains("Stary opis.", snapshot.Overview, StringComparison.Ordinal);

        var filled = new RatingSnapshot { CommunityRating = 7.1f };
        Assert.True(RatingApplier.Apply(filled, 5, new FilmwebRating(9, 100), null, Config(RatingWriteMode.FillIfEmpty, false)));
        Assert.Equal(7.1f, filled.CommunityRating);
        Assert.Equal("5", filled.ProviderId);

        var quiet = new RatingSnapshot { CommunityRating = 6f, Overview = "Zostaw." };
        Assert.True(RatingApplier.Apply(quiet, 9, new FilmwebRating(8, 3), null, Config(RatingWriteMode.Replace, true, minimumVotes: 10)));
        Assert.Equal(6f, quiet.CommunityRating);
        Assert.Equal("Zostaw.", quiet.Overview);

        var locked = new RatingSnapshot { Overview = "Zablokowany.", OverviewLocked = true };
        RatingApplier.Apply(locked, 9, new FilmwebRating(8, 100), null, Config(RatingWriteMode.Replace, true));
        Assert.Equal("Zablokowany.", locked.Overview);
    }

    [Fact]
    public void Url_uses_the_filmweb_title_year_and_id()
    {
        var url = FilmwebUrls.TitlePage(new FilmwebTitle(500891, "Incepcja", "Inception", 2010, null, "film"));
        Assert.Equal("https://www.filmweb.pl/film/Incepcja-2010-500891", url);

        var series = FilmwebUrls.TitlePage(new FilmwebTitle(476848, "Gra o tron", "Game of Thrones", 2011, 2019, "serial"));
        Assert.StartsWith("https://www.filmweb.pl/serial/", series, StringComparison.Ordinal);
        Assert.EndsWith("-2011-476848", series, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Resolver_follows_search_hits_and_refuses_an_ambiguous_title()
    {
        var api = new FakeApi();
        api.Titles[500891] = new FilmwebTitle(500891, "Incepcja", "Inception", 2010, null, "film");
        api.Searches["Incepcja"] =
        [
            new FilmwebSearchHit(500891, "film", "Incepcja", ["Leonardo DiCaprio"])
        ];

        var found = await FilmwebResolver.ResolveAsync(api, Request("Incepcja", null, 2010, false), 5, CancellationToken.None);
        Assert.True(found.Definitive);
        Assert.Equal(500891, found.FilmwebId);

        api.Titles[150481] = new FilmwebTitle(150481, "The Office", "The Office", 2005, 2013, "serial");
        api.Titles[202887] = new FilmwebTitle(202887, "The Office", "The Office", 2001, 2003, "serial");
        api.Searches["The Office"] =
        [
            new FilmwebSearchHit(150481, "serial", "The Office", []),
            new FilmwebSearchHit(202887, "serial", "The Office", [])
        ];
        var ambiguous = await FilmwebResolver.ResolveAsync(api, Request("The Office", null, null, true), 5, CancellationToken.None);
        Assert.True(ambiguous.Definitive);
        Assert.Null(ambiguous.FilmwebId);
    }

    private static MatchRequest Request(string name, string? original, int? year, bool series, params string[] people) =>
        new(name, original, year, series, people);

    private static FilmwebCandidate Film(int id, string title, string original, int year, params string[] cast) =>
        new(id, "film", title, original, year, null, cast);

    private static FilmwebCandidate Show(int id, string title, string original, int year, int end, params string[] cast) =>
        new(id, "serial", title, original, year, end, cast);

    private static EffectiveConfig Config(RatingWriteMode community, bool showOverview, int minimumVotes = 10) =>
        new(community, RatingWriteMode.Disabled, showOverview, true, true, minimumVotes, TimeSpan.FromDays(7), 0, 5);

    private sealed class FakeApi : IFilmwebApi
    {
        public Dictionary<string, IReadOnlyList<FilmwebSearchHit>> Searches { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<int, FilmwebTitle> Titles { get; } = new();

        public Task<IReadOnlyList<FilmwebSearchHit>> SearchAsync(string query, CancellationToken cancellationToken) =>
            Task.FromResult(Searches.TryGetValue(query, out var hits) ? hits : []);

        public Task<FilmwebTitle?> GetTitleAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Titles.TryGetValue(id, out var title) ? title : null);

        public Task<FilmwebRating?> GetRatingAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<FilmwebRating?>(new FilmwebRating(8, 100));

        public Task<FilmwebRating?> GetCriticsRatingAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<FilmwebRating?>(null);
    }
}
