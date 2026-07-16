using Amazon.DynamoDBv2.DataModel;
using Fringe.Data;
using Fringe.Data.Models;
using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests.Services;

/// <summary>
/// Tests for DatabaseInserter.InsertDataAsync.
///
/// Approach: FringeRepository does not implement an interface, but both
/// SaveShowsAsync and SaveShowTimesAsync are declared virtual (added as part of
/// the IFetcher testability refactor).  Moq can therefore create a partial mock
/// of the concrete class by passing a mock IDynamoDBContext to the constructor.
/// The virtual methods are overridden by Moq so no real DynamoDB calls are made.
/// </summary>
public sealed class DatabaseInserterTests
{
    // ── helpers ────────────────────────────────────────────────────────────────

    private static (Mock<FringeRepository> repoMock, DatabaseInserter inserter) CreateInserter()
    {
        var dbContext = new Mock<IDynamoDBContext>();
        var repoMock = new Mock<FringeRepository>(dbContext.Object) { CallBase = false };

        _ = repoMock
            .Setup(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()))
            .Returns(Task.CompletedTask);
        _ = repoMock
            .Setup(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()))
            .Returns(Task.CompletedTask);

        return (repoMock, new DatabaseInserter(repoMock.Object));
    }

    private static Show MakeShow(int id = 1)
    {
        return new Show
        {
            Id = id,
            Title = $"Show {id}",
            Price = 20m,
            Fee = 2m,
            FirstShowDate = new DateOnly(2025, 7, 9),
            LengthInMinutes = 60,
            Venue = new Venue { VenueNumber = 1, Name = "Stage One", Address = "123 St", PostalCode = "T5J2R7", Phone = "7805551234" },
            ContentRating = new ContentRating { Name = "General", Code = "G", Description = "" }
        };
    }

    private static ShowTime MakeShowTime(int showId = 1)
    {
        return new ShowTime
        {
            ShowId = showId,
            DateTime = new DateTime(2025, 7, 9, 19, 30, 0, DateTimeKind.Utc),
            PerformanceTime = new TimeOnly(19, 30),
            PerformanceDate = "July 9, 2025",
            PresentationFormat = "In-Person",
            Reserved = false
        };
    }

    // ── behaviour tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertDataAsyncCallsSaveShowsAsyncWithProvidedShows()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        var shows = new List<Show> { MakeShow(1), MakeShow(2) };

        await inserter.InsertDataAsync(shows, []).ConfigureAwait(true);

        repoMock.Verify(
            r => r.SaveShowsAsync(It.Is<List<Show>>(s => s.Count == 2 && s[0].Id == 1 && s[1].Id == 2)),
            Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncCallsSaveShowTimesAsyncWithProvidedShowTimes()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        var showTimes = new List<ShowTime> { MakeShowTime(1), MakeShowTime(2) };

        await inserter.InsertDataAsync([], showTimes).ConfigureAwait(true);

        repoMock.Verify(
            r => r.SaveShowTimesAsync(It.Is<List<ShowTime>>(st => st.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncEmptyListsBothSaveMethodsStillCalled()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();

        await inserter.InsertDataAsync([], []).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Once);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncSaveShowsAsyncCalledBeforeSaveShowTimesAsync()
    {
        var dbContext = new Mock<IDynamoDBContext>();
        var repoMock = new Mock<FringeRepository>(dbContext.Object) { CallBase = false };

        var callOrder = new List<string>();

        _ = repoMock
            .Setup(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("shows"));
        _ = repoMock
            .Setup(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("showtimes"));

        var inserter = new DatabaseInserter(repoMock.Object);
        await inserter.InsertDataAsync([MakeShow()], [MakeShowTime()]).ConfigureAwait(true);

        Assert.Equal(2, callOrder.Count);
        Assert.Equal("shows", callOrder[0]);
        Assert.Equal("showtimes", callOrder[1]);
    }

    [Fact]
    public async Task InsertDataAsyncSaveShowsAsyncCalledExactlyOnce()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        await inserter.InsertDataAsync([MakeShow()], [MakeShowTime()]).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncSaveShowTimesAsyncCalledExactlyOnce()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        await inserter.InsertDataAsync([MakeShow()], [MakeShowTime()]).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Once);
    }
}
