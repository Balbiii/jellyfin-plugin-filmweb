using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Filmweb.Configuration;

public enum RatingWriteMode
{
    Disabled = 0,
    FillIfEmpty = 1,
    Replace = 2
}

public class PluginConfiguration : BasePluginConfiguration
{
    public RatingWriteMode CommunityRating { get; set; } = RatingWriteMode.Replace;

    public RatingWriteMode CriticRating { get; set; } = RatingWriteMode.Disabled;

    public bool ShowInOverview { get; set; }

    public bool ReplaceStarIcon { get; set; } = true;

    public bool EnableMovies { get; set; } = true;

    public bool EnableSeries { get; set; } = true;

    public int MinimumVotes { get; set; } = 10;

    public int CacheDays { get; set; } = 7;

    public int RequestDelayMs { get; set; } = 300;

    public int MaxCandidates { get; set; } = 5;
}

public readonly record struct EffectiveConfig(
    RatingWriteMode CommunityRating,
    RatingWriteMode CriticRating,
    bool ShowInOverview,
    bool EnableMovies,
    bool EnableSeries,
    int MinimumVotes,
    TimeSpan CacheTtl,
    int RequestDelayMs,
    int MaxCandidates)
{
    public static EffectiveConfig From(PluginConfiguration? source)
    {
        source ??= new PluginConfiguration();
        var days = Math.Clamp(source.CacheDays, 1, 90);
        return new EffectiveConfig(
            source.CommunityRating,
            source.CriticRating,
            source.ShowInOverview,
            source.EnableMovies,
            source.EnableSeries,
            Math.Clamp(source.MinimumVotes, 0, 10_000_000),
            TimeSpan.FromDays(days),
            Math.Clamp(source.RequestDelayMs, 0, 10_000),
            Math.Clamp(source.MaxCandidates, 1, 10));
    }
}
