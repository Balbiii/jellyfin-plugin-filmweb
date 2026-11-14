using Jellyfin.Plugin.Filmweb.Filmweb;
using Jellyfin.Plugin.Filmweb.Services;
using Jellyfin.Plugin.Filmweb.Web;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.Filmweb;

public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddHttpClient(FilmwebClient.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://www.filmweb.pl/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Jellyfin-Filmweb-Plugin/1.0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
            client.DefaultRequestHeaders.TryAddWithoutValidation("x-locale", "pl_PL");
        });

        serviceCollection.AddSingleton<FilmwebClient>();
        serviceCollection.AddSingleton<FilmwebCache>();
        serviceCollection.AddSingleton<FilmwebRatingService>();

        // Ikona Filmwebu zamiast gwiazdki w kliencie webowym.
        serviceCollection.AddTransient<IStartupFilter, FilmwebWebStartupFilter>();
    }
}
