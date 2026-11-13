using Jellyfin.Data.Enums;
using Jellyfin.Plugin.Filmweb.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Filmweb.Tasks;

public sealed class UpdateFilmwebRatingsTask : IScheduledTask
{
    private readonly FilmwebRatingService _service;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<UpdateFilmwebRatingsTask> _logger;

    public UpdateFilmwebRatingsTask(
        FilmwebRatingService service,
        ILibraryManager libraryManager,
        ILogger<UpdateFilmwebRatingsTask> logger)
    {
        _service = service;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public string Name => "Aktualizuj oceny Filmweb";

    public string Key => "FilmwebRatings";

    public string Description => "Pobiera oceny użytkowników Filmweb.pl dla filmów i seriali i zapisuje je w bibliotece.";

    public string Category => "Filmweb";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        yield return new TaskTriggerInfo
        {
            Type = TaskTriggerInfoType.DailyTrigger,
            TimeOfDayTicks = TimeSpan.FromHours(4.5).Ticks
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Configuration.EffectiveConfig.From(Plugin.Instance?.Configuration);
        var kinds = new List<BaseItemKind>();
        if (config.EnableMovies)
        {
            kinds.Add(BaseItemKind.Movie);
        }

        if (config.EnableSeries)
        {
            kinds.Add(BaseItemKind.Series);
        }

        if (kinds.Count == 0)
        {
            progress.Report(100);
            return;
        }

        var items = _libraryManager.GetItemList(new InternalItemsQuery
        {
            IncludeItemTypes = kinds.ToArray(),
            Recursive = true,
            IsVirtualItem = false
        });

        var updated = 0;
        var unchanged = 0;
        var missed = 0;
        var failed = 0;
        var total = items.Count;
        for (var index = 0; index < total; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[index];
            if (string.IsNullOrWhiteSpace(item.Name))
            {
                continue;
            }

            var status = await _service.ApplyAsync(item, cancellationToken).ConfigureAwait(false);
            switch (status)
            {
                case ApplyStatus.Updated:
                    await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
                    updated++;
                    break;
                case ApplyStatus.Unchanged:
                    unchanged++;
                    break;
                case ApplyStatus.NotMatched:
                    missed++;
                    break;
                case ApplyStatus.Unavailable:
                case ApplyStatus.Failed:
                    failed++;
                    if (_service.IsCircuitOpen)
                    {
                        _logger.LogWarning("Filmweb nie odpowiada, przerywam zadanie.");
                        progress.Report(100);
                        return;
                    }

                    break;
            }

            progress.Report(total == 0 ? 100 : 100d * (index + 1) / total);
        }

        _logger.LogInformation(
            "Filmweb: zaktualizowano {Updated}, bez zmian {Unchanged}, bez dopasowania {Missed}, błędy {Failed}",
            updated,
            unchanged,
            missed,
            failed);
        progress.Report(100);
    }
}
