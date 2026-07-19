using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests.Services;

public sealed class IndexScraperTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal Fringe-style index page with one or more show cards.
    /// Each card entry is a (href, anchorText) pair. Pass null href to produce a
    /// card that has no card-footer anchor at all.
    /// </summary>
    private static string BuildIndexHtml(IEnumerable<string?> hrefs)
    {
        string cards = string.Join("\n", hrefs.Select(href =>
        {
            string footer = href is null
                ? "<div class=\"card-footer\"></div>"
                : $"<div class=\"card-footer\"><a href=\"{href}\">Buy</a></div>";

            return $"<div class=\"card text-left\"><div class=\"card-body\">body</div>{footer}</div>";
        }));

        return $"<html><body>{cards}</body></html>";
    }

    private static IFetcher FetcherFor(string html)
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .Returns(HtmlHelper.ParseAsync(html));
        return mock.Object;
    }

    // ── happy-path tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeIdsAsyncSingleValidCardReturnsSingleId()
    {
        string html = BuildIndexHtml(["/event/601:12345"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        _ = Assert.Single(ids);
        Assert.Equal(12345, ids[0]);
    }

    [Fact]
    public async Task ScrapeIdsAsyncMultipleCardsReturnsAllIds()
    {
        string html = BuildIndexHtml(["/event/601:11111", "/event/601:22222", "/event/601:33333"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        Assert.Equal(3, ids.Count);
        Assert.Contains(11111, ids);
        Assert.Contains(22222, ids);
        Assert.Contains(33333, ids);
    }

    [Fact]
    public async Task ScrapeIdsAsyncDuplicateCardsDeduplicates()
    {
        // The live Fringe site renders each card twice — duplicates must be removed
        string html = BuildIndexHtml(["/event/601:11111", "/event/601:22222", "/event/601:11111"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        Assert.Equal(2, ids.Count);
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public async Task ScrapeIdsAsyncHrefWithQueryStringStillExtractsId()
    {
        string html = BuildIndexHtml(["/event/601:99999?ref=homepage"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        _ = Assert.Single(ids);
        Assert.Equal(99999, ids[0]);
    }

    // ── error cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeIdsAsyncNoCardsThrowsExceptionWithMessage()
    {
        string html = "<html><body><p>Nothing here</p></body></html>";
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => IndexScraper.ScrapeIdsAsync(FetcherFor(html))).ConfigureAwait(true);

        Assert.Contains("No cards found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScrapeIdsAsyncCardsWithNoAnchorsThrowsNoShowIdsException()
    {
        // Cards exist but none have a card-footer anchor → no IDs → exception
        string html = BuildIndexHtml([null, null]);
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => IndexScraper.ScrapeIdsAsync(FetcherFor(html))).ConfigureAwait(true);

        Assert.Contains("No show IDs found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScrapeIdsAsyncCardsWithHrefNotMatchingPatternThrowsNoShowIdsException()
    {
        // Anchor exists but href doesn't contain the "601:<digits>" pattern
        string html = BuildIndexHtml(["/event/other:12345", "/venue/123"]);
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => IndexScraper.ScrapeIdsAsync(FetcherFor(html))).ConfigureAwait(true);

        Assert.Contains("No show IDs found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ScrapeIdsAsyncMixedValidAndInvalidHrefsReturnsOnlyValidIds()
    {
        // One card with no matching href, one valid card → only the valid ID returned
        string html = BuildIndexHtml(["/venue/not-a-show", "/event/601:55555"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        _ = Assert.Single(ids);
        Assert.Equal(55555, ids[0]);
    }

    [Fact]
    public async Task ScrapeIdsAsyncCardWithNoAnchorContinuesAndReturnsOtherIds()
    {
        // A card whose footer has no <a> should be skipped (with a console warning),
        // but a valid card that follows should still be returned.
        string html = BuildIndexHtml([null, "/event/601:77777"]);
        List<int> ids = await IndexScraper.ScrapeIdsAsync(FetcherFor(html)).ConfigureAwait(true);

        _ = Assert.Single(ids);
        Assert.Equal(77777, ids[0]);
    }

    [Fact]
    public async Task ScrapeIdsAsyncFetcherCalledWithIndexUrl()
    {
        string html = BuildIndexHtml(["/event/601:10001"]);
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .Returns(HtmlHelper.ParseAsync(html));

        _ = await IndexScraper.ScrapeIdsAsync(mock.Object).ConfigureAwait(true);

        mock.Verify(
            f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("fringetheatre.ca", StringComparison.Ordinal))),
            Times.Once);
    }
}
