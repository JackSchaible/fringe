using AngleSharp;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace Fringe.Scraper.Tests;

/// <summary>
/// Utility for creating real AngleSharp IHtmlDocument instances from HTML strings.
/// Used by all scraper tests to produce parsed documents without making real HTTP calls.
/// </summary>
internal static class HtmlHelper
{
    internal static async Task<IHtmlDocument> ParseAsync(string html)
    {
        IBrowsingContext context = BrowsingContext.New(Configuration.Default);
        IHtmlParser parser = context.GetService<IHtmlParser>()!;
        return await parser.ParseDocumentAsync(html).ConfigureAwait(false);
    }

    internal static IHtmlDocument Parse(string html)
    {
        return ParseAsync(html).GetAwaiter().GetResult();
    }
}
