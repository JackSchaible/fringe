using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Moq;

namespace Fringe.API.Tests.Controllers;

/// <summary>Tests for ShowsController.ToDto and GetShows.</summary>
public sealed class ShowsControllerTests
{
    private static Mock<FringeRepository> BuildMockRepo()
    {
        return new Mock<FringeRepository>(MockBehavior.Strict, Mock.Of<IDynamoDBContext>());
    }

    // ── ToDto static helper ───────────────────────────────────────────────────

    [Fact]
    public void ToDtoMapsAllFields()
    {
        ShowRecord record = new()
        {
            ShowId = 42,
            Title = "Test Show",
            Description = "<p>desc</p>",
            PlainTextDescription = "desc",
            ImageUrl = new Uri("https://img.example.com/show.png"),
            Tag = "comedy",
            Price = "12.00",
            Fee = "1.50",
            LengthInMinutes = 60,
            Venue = new VenueData { Name = "Main Stage", Address = "123 Street", Phone = "555-1234" },
            ContentRating = new ContentRatingData { Name = "General", Code = "G", Description = "All ages" }
        };
        List<string> times = ["2025-07-15T19:00:00Z", "2025-07-16T14:00:00Z"];

        ShowDto dto = ShowsController.ToDto(record, times);

        Assert.Equal(42, dto.ShowId);
        Assert.Equal("Test Show", dto.Title);
        Assert.Equal("<p>desc</p>", dto.Description);
        Assert.Equal("desc", dto.PlainTextDescription);
        Assert.Equal(new Uri("https://img.example.com/show.png"), dto.ImageUrl);
        Assert.Equal("comedy", dto.Tag);
        Assert.Equal("12.00", dto.Price);
        Assert.Equal("1.50", dto.Fee);
        Assert.Equal(60, dto.LengthInMinutes);
        Assert.NotNull(dto.Venue);
        Assert.Equal("Main Stage", dto.Venue!.Name);
        Assert.Equal("123 Street", dto.Venue.Address);
        Assert.Equal("555-1234", dto.Venue.Phone);
        Assert.NotNull(dto.ContentRating);
        Assert.Equal("General", dto.ContentRating!.Name);
        Assert.Equal("G", dto.ContentRating.Code);
        Assert.Equal("All ages", dto.ContentRating.Description);
        Assert.Equal(times, dto.ShowTimes);
    }

    [Fact]
    public void ToDtoNullVenueReturnsNullVenueDto()
    {
        ShowRecord record = new()
        {
            ShowId = 1,
            Title = "No Venue",
            Price = "0.00",
            Fee = "0.00",
            LengthInMinutes = 45,
            Venue = null,
            ContentRating = null
        };

        ShowDto dto = ShowsController.ToDto(record, []);

        Assert.Null(dto.Venue);
        Assert.Null(dto.ContentRating);
        Assert.Empty(dto.ShowTimes);
    }

    [Fact]
    public void ToDtoNullOptionalFieldsArePropagated()
    {
        ShowRecord record = new()
        {
            ShowId = 5,
            Title = "Minimal",
            Description = null,
            PlainTextDescription = null,
            ImageUrl = null,
            Tag = null,
            Price = "10.00",
            Fee = "2.00",
            LengthInMinutes = 30,
        };

        ShowDto dto = ShowsController.ToDto(record, []);

        Assert.Null(dto.Description);
        Assert.Null(dto.PlainTextDescription);
        Assert.Null(dto.ImageUrl);
        Assert.Null(dto.Tag);
    }

    [Fact]
    public void ToDtoShowTimesListIsPassedThrough()
    {
        ShowRecord record = new() { ShowId = 1, Title = "A", Price = "0", Fee = "0", LengthInMinutes = 60 };
        List<string> times = ["2025-07-15T10:00:00Z"];

        ShowDto dto = ShowsController.ToDto(record, times);

        Assert.Same(times, dto.ShowTimes);
    }

    // ── GetShows ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetShowsReturnsEmptyListWhenNoShows()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([]);
        ShowsController controller = new(mockRepo.Object);

        List<ShowDto> result = await controller.GetShows().ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetShowsReturnsSortedByTitle()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<ShowRecord> shows =
        [
            new() { ShowId = 1, Title = "Zebra Show", Price = "10", Fee = "1", LengthInMinutes = 60 },
            new() { ShowId = 2, Title = "Apple Show", Price = "10", Fee = "1", LengthInMinutes = 60 },
            new() { ShowId = 3, Title = "Mango Show", Price = "10", Fee = "1", LengthInMinutes = 60 },
        ];
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(shows);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync([]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(3)).ReturnsAsync([]);
        ShowsController controller = new(mockRepo.Object);

        List<ShowDto> result = await controller.GetShows().ConfigureAwait(true);

        Assert.Equal(3, result.Count);
        Assert.Equal("Apple Show", result[0].Title);
        Assert.Equal("Mango Show", result[1].Title);
        Assert.Equal("Zebra Show", result[2].Title);
    }

    [Fact]
    public async Task GetShowsShowTimesAreSortedAndAttachedCorrectly()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<ShowRecord> shows =
        [
            new() { ShowId = 10, Title = "Show A", Price = "10", Fee = "1", LengthInMinutes = 60 }
        ];
        List<ShowTimeRecord> showTimes =
        [
            new() { Pk = "SHOW#10", Sk = "SHOWTIME#2025-07-16T19:00:00Z", DateTime = "2025-07-16T19:00:00Z", PerformanceDate = "", PerformanceTime = "", PresentationFormat = "" },
            new() { Pk = "SHOW#10", Sk = "SHOWTIME#2025-07-15T14:00:00Z", DateTime = "2025-07-15T14:00:00Z", PerformanceDate = "", PerformanceTime = "", PresentationFormat = "" },
        ];
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(shows);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(10)).ReturnsAsync(showTimes);
        ShowsController controller = new(mockRepo.Object);

        List<ShowDto> result = await controller.GetShows().ConfigureAwait(true);

        _ = Assert.Single(result);
        Assert.Equal(2, result[0].ShowTimes.Count);
        Assert.Equal("2025-07-15T14:00:00Z", result[0].ShowTimes[0]);
        Assert.Equal("2025-07-16T19:00:00Z", result[0].ShowTimes[1]);
    }

    [Fact]
    public async Task GetShowsMultipleShowsAttachesCorrectShowTimes()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<ShowRecord> shows =
        [
            new() { ShowId = 1, Title = "Alpha", Price = "10", Fee = "1", LengthInMinutes = 60 },
            new() { ShowId = 2, Title = "Beta", Price = "10", Fee = "1", LengthInMinutes = 60 },
        ];
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(shows);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
        [
            new ShowTimeRecord { Pk = "SHOW#1", Sk = "SHOWTIME#T1", DateTime = "2025-07-15T10:00:00Z", PerformanceDate = "", PerformanceTime = "", PresentationFormat = "" }
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
        [
            new ShowTimeRecord { Pk = "SHOW#2", Sk = "SHOWTIME#T2", DateTime = "2025-07-16T10:00:00Z", PerformanceDate = "", PerformanceTime = "", PresentationFormat = "" }
        ]);
        ShowsController controller = new(mockRepo.Object);

        List<ShowDto> result = await controller.GetShows().ConfigureAwait(true);

        ShowDto alpha = result.First(x => x.Title == "Alpha");
        ShowDto beta = result.First(x => x.Title == "Beta");
        _ = Assert.Single(alpha.ShowTimes);
        Assert.Equal("2025-07-15T10:00:00Z", alpha.ShowTimes[0]);
        _ = Assert.Single(beta.ShowTimes);
        Assert.Equal("2025-07-16T10:00:00Z", beta.ShowTimes[0]);
    }
}
