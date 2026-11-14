using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Jellyfin.Plugin.Filmweb.Web;

/// <summary>
/// Wpina <see cref="FilmwebWebMiddleware"/> na początku potoku HTTP Jellyfina.
/// </summary>
public sealed class FilmwebWebStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<FilmwebWebMiddleware>();
            next(app);
        };
    }
}
