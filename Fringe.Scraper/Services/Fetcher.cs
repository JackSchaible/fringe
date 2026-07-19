using System.Net;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace FringeScraper.Services;

/// <summary>Default HTTP-backed implementation of <see cref="IFetcher"/>.</summary>
internal sealed class Fetcher : IFetcher
{
    private static readonly HttpClient http = new(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        CheckCertificateRevocationList = true,
    });
    private static readonly HtmlParser parser = new();

    static Fetcher()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        http.DefaultRequestHeaders.Referrer = new Uri("https://tickets.fringetheatre.ca/events/");
    }

    /// <inheritdoc/>
    public async Task<IHtmlDocument> LoadAsync(Uri url)
    {
        using HttpResponseMessage response = await http.GetAsync(url).ConfigureAwait(false);
        string html = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string headers = string.Join(", ", response.Headers.Concat(response.Content.Headers)
                .Select(h => $"{h.Key}={string.Join('|', h.Value)}"));
            string snippet = html.Length > 500 ? html[..500] : html;
            Console.WriteLine($"⚠️ {(int)response.StatusCode} {response.ReasonPhrase} for {url}");
            Console.WriteLine($"   Headers: {headers}");
            Console.WriteLine($"   Body: {snippet}");
            _ = response.EnsureSuccessStatusCode();
        }

        return await parser.ParseDocumentAsync(html).ConfigureAwait(false);
    }
}
