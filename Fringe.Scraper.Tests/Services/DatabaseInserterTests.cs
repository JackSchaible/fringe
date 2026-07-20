using System.Collections.ObjectModel;
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
/// Approach: FringeRepository does not implement an interface, but SaveVenuesAsync,
/// SaveShowsAsync, and SaveShowTimesAsync are all declared virtual (added as part of
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
            .Setup(r => r.SaveVenuesAsync(It.IsAny<IEnumerable<Venue>>()))
            .Returns(Task.CompletedTask);
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

    private static Venue MakeVenue(int venueNumber = 1)
    {
        return new Venue { VenueNumber = venueNumber, Name = "Stage One", Address = "123 St", PostalCode = "T5J2R7", Phone = "7805551234" };
    }

    private static FestivalImport MakeImport(List<Show>? shows = null, List<ShowTime>? showTimes = null, List<Venue>? venues = null)
    {
        return new FestivalImport
        {
            Shows = [.. shows ?? []],
            ShowTimes = [.. showTimes ?? []],
            Venues = [.. venues ?? []]
        };
    }

    // ── behaviour tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task InsertDataAsyncCallsSaveShowsAsyncWithProvidedShows()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        var shows = new List<Show> { MakeShow(1), MakeShow(2) };

        await inserter.InsertDataAsync(MakeImport(shows: shows)).ConfigureAwait(true);

        repoMock.Verify(
            r => r.SaveShowsAsync(It.Is<Collection<Show>>(s => s.Count == 2 && s[0].Id == 1 && s[1].Id == 2)),
            Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncCallsSaveShowTimesAsyncWithProvidedShowTimes()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        var showTimes = new List<ShowTime> { MakeShowTime(1), MakeShowTime(2) };

        await inserter.InsertDataAsync(MakeImport(showTimes: showTimes)).ConfigureAwait(true);

        repoMock.Verify(
            r => r.SaveShowTimesAsync(It.Is<Collection<ShowTime>>(st => st.Count == 2)),
            Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncCallsSaveVenuesAsyncWithProvidedVenues()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        var venues = new List<Venue> { MakeVenue(1), MakeVenue(2) };

        await inserter.InsertDataAsync(MakeImport(venues: venues)).ConfigureAwait(true);

        repoMock.Verify(
            r => r.SaveVenuesAsync(It.Is<Collection<Venue>>(v => v.Count == 2 && v[0].VenueNumber == 1 && v[1].VenueNumber == 2)),
            Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncEmptyListsAllSaveMethodsStillCalled()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();

        await inserter.InsertDataAsync(MakeImport()).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveVenuesAsync(It.IsAny<IEnumerable<Venue>>()), Times.Once);
        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Once);
        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncSavesVenuesThenShowsThenShowTimes()
    {
        var dbContext = new Mock<IDynamoDBContext>();
        var repoMock = new Mock<FringeRepository>(dbContext.Object) { CallBase = false };

        var callOrder = new List<string>();

        _ = repoMock
            .Setup(r => r.SaveVenuesAsync(It.IsAny<IEnumerable<Venue>>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("venues"));
        _ = repoMock
            .Setup(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("shows"));
        _ = repoMock
            .Setup(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()))
            .Returns(Task.CompletedTask)
            .Callback(() => callOrder.Add("showtimes"));

        var inserter = new DatabaseInserter(repoMock.Object);
        await inserter.InsertDataAsync(MakeImport([MakeShow()], [MakeShowTime()], [MakeVenue()])).ConfigureAwait(true);

        Assert.Equal(3, callOrder.Count);
        Assert.Equal("venues", callOrder[0]);
        Assert.Equal("shows", callOrder[1]);
        Assert.Equal("showtimes", callOrder[2]);
    }

    [Fact]
    public async Task InsertDataAsyncSaveShowsAsyncCalledExactlyOnce()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        await inserter.InsertDataAsync(MakeImport([MakeShow()], [MakeShowTime()], [MakeVenue()])).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowsAsync(It.IsAny<IEnumerable<Show>>()), Times.Once);
    }

    [Fact]
    public async Task InsertDataAsyncSaveShowTimesAsyncCalledExactlyOnce()
    {
        (Mock<FringeRepository> repoMock, DatabaseInserter inserter) = CreateInserter();
        await inserter.InsertDataAsync(MakeImport([MakeShow()], [MakeShowTime()], [MakeVenue()])).ConfigureAwait(true);

        repoMock.Verify(r => r.SaveShowTimesAsync(It.IsAny<IEnumerable<ShowTime>>()), Times.Once);
    }
}
