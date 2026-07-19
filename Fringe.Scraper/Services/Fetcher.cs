using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace FringeScraper.Services;

/// <summary>Default HTTP-backed implementation of <see cref="IFetcher"/>.</summary>
internal sealed class Fetcher : IFetcher
{
    private static readonly HttpClient http = new();
    private static readonly HtmlParser parser = new();

    static Fetcher()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    }

    /// <inheritdoc/>
    public async Task<IHtmlDocument> LoadAsync(Uri url)
    {
        string html = await http.GetStringAsync(url).ConfigureAwait(false);
        return await parser.ParseDocumentAsync(html).ConfigureAwait(false);
    }
}
