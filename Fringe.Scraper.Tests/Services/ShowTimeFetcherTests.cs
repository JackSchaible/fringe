using Fringe.Data.Models;
using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests.Services;

public sealed class ShowTimeFetcherTests
{
    // ── HTML building helpers ──────────────────────────────────────────────────

    /// <summary>
    /// Creates an event page HTML string with the #event-data element
    /// whose data-performances attribute contains the supplied JSON.
    /// </summary>
    private static string BuildEventHtml(string? performancesJson = null)
    {
        string attr = performancesJson is null
            ? ""
            : $" data-performances='{performancesJson}'";

        return $"<html><body><div id=\"event-data\"{attr}></div></body></html>";
    }

    private static string MinimalPerformancesJson(
        string date = "2025-07-09",
        string performanceTime = "19:30",
        string performanceRealTime = "2025-07-09T19:30:00",
        string performanceDate = "July 9, 2025",
        string presentationFormat = "In-Person",
        bool reserved = false)
    {
        string reservedStr = reserved ? "true" : "false";
        return "{\"times\":{\"" + date + "\":[{\"presentationFormat\":\"" + presentationFormat
            + "\",\"performanceTime\":\"" + performanceTime
            + "\",\"performanceRealTime\":\"" + performanceRealTime
            + "\",\"performanceDate\":\"" + performanceDate
            + "\",\"reserved\":" + reservedStr + "}]}}";
    }

    private static IFetcher FetcherReturning(string html)
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .Returns(HtmlHelper.ParseAsync(html));
        return mock.Object;
    }

    // ── happy-path tests ───────────────────────────────────────────────────────

    [Fact]
    public async Task PullShowtimesForShowsAsyncValidPerformancesJsonParsesShowtime()
    {
        string html = BuildEventHtml(MinimalPerformancesJson());
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([42], FetcherReturning(html)).ConfigureAwait(true);

        _ = Assert.Single(showtimes);
        ShowTime st = showtimes[0];
        Assert.Equal(42, st.ShowId);
        Assert.Equal(new DateTime(2025, 7, 9, 19, 30, 0, DateTimeKind.Unspecified), st.DateTime);
        Assert.Equal(new TimeOnly(19, 30), st.PerformanceTime);
        Assert.Equal("July 9, 2025", st.PerformanceDate);
        Assert.Equal("In-Person", st.PresentationFormat);
        Assert.False(st.Reserved);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncReservedTrueParsesReservedFlag()
    {
        string html = BuildEventHtml(MinimalPerformancesJson(reserved: true));
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        _ = Assert.Single(showtimes);
        Assert.True(showtimes[0].Reserved);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncMultiplePerformancesOnSameDayReturnsAll()
    {
        string json = /*lang=json,strict*/ """
            {"times":{"2025-07-09":[
                {"presentationFormat":"In-Person","performanceTime":"14:00","performanceRealTime":"2025-07-09T14:00:00","performanceDate":"July 9, 2025","reserved":false},
                {"presentationFormat":"In-Person","performanceTime":"19:30","performanceRealTime":"2025-07-09T19:30:00","performanceDate":"July 9, 2025","reserved":false}
            ]}}
            """;

        string html = BuildEventHtml(json);
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(2, showtimes.Count);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncPerformancesAcrossMultipleDaysReturnsAll()
    {
        string json = /*lang=json,strict*/ """
            {"times":{
                "2025-07-09":[{"presentationFormat":"In-Person","performanceTime":"19:30","performanceRealTime":"2025-07-09T19:30:00","performanceDate":"July 9, 2025","reserved":false}],
                "2025-07-10":[{"presentationFormat":"In-Person","performanceTime":"19:30","performanceRealTime":"2025-07-10T19:30:00","performanceDate":"July 10, 2025","reserved":false}]
            }}
            """;

        string html = BuildEventHtml(json);
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Equal(2, showtimes.Count);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncMultipleShowIdsAggregatesAllShowtimes()
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(BuildEventHtml(MinimalPerformancesJson())));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(BuildEventHtml(MinimalPerformancesJson(date: "2025-07-10", performanceRealTime: "2025-07-10T14:00:00"))));

        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        Assert.Equal(2, showtimes.Count);
        Assert.Contains(showtimes, st => st.ShowId == 1);
        Assert.Contains(showtimes, st => st.ShowId == 2);
    }

    // ── missing / empty #event-data ────────────────────────────────────────────

    [Fact]
    public async Task PullShowtimesForShowsAsyncNoEventDataElementReturnsEmptyList()
    {
        string html = "<html><body><p>No event data here</p></body></html>";
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncEventDataElementWithNoAttributeReturnsEmptyList()
    {
        // #event-data exists but has no data-performances attribute
        string html = "<html><body><div id=\"event-data\"></div></body></html>";
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncEmptyDataPerformancesAttributeReturnsEmptyList()
    {
        string html = "<html><body><div id=\"event-data\" data-performances=\"\"></div></body></html>";
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncJsonWithNullTimesReturnsEmptyList()
    {
        string html = BuildEventHtml(/*lang=json,strict*/ "{\"times\":null}");
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncJsonWithEmptyTimesDictReturnsEmptyList()
    {
        string html = BuildEventHtml(/*lang=json,strict*/ "{\"times\":{}}");
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1], FetcherReturning(html)).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    // ── HTTP failure ───────────────────────────────────────────────────────────

    [Fact]
    public async Task PullShowtimesForShowsAsyncFetcherThrowsForOneShowOtherShowtimesStillReturned()
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:1", StringComparison.Ordinal))))
            .ThrowsAsync(new HttpRequestException("timeout"));
        _ = mock.Setup(f => f.LoadAsync(It.Is<Uri>(u => u.AbsoluteUri.Contains("601:2", StringComparison.Ordinal))))
            .Returns(HtmlHelper.ParseAsync(BuildEventHtml(MinimalPerformancesJson())));

        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1, 2], mock.Object).ConfigureAwait(true);

        // Show 1 failed but show 2's showtime should still be returned
        _ = Assert.Single(showtimes);
        Assert.Equal(2, showtimes[0].ShowId);
    }

    [Fact]
    public async Task PullShowtimesForShowsAsyncAllFetchersFailReturnsEmptyList()
    {
        var mock = new Mock<IFetcher>();
        _ = mock.Setup(f => f.LoadAsync(It.IsAny<Uri>()))
            .ThrowsAsync(new HttpRequestException("server error"));

        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([1, 2, 3], mock.Object).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }

    // ── empty showIds ──────────────────────────────────────────────────────────

    [Fact]
    public async Task PullShowtimesForShowsAsyncEmptyShowIdListReturnsEmptyList()
    {
        IFetcher fetcher = FetcherReturning("<html></html>");
        List<ShowTime> showtimes = await ShowTimeFetcher.PullShowtimesForShowsAsync([], fetcher).ConfigureAwait(true);

        Assert.Empty(showtimes);
    }
}
