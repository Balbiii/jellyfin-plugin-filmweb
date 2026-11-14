using System.Reflection;
using System.Text;

namespace Jellyfin.Plugin.Filmweb.Web;

/// <summary>
/// Styl z ikoną Filmwebu wstrzykiwany do klienta webowego Jellyfina.
/// </summary>
internal static class FilmwebWebAssets
{
    public const string StyleElementId = "filmweb-rating-icon";

    private const string IconPlaceholder = "{{FILMWEB_ICON}}";

    private static readonly Lazy<string> StyleBlockValue = new(BuildStyleBlock);

    public static string StyleBlock => StyleBlockValue.Value;

    public static string IconSvg => ReadResource("filmweb-icon.svg");

    public static string IconDataUri =>
        "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes(IconSvg.Trim()));

    /// <summary>
    /// Dokleja blok stylu przed &lt;/head&gt;. Zwraca oryginał, gdy styl już jest albo brakuje &lt;/head&gt;.
    /// </summary>
    public static string InjectInto(string html, string styleBlock)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(styleBlock);

        if (html.Contains("id=\"" + StyleElementId + "\"", StringComparison.Ordinal))
        {
            return html;
        }

        var headEnd = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headEnd < 0)
        {
            return html;
        }

        return html.Insert(headEnd, styleBlock);
    }

    private static string BuildStyleBlock()
    {
        var css = ReadResource("filmweb-rating.css").Replace(IconPlaceholder, IconDataUri, StringComparison.Ordinal);
        return "<style id=\"" + StyleElementId + "\">" + css + "</style>";
    }

    private static string ReadResource(string fileName)
    {
        var name = typeof(FilmwebWebAssets).Namespace + "." + fileName;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Brak zasobu " + name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
