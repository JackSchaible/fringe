namespace FringeScraper.Services;

using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

internal static class Fetcher
{
    private static readonly HttpClient Http = new();
    private static readonly HtmlParser Parser = new();

    static Fetcher()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
    }

    public static async Task<IHtmlDocument> LoadAsync(string url)
    {
        string html = await Http.GetStringAsync(url);
        return await Parser.ParseDocumentAsync(html);
    }
}
