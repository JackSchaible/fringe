using Amazon.DynamoDBv2.DataModel;
using Fringe.Data;
using Fringe.Data.Models;
using FringeScraper;
using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests;

/// <summary>
/// Tests for ScraperRunner.RunAsync.
///
/// ScraperRunner is a thin orchestrator: it calls three static scraper methods
/// sequentially and exits early when any stage returns an empty collection.
/// The version of RunAsync that accepts an IFetcher threads the fetcher through
/// all three stages, enabling full unit testing without real HTTP calls.
///
/// To test it we use a carefully crafted IFetcher that returns documents fitting
/// all three stages:
///   Stage 1 — IndexScraper: a div.card.text-left with a card-footer anchor
///             whose href contains "601:&lt;id&gt;"
///   Stage 2 — DetailScraper: a .content h2, ul.schedule items for price / date /
///             duration / rating, and a section.venu-main for the venue
///   Stage 3 — ShowTimeFetcher: a div#event-data with data-performances JSON
///
/// Because DetailScraper and ShowTimeFetcher both fetch the same per-show URL
/// ("…/event/601:&lt;id&gt;"), we embed all stage 2 and 3 data in the same HTML page.
/// </summary>
public sealed class ScraperRunnerTests
{
    // ── HTML factories ─────────────────────────────────────────────────────────

    /// <summary>Page returned for the index URL — contains one show card.</summary>
    private static string IndexHtml(int showId)
    {
        return $"""
            <html><body>
              <div class="card text-left">
                <div class="card-footer"><a href="/event/601:{showId}">Buy</a></div>
              </div>
            </body></html>
            """;
    }

    /// <summary>
    /// Page returned for the per-show URL — satisfies both DetailScraper and
    /// ShowTimeFetcher selectors in one document.
    /// </summary>
    private static string ShowDetailAndTimesHtml(int showId)
    {
        // Build the performances JSON as a plain string to avoid brace-escaping
        // conflicts with C# interpolated raw string literals.
#pragma warning disable JSON002
        string perfJson = """{"times":{"2025-07-09":[{"presentationFormat":"In-Person","performanceTime":"19:30","performanceRealTime":"2025-07-09T19:30:00","performanceDate":"July 9, 2025","reserved":false}]}}""";
#pragma warning restore JSON002

        return $"""
            <html><body>
              <div class="content">
                <h2>Show {showId}</h2>
                <p>A great show.</p>
                <ul class="schedule">
                  <li>Theatre</li>
                  <li>$25.00 inc $3.50</li>
                  <li>9-July 12, 2025</li>
                  <li>60 minute show</li>
                  <li>General (G)</li>
                </ul>
              </div>
              <section class="venu-main">
                <h3>01: Stage One</h3>
                <p>123 Main St</p>
                <p>T5J 2R7</p>
                <span>780-555-1234</span>
              </section>
              <div id="event-data" data-performances='{perfJson}'></div>
            </body></html>
            """;
    }

    /// <summary>Creates a stub repository whose virtual save methods do nothing.</summary>
    private static Mock<FringeRepository> StubRepository()
    {
        var db = new Mock<IDynamoDBContext>();
        var repo = new Mock<FringeRepository>(db.Object) { CallBase = false };
        _ = repo.Setup(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>())).Returns(Task.CompletedTask);
        _ = repo.Setup(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>())).Returns(Task.CompletedTask);
        return repo;
    }

    /// <summary>
    /// Builds a fetcher that routes requests to the correct HTML:
    ///   • URLs containing "fringetheatre.ca/events" → index page
    ///   • URLs containing "601:&lt;id&gt;"              → show detail page
    /// </summary>
    private static IFetcher FullPipelineFetcher(int showId)
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("/events", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(IndexHtml(showId)));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains($"601:{showId}", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(ShowDetailAndTimesHtml(showId)));
        return mock.Object;
    }

    // ── happy path ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsyncFullPipelineCallsSaveShowsAsync()
    {
        Mock<FringeRepository> repoMock = StubRepository();
        await ScraperRunner.RunAsync(repoMock.Object, FullPipelineFetcher(42)).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Once);
    }

    [Fact]
    public async Task RunAsyncFullPipelineCallsSaveShowTimesAsync()
    {
        Mock<FringeRepository> repoMock = StubRepository();
        await ScraperRunner.RunAsync(repoMock.Object, FullPipelineFetcher(42)).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Once);
    }

    // ── early-exit: no show IDs ────────────────────────────────────────────────

    [Fact]
    public async Task RunAsyncIndexPageHasNoCardsExitsBeforeDetailScraper()
    {
        // Index page has no div.card.text-left → ScrapeIdsAsync throws; RunAsync catches
        // and exits gracefully after logging.
        string emptyIndex = "<html><body><p>No shows yet</p></body></html>";
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .Returns(HtmlHelper.ParseAsync(emptyIndex));

        Mock<FringeRepository> repoMock = StubRepository();
        // Should not throw; the exception from IndexScraper is propagated (RunAsync
        // does not swallow it — the caller is expected to handle it)
        _ = await Assert.ThrowsAsync<InvalidOperationException>(() => ScraperRunner.RunAsync(repoMock.Object, mock.Object)).ConfigureAwait(true);

        // Since we never reach DatabaseInserter, save methods must not have been called
        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Never);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Never);
    }

    // ── early-exit: no shows scraped ───────────────────────────────────────────

    [Fact]
    public async Task RunAsyncEmptyDetailPageExitsBeforeSavingBecauseNoShowtimes()
    {
        // An empty detail page still yields a Show (empty title is fine) so shows is non-empty,
        // but the empty page has no #event-data element → showtimes list is empty →
        // RunAsync exits early before calling either save method.
        int showId = 5;
        string indexHtml = IndexHtml(showId);
        string emptyDetailHtml = "<html><body></body></html>";

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("/events", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(indexHtml));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains($"601:{showId}", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(emptyDetailHtml));

        Mock<FringeRepository> repoMock = StubRepository();
        await ScraperRunner.RunAsync(repoMock.Object, mock.Object).ConfigureAwait(true);

        // ShowTimeFetcher returns empty → early exit before DatabaseInserter
        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Never);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Never);
    }

    [Fact]
    public async Task RunAsyncDetailScraperThrowsForEveryShowSaveNeverCalled()
    {
        int showId = 7;
        string indexHtml = IndexHtml(showId);

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("/events", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(indexHtml));
        // Per-show URL throws — so shows list will be empty
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains($"601:{showId}", StringComparison.Ordinal))))
            .ThrowsAsync(new HttpRequestException("server error"));

        Mock<FringeRepository> repoMock = StubRepository();
        await ScraperRunner.RunAsync(repoMock.Object, mock.Object).ConfigureAwait(true);

        // No shows scraped → early exit before DatabaseInserter
        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Never);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Never);
    }

    // ── early-exit: no showtimes ───────────────────────────────────────────────

    [Fact]
    public async Task RunAsyncNoShowtimesScrapedSaveShowTimesAsyncNeverCalled()
    {
        int showId = 9;
        // Show detail page has no #event-data → showtime fetcher returns empty
        string detailNoShowtimes = $"""
            <html><body>
              <div class="content">
                <h2>Show {showId}</h2>
                <p>A great show.</p>
                <ul class="schedule">
                  <li>Theatre</li>
                  <li>$25.00 inc $3.50</li>
                  <li>9-July 12, 2025</li>
                  <li>60 minute show</li>
                  <li>General (G)</li>
                </ul>
              </div>
              <section class="venu-main">
                <h3>01: Stage One</h3>
              </section>
            </body></html>
            """;

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("/events", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(IndexHtml(showId)));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains($"601:{showId}", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(detailNoShowtimes));

        Mock<FringeRepository> repoMock = StubRepository();
        await ScraperRunner.RunAsync(repoMock.Object, mock.Object).ConfigureAwait(true);

        // Shows were scraped, but no showtimes → early exit before saving showtimes
        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Never);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Never);
    }
}
