using Fringe.Data.Models;
using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests.Services;

public sealed class DetailScraperTests
{
    // ── HTML building helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal show-detail page that satisfies all selectors used by
    /// DetailScraper.  Every parameter is optional and defaults to something
    /// recognisable so individual tests can assert on specific fields.
    /// </summary>
    private static string BuildDetailHtml(
        string title = "Test Show Title",
        string description = "A great show.",
        string tag = "Theatre",
        string priceItem = "$25.00 inc $3.50",
        string dateItem = "9-July 12, 2025",
        string durationItem = "60 minute show",
        string ratingItem = "General (G)",
        string venueH3 = "01: Stage One",
        string venueAddress = "123 Main St",
        string venuePostal = "T5J 2R7",
        string venuePhone = "780-555-1234",
        string imageUrl = "https://example.com/img.jpg")
    {
        return $"""
            <html><body>
              <div class="content">
                <h2>{title}</h2>
                <p>{description}</p>
                <ul class="schedule">
                  <li>{tag}</li>
                  <li>{priceItem}</li>
                  <li>{dateItem}</li>
                  <li>{durationItem}</li>
                  <li>{ratingItem}</li>
                </ul>
              </div>
              <section class="venu-main">
                <h3>{venueH3}</h3>
                <p>{venueAddress}</p>
                <p>{venuePostal}</p>
                <span>{venuePhone}</span>
              </section>
              <img class="event-image-square" src="{imageUrl}" />
            </body></html>
            """;
    }

    private static IFetcher FetcherReturning(string html)
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .Returns(HtmlHelper.ParseAsync(html));
        return mock.Object;
    }

    // ── single-show happy path ─────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncSingleShowParsesTitle()
    {
        string html = BuildDetailHtml(title: "My Amazing Show");
        IFetcher fetcher = FetcherReturning(html);

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([42], fetcher).ConfigureAwait(true);

        _ = Assert.Single(shows);
        Assert.Equal("My Amazing Show", shows[0].Title);
    }

    [Fact]
    public async Task ScrapeShowsAsyncSingleShowSetsShowId()
    {
        IFetcher fetcher = FetcherReturning(BuildDetailHtml());

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([999], fetcher).ConfigureAwait(true);

        _ = Assert.Single(shows);
        Assert.Equal(999, shows[0].Id);
    }

    [Fact]
    public async Task ScrapeShowsAsyncSingleShowParsesImageUrl()
    {
        string html = BuildDetailHtml(imageUrl: "https://cdn.example.com/poster.jpg");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(new Uri("https://cdn.example.com/poster.jpg"), shows[0].ImageUrl);
    }

    // ── price parsing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncPriceAndFeeExtractedCorrectly()
    {
        // "$25.00 inc $3.50" → price = 25.00 - 3.50 = 21.50, fee = 3.50
        string html = BuildDetailHtml(priceItem: "$25.00 inc $3.50");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(21.50m, shows[0].Price);
        Assert.Equal(3.50m, shows[0].Fee);
    }

    [Fact]
    public async Task ScrapeShowsAsyncNoIncTextPriceAndFeeAreZero()
    {
        string html = BuildDetailHtml(priceItem: "Admission is free");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(0m, shows[0].Price);
        Assert.Equal(0m, shows[0].Fee);
    }

    [Fact]
    public async Task ScrapeShowsAsyncIntegerPriceAndFeeParsesCorrectly()
    {
        string html = BuildDetailHtml(priceItem: "$20 inc $2");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(18m, shows[0].Price);
        Assert.Equal(2m, shows[0].Fee);
    }

    // ── date parsing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncDateFormatExtractsFirstShowDate()
    {
        // "9-July 12, 2025" means the run starts July 9, 2025
        string html = BuildDetailHtml(dateItem: "9-July 12, 2025");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(new DateOnly(2025, 7, 9), shows[0].FirstShowDate);
    }

    [Fact]
    public async Task ScrapeShowsAsyncDateFormatWithLeadingZeroDayExtractsFirstShowDate()
    {
        string html = BuildDetailHtml(dateItem: "1-August 15, 2025");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(new DateOnly(2025, 8, 1), shows[0].FirstShowDate);
    }

    [Fact]
    public async Task ScrapeShowsAsyncNoDateItemReturnsMinDate()
    {
        // Remove the date entry so no li matches the date regex
        string html = BuildDetailHtml(dateItem: "No dates listed here");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(DateOnly.MinValue, shows[0].FirstShowDate);
    }

    // ── duration parsing ───────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncDurationItemExtractsDurationInMinutes()
    {
        string html = BuildDetailHtml(durationItem: "60 minute show");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(60, shows[0].LengthInMinutes);
    }

    [Fact]
    public async Task ScrapeShowsAsyncNoDurationItemReturnsDurationZero()
    {
        string html = BuildDetailHtml(durationItem: "Running time not listed");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(0, shows[0].LengthInMinutes);
    }

    [Fact]
    public async Task ScrapeShowsAsync90MinuteDurationParsed()
    {
        string html = BuildDetailHtml(durationItem: "90 minute show");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(90, shows[0].LengthInMinutes);
    }

    // ── venue parsing ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncVenueSectionExtractsVenueNumberAndName()
    {
        string html = BuildDetailHtml(venueH3: "01: Stage One");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(1, shows[0].Venue.VenueNumber);
        Assert.Equal("Stage One", shows[0].Venue.Name);
    }

    [Fact]
    public async Task ScrapeShowsAsyncVenueSectionExtractsAddress()
    {
        string html = BuildDetailHtml(venueAddress: "456 Festival Ave");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal("456 Festival Ave", shows[0].Venue.Address);
    }

    [Fact]
    public async Task ScrapeShowsAsyncVenueSectionExtractsPostalCodeWithoutSpaces()
    {
        string html = BuildDetailHtml(venuePostal: "T5J 2R7");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        // Postal codes have spaces stripped by the scraper
        Assert.Equal("T5J2R7", shows[0].Venue.PostalCode);
    }

    [Fact]
    public async Task ScrapeShowsAsyncVenueSectionExtractsPhoneWithoutDashesOrSpaces()
    {
        string html = BuildDetailHtml(venuePhone: "780-555-1234");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal("7805551234", shows[0].Venue.Phone);
    }

    [Fact]
    public async Task ScrapeShowsAsyncNoVenueSectionUsesDefaultVenue()
    {
        // Build HTML with no <section class="venu-main">
        string html = $"""
            <html><body>
              <div class="content">
                <h2>No Venue Show</h2>
                <p>A show with no venue info.</p>
                <ul class="schedule">
                  <li>Theatre</li>
                </ul>
              </div>
            </body></html>
            """;

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        _ = Assert.Single(shows);
        // venueNumber defaults to -1 when no section found
        Assert.Equal(-1, shows[0].Venue.VenueNumber);
        Assert.Equal("Unknown", shows[0].Venue.Name);
    }

    // ── content rating parsing ─────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncGeneralRatingParsesNameAndCode()
    {
        string html = BuildDetailHtml(ratingItem: "General (G)");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal("General", shows[0].ContentRating.Name);
        Assert.Equal("G", shows[0].ContentRating.Code);
    }

    [Fact]
    public async Task ScrapeShowsAsyncMatureRatingParsesNameAndCode()
    {
        string html = BuildDetailHtml(ratingItem: "Mature (M)");
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal("Mature", shows[0].ContentRating.Name);
        Assert.Equal("M", shows[0].ContentRating.Code);
    }

    [Fact]
    public async Task ScrapeShowsAsyncNoRatingItemDefaultsToUnrated()
    {
        // Schedule list with no item containing "("
        string html = $"""
            <html><body>
              <div class="content">
                <h2>Unrated Show</h2>
                <ul class="schedule">
                  <li>Theatre</li>
                  <li>60 minute show</li>
                </ul>
              </div>
              <section class="venu-main">
                <h3>01: Stage One</h3>
              </section>
            </body></html>
            """;

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal("Unrated", shows[0].ContentRating.Name);
        Assert.Equal("UR", shows[0].ContentRating.Code);
    }

    // ── empty HTML ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncEmptyHtmlReturnsShowWithEmptyTitle()
    {
        string html = "<html><body></body></html>";
        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        _ = Assert.Single(shows);
        Assert.Equal("", shows[0].Title);
    }

    // ── HTTP failure / error handling ──────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncFetcherThrowsForOneShowOtherShowsStillScraped()
    {
        // ID 1 will fail; ID 2 succeeds
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .ThrowsAsync(new HttpRequestException("timeout"));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(BuildDetailHtml(title: "Show Two")));

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        _ = Assert.Single(shows);
        Assert.Equal("Show Two", shows[0].Title);
    }

    [Fact]
    public async Task ScrapeShowsAsyncAllFetchersFailReturnsEmptyShowList()
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .ThrowsAsync(new HttpRequestException("server error"));

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1, 2, 3], mock.Object).ConfigureAwait(true);

        Assert.Empty(shows);
    }

    // ── deduplication of venues and ratings ────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncTwoShowsSameVenueBothShowsReferToSameVenueInstance()
    {
        string htmlA = BuildDetailHtml(title: "Show A", venueH3: "01: Stage One");
        string htmlB = BuildDetailHtml(title: "Show B", venueH3: "01: Stage One");

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlA));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlB));

        (List<Show> shows, List<Venue> dedupedVenues, _) = await DetailScraper.ScrapeShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        Assert.Equal(2, shows.Count);
        // After deduplication both show.Venue properties are the same object reference
        Assert.Same(shows[0].Venue, shows[1].Venue);
        // Only one venue in the deduplicated list
        _ = Assert.Single(dedupedVenues);
    }

    [Fact]
    public async Task ScrapeShowsAsyncTwoShowsSameRatingBothShowsReferToSameRatingInstance()
    {
        string htmlA = BuildDetailHtml(title: "Show A", ratingItem: "General (G)");
        string htmlB = BuildDetailHtml(title: "Show B", ratingItem: "General (G)");

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlA));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlB));

        (List<Show> shows, _, List<ContentRating> dedupedRatings) = await DetailScraper.ScrapeShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        Assert.Equal(2, shows.Count);
        Assert.Same(shows[0].ContentRating, shows[1].ContentRating);
        _ = Assert.Single(dedupedRatings);
    }

    [Fact]
    public async Task ScrapeShowsAsyncTwoShowsDifferentVenuesReturnsTwoDedupedVenues()
    {
        string htmlA = BuildDetailHtml(title: "Show A", venueH3: "01: Stage One");
        string htmlB = BuildDetailHtml(title: "Show B", venueH3: "02: Stage Two");

        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlA));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(htmlB));

        (List<Show> shows, List<Venue> dedupedVenues, _) = await DetailScraper.ScrapeShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        Assert.Equal(2, dedupedVenues.Count);
        Assert.NotSame(shows[0].Venue, shows[1].Venue);
    }

    // ── empty showIds list ─────────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncEmptyListReturnsEmptyResults()
    {
        IFetcher fetcher = FetcherReturning("<html><body></body></html>");
        (List<Show> shows, List<Venue> venues, List<ContentRating> ratings) = await DetailScraper.ScrapeShowsAsync([], fetcher).ConfigureAwait(true);

        Assert.Empty(shows);
        Assert.Empty(venues);
        Assert.Empty(ratings);
    }

    // ── description extraction ─────────────────────────────────────────────────

    [Fact]
    public async Task ScrapeShowsAsyncMultipleDescriptionParagraphsJoinedByDoubleNewline()
    {
        string html = $"""
            <html><body>
              <div class="content">
                <h2>Show Title</h2>
                <p>First paragraph.</p>
                <p>Second paragraph.</p>
                <ul class="schedule">
                  <li>Theatre</li>
                </ul>
              </div>
              <section class="venu-main">
                <h3>01: Stage One</h3>
              </section>
            </body></html>
            """;

        (List<Show> shows, _, _) = await DetailScraper.ScrapeShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Contains("First paragraph.", shows[0].Description, StringComparison.Ordinal);
        Assert.Contains("Second paragraph.", shows[0].Description, StringComparison.Ordinal);
    }
}
