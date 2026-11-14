using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Jellyfin.Plugin.Filmweb.Web;

/// <summary>
/// Dopisuje styl z ikoną Filmwebu do index.html klienta webowego.
/// Nic nie zmienia w plikach Jellyfina na dysku.
/// </summary>
public sealed class FilmwebWebMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FilmwebWebMiddleware> _logger;

    public FilmwebWebMiddleware(RequestDelegate next, ILogger<FilmwebWebMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsEnabled() || !IsWebIndexRequest(context.Request))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Bez kompresji i bez 304, żeby dostać pełny, czytelny HTML.
        context.Request.Headers.Remove(HeaderNames.AcceptEncoding);
        context.Request.Headers.Remove(HeaderNames.IfNoneMatch);
        context.Request.Headers.Remove(HeaderNames.IfModifiedSince);

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        buffer.Position = 0;
        var response = context.Response;
        var isHtml = response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
        var isEncoded = response.Headers.ContainsKey(HeaderNames.ContentEncoding);

        if (response.StatusCode != StatusCodes.Status200OK || !isHtml || isEncoded)
        {
            await buffer.CopyToAsync(originalBody, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        string html;
        using (var reader = new StreamReader(buffer, Encoding.UTF8, true, 4096, leaveOpen: true))
        {
            html = await reader.ReadToEndAsync(context.RequestAborted).ConfigureAwait(false);
        }

        var patched = FilmwebWebAssets.InjectInto(html, FilmwebWebAssets.StyleBlock);
        var bytes = Encoding.UTF8.GetBytes(patched);

        response.Headers.Remove(HeaderNames.ETag);
        response.Headers.Remove(HeaderNames.LastModified);
        response.Headers.CacheControl = "no-cache";
        response.ContentLength = bytes.Length;
        await originalBody.WriteAsync(bytes, context.RequestAborted).ConfigureAwait(false);

        if (ReferenceEquals(patched, html))
        {
            _logger.LogDebug("Filmweb: nie znaleziono </head> w index.html, ikona nie została dodana");
        }
    }

    internal static bool IsWebIndexRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.PathBase.Add(request.Path).Value;
        return IsWebIndexPath(path);
    }

    internal static bool IsWebIndexPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        return path.EndsWith("/web/", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/web/index.html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEnabled() => Plugin.Instance?.Configuration.ReplaceStarIcon ?? false;
}
