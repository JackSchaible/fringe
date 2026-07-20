using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;
using Moq;
using System.Collections.ObjectModel;

namespace Fringe.Data.Tests;

/// <summary>
/// Unit tests for FringeRepository — mocking IDynamoDBContext.
///
/// Key mocking patterns used:
/// - IAsyncSearch&lt;T&gt;.GetRemainingAsync() for FromQueryAsync and QueryAsync
/// - IBatchWrite&lt;T&gt;.ExecuteAsync() for batch save/delete operations
/// - IDynamoDBContext.LoadAsync / SaveAsync / DeleteAsync for single-item ops
/// </summary>
public sealed class FringeRepositoryTests
{
    private static Mock<IDynamoDBContext> BuildMockDb()
    {
        return new Mock<IDynamoDBContext>(MockBehavior.Loose);
    }

    private static FringeRepository BuildRepo(IDynamoDBContext db)
    {
        return new FringeRepository(db);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static Mock<IAsyncSearch<T>> SetupSearch<T>(Mock<IDynamoDBContext> mockDb,
        List<T> items,
        bool viaFromQuery = false)
    {
        var mockSearch = new Mock<IAsyncSearch<T>>();
        _ = mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync(items);

        _ = viaFromQuery
            ? mockDb.Setup(db => db.FromQueryAsync<T>(It.IsAny<QueryOperationConfig>()))
                    .Returns(mockSearch.Object)
            : mockDb.Setup(db => db.QueryAsync<T>(
                        It.IsAny<string>(),
                        It.IsAny<QueryOperator>(),
                        It.IsAny<IEnumerable<object>>(),
                        It.IsAny<QueryConfig>()))
                    .Returns(mockSearch.Object);

        return mockSearch;
    }

    private static Mock<IBatchWrite<T>> SetupBatchWrite<T>(Mock<IDynamoDBContext> mockDb)
    {
        var mockBatch = new Mock<IBatchWrite<T>>();
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<T>()).Returns(mockBatch.Object);
        return mockBatch;
    }

    private static Show MakeShow(int id, string title = "Test Show")
    {
        return new Show
        {
            Id = id,
            Title = title,
            Description = "A show",
            PlainTextDescription = "A show",
            ImageUrl = new Uri("https://img.example.com/show.png"),
            Tag = "comedy",
            Price = 12.00m,
            Fee = 1.50m,
            FirstShowDate = new DateOnly(2025, 7, 15),
            LengthInMinutes = 60,
            Venue = new() { VenueNumber = 1, Name = "Main Stage", Address = "123 Street", Phone = "555-0000", PostalCode = "T0T 0T0" },
            ContentRating = new() { Name = "General", Code = "G", Description = "All ages" }
        };
    }

    private static ShowTime MakeShowTime(int showId)
    {
        return new ShowTime
        {
            ShowId = showId,
            DateTime = new DateTime(2025, 7, 15, 19, 0, 0, DateTimeKind.Utc),
            PerformanceTime = new TimeOnly(19, 0),
            PerformanceDate = "Tuesday, July 15",
            PresentationFormat = "Standard",
            Reserved = false
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveShowsAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveShowsAsyncSingleShowCallsBatchExecute()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        Mock<IBatchWrite<ShowRecord>> mockBatch = SetupBatchWrite<ShowRecord>(mockDb);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveShowsAsync([MakeShow(1)]).ConfigureAwait(true);

        mockBatch.Verify(b => b.AddPutItem(It.IsAny<ShowRecord>()), Times.Once);
        mockBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
    }

    [Fact]
    public async Task SaveShowsAsyncDuplicateIdsDeduplicatesBeforeBatch()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var putItems = new List<ShowRecord>();
        var mockBatch = new Mock<IBatchWrite<ShowRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowRecord>()))
                     .Callback<ShowRecord>(putItems.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveShowsAsync([MakeShow(1, "First"), MakeShow(1, "Duplicate")]).ConfigureAwait(true);

        _ = Assert.Single(putItems);
        Assert.Equal(1, putItems[0].ShowId);
    }

    [Fact]
    public async Task SaveShowsAsyncMapsShowRecordCorrectly()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        ShowRecord? savedRecord = null;
        var mockBatch = new Mock<IBatchWrite<ShowRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowRecord>()))
                     .Callback<ShowRecord>(r => savedRecord = r);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        Show show = MakeShow(42, "My Show");
        await repo.SaveShowsAsync([show]).ConfigureAwait(true);

        Assert.NotNull(savedRecord);
        Assert.Equal("SHOW#42", savedRecord!.Pk);
        Assert.Equal("METADATA", savedRecord.Sk);
        Assert.Equal("SHOW", savedRecord.EntityType);
        Assert.Equal(42, savedRecord.ShowId);
        Assert.Equal("My Show", savedRecord.Title);
        Assert.Equal("12.00", savedRecord.Price);
        Assert.Equal("1.50", savedRecord.Fee);
        Assert.Equal("2025-07-15", savedRecord.FirstShowDate);
        Assert.Equal(60, savedRecord.LengthInMinutes);
        Assert.NotNull(savedRecord.Venue);
        Assert.Equal("Main Stage", savedRecord.Venue!.Name);
        Assert.Equal("G", savedRecord.ContentRating!.Code);
    }

    [Fact]
    public async Task SaveShowsAsyncMinDateFirstShowDateSavedAsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        ShowRecord? savedRecord = null;
        var mockBatch = new Mock<IBatchWrite<ShowRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowRecord>()))
                     .Callback<ShowRecord>(r => savedRecord = r);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        Show show = MakeShow(1);
        show.FirstShowDate = DateOnly.MinValue;
        await repo.SaveShowsAsync([show]).ConfigureAwait(true);

        Assert.Null(savedRecord!.FirstShowDate);
    }

    [Fact]
    public async Task SaveShowsAsyncEmptyCollectionBatchExecutedWithNoItems()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        Mock<IBatchWrite<ShowRecord>> mockBatch = SetupBatchWrite<ShowRecord>(mockDb);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveShowsAsync([]).ConfigureAwait(true);

        mockBatch.Verify(b => b.AddPutItem(It.IsAny<ShowRecord>()), Times.Never);
        mockBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveShowTimesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveShowTimesAsyncSingleShowTimeCallsBatchExecute()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        Mock<IBatchWrite<ShowTimeRecord>> mockBatch = SetupBatchWrite<ShowTimeRecord>(mockDb);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveShowTimesAsync([MakeShowTime(1)]).ConfigureAwait(true);

        mockBatch.Verify(b => b.AddPutItem(It.IsAny<ShowTimeRecord>()), Times.Once);
        mockBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
    }

    [Fact]
    public async Task SaveShowTimesAsyncDuplicateShowIdAndDateTimeDeduplicates()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var putItems = new List<ShowTimeRecord>();
        var mockBatch = new Mock<IBatchWrite<ShowTimeRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowTimeRecord>()))
                     .Callback<ShowTimeRecord>(putItems.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowTimeRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        ShowTime st = MakeShowTime(1);
        await repo.SaveShowTimesAsync([st, st]).ConfigureAwait(true);

        _ = Assert.Single(putItems);
    }

    [Fact]
    public async Task SaveShowTimesAsyncDifferentShowIdsBothAdded()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var putItems = new List<ShowTimeRecord>();
        var mockBatch = new Mock<IBatchWrite<ShowTimeRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowTimeRecord>()))
                     .Callback<ShowTimeRecord>(putItems.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowTimeRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveShowTimesAsync([MakeShowTime(1), MakeShowTime(2)]).ConfigureAwait(true);

        Assert.Equal(2, putItems.Count);
    }

    [Fact]
    public async Task SaveShowTimesAsyncMapsRecordCorrectly()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        ShowTimeRecord? saved = null;
        var mockBatch = new Mock<IBatchWrite<ShowTimeRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowTimeRecord>()))
                     .Callback<ShowTimeRecord>(r => saved = r);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowTimeRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var dt = new DateTime(2025, 7, 15, 19, 0, 0, DateTimeKind.Utc);
        var st = new ShowTime
        {
            ShowId = 5,
            DateTime = dt,
            PerformanceTime = new TimeOnly(19, 0),
            PerformanceDate = "Tuesday",
            PresentationFormat = "Standard",
            Reserved = true
        };
        await repo.SaveShowTimesAsync([st]).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("SHOW#5", saved!.Pk);
        Assert.StartsWith("SHOWTIME#", saved.Sk, StringComparison.Ordinal);
        Assert.Equal(dt.ToString("O"), saved.DateTime);
        Assert.Equal("19:00", saved.PerformanceTime);
        Assert.Equal("Tuesday", saved.PerformanceDate);
        Assert.Equal("Standard", saved.PresentationFormat);
        Assert.True(saved.Reserved);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAllShowsAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllShowsAsyncReturnsAllShows()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var shows = new List<ShowRecord>
        {
            new() { Pk = "SHOW#1", ShowId = 1, Title = "A", Price = "10", Fee = "1", LengthInMinutes = 60 },
            new() { Pk = "SHOW#2", ShowId = 2, Title = "B", Price = "10", Fee = "1", LengthInMinutes = 60 },
        };
        _ = SetupSearch(mockDb, shows, viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<ShowRecord> result = await repo.GetAllShowsAsync().ConfigureAwait(true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllShowsAsyncEmptyDatabaseReturnsEmptyList()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = SetupSearch(mockDb, new List<ShowRecord>(), viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<ShowRecord> result = await repo.GetAllShowsAsync().ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllShowsAsyncQueriesEntityTypeIndex()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        QueryOperationConfig? capturedConfig = null;
        var mockSearch = new Mock<IAsyncSearch<ShowRecord>>();
        _ = mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync([]);
        _ = mockDb.Setup(db => db.FromQueryAsync<ShowRecord>(It.IsAny<QueryOperationConfig>()))
                  .Callback<QueryOperationConfig>(cfg => capturedConfig = cfg)
                  .Returns(mockSearch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetAllShowsAsync().ConfigureAwait(true);

        Assert.NotNull(capturedConfig);
        Assert.Equal("entity-type-index", capturedConfig!.IndexName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetShowAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetShowAsyncExistingShowReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var record = new ShowRecord { Pk = "SHOW#42", ShowId = 42, Title = "My Show", Price = "0", Fee = "0", LengthInMinutes = 60 };
        _ = mockDb.Setup(db => db.LoadAsync<ShowRecord>("SHOW#42", "METADATA", default))
                  .Returns(Task.FromResult(record));
        FringeRepository repo = BuildRepo(mockDb.Object);

        ShowRecord? result = await repo.GetShowAsync(42).ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal(42, result!.ShowId);
    }

    [Fact]
    public async Task GetShowAsyncNonExistentShowReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<ShowRecord>("SHOW#999", "METADATA", default))
                  .Returns(Task.FromResult<ShowRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        ShowRecord? result = await repo.GetShowAsync(999).ConfigureAwait(true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetShowAsyncLoadsWithCorrectKeys()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<ShowRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<ShowRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetShowAsync(7).ConfigureAwait(true);

        mockDb.Verify(db => db.LoadAsync<ShowRecord>("SHOW#7", "METADATA", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveVenuesAsync
    // ─────────────────────────────────────────────────────────────────────────

    private static Venue MakeVenue(int venueNumber, string name = "Main Stage")
    {
        return new Venue
        {
            VenueNumber = venueNumber,
            Name = name,
            Address = "123 Street",
            Phone = "555-0000",
            PostalCode = "T0T 0T0"
        };
    }

    [Fact]
    public async Task SaveVenuesAsyncNewVenueSavesRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<VenueRecord>(null!));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveVenuesAsync([MakeVenue(1)]).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("VENUE#1", saved!.Pk);
        Assert.Equal("METADATA", saved.Sk);
        Assert.Equal("VENUE", saved.EntityType);
        Assert.Equal(1, saved.VenueNumber);
        Assert.Equal("Main Stage", saved.Name);
    }

    [Fact]
    public async Task SaveVenuesAsyncDuplicateVenueNumbersDeduplicatesBeforeSave()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<VenueRecord>(null!));
        var savedRecords = new List<VenueRecord>();
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => savedRecords.Add(r))
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveVenuesAsync([MakeVenue(1, "First"), MakeVenue(1, "Duplicate")]).ConfigureAwait(true);

        _ = Assert.Single(savedRecords);
        Assert.Equal("First", savedRecords[0].Name);
    }

    [Fact]
    public async Task SaveVenuesAsyncNoExistingRecordEnrichmentFieldsAreNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<VenueRecord>(null!));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveVenuesAsync([MakeVenue(1)]).ConfigureAwait(true);

        Assert.Null(saved!.Latitude);
        Assert.Null(saved.Longitude);
    }

    [Fact]
    public async Task SaveVenuesAsyncExistingRecordPreservesEnrichmentFields()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var existing = new VenueRecord
        {
            Pk = "VENUE#1",
            VenueNumber = 1,
            Name = "Old Name",
            Address = "Old Address",
            Phone = "000",
            PostalCode = "T0T 0T0",
            Latitude = 53.5461,
            Longitude = -113.4938
        };
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveVenuesAsync([MakeVenue(1, "New Name")]).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("New Name", saved!.Name);
        Assert.Equal(53.5461, saved.Latitude);
        Assert.Equal(-113.4938, saved.Longitude);
    }

    [Fact]
    public async Task SaveVenuesAsyncUnchangedFestivalFieldsSkipsWrite()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var existing = new VenueRecord
        {
            Pk = "VENUE#1",
            VenueNumber = 1,
            Name = "Main Stage",
            Address = "123 Street",
            Phone = "555-0000",
            PostalCode = "T0T 0T0",
            Latitude = 53.5461,
            Longitude = -113.4938
        };
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        FringeRepository repo = BuildRepo(mockDb.Object);

        // Same festival-owned fields as `existing` — simulates a show changing while its venue does not.
        await repo.SaveVenuesAsync([MakeVenue(1)]).ConfigureAwait(true);

        mockDb.Verify(db => db.SaveAsync(It.IsAny<VenueRecord>(), default), Times.Never);
    }

    [Fact]
    public async Task SaveVenuesAsyncChangedFestivalFieldSavesUpdatedRecordAndPreservesEnrichment()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var existing = new VenueRecord
        {
            Pk = "VENUE#1",
            VenueNumber = 1,
            Name = "Main Stage",
            Address = "Old Address",
            Phone = "555-0000",
            PostalCode = "T0T 0T0",
            Latitude = 53.5461,
            Longitude = -113.4938
        };
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        Venue venue = MakeVenue(1);
        venue.Address = "New Address";
        await repo.SaveVenuesAsync([venue]).ConfigureAwait(true);

        mockDb.Verify(db => db.SaveAsync(It.IsAny<VenueRecord>(), default), Times.Once);
        Assert.NotNull(saved);
        Assert.Equal("New Address", saved!.Address);
        Assert.Equal(53.5461, saved.Latitude);
        Assert.Equal(-113.4938, saved.Longitude);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetVenueAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVenueAsyncExistingVenueReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var record = new VenueRecord { Pk = "VENUE#1", VenueNumber = 1, Name = "Main Stage", Address = "", Phone = "", PostalCode = "" };
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(record));
        FringeRepository repo = BuildRepo(mockDb.Object);

        VenueRecord? result = await repo.GetVenueAsync(1).ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal(1, result!.VenueNumber);
    }

    [Fact]
    public async Task GetVenueAsyncNonExistentVenueReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<VenueRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        VenueRecord? result = await repo.GetVenueAsync(999).ConfigureAwait(true);

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAllVenuesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllVenuesAsyncReturnsAllVenues()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var venues = new List<VenueRecord>
        {
            new() { Pk = "VENUE#1", VenueNumber = 1, Name = "A", Address = "", Phone = "", PostalCode = "" },
            new() { Pk = "VENUE#2", VenueNumber = 2, Name = "B", Address = "", Phone = "", PostalCode = "" },
        };
        _ = SetupSearch(mockDb, venues, viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetAllVenuesAsync().ConfigureAwait(true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetAllVenuesAsyncQueriesEntityTypeIndex()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        QueryOperationConfig? capturedConfig = null;
        var mockSearch = new Mock<IAsyncSearch<VenueRecord>>();
        _ = mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync([]);
        _ = mockDb.Setup(db => db.FromQueryAsync<VenueRecord>(It.IsAny<QueryOperationConfig>()))
                  .Callback<QueryOperationConfig>(cfg => capturedConfig = cfg)
                  .Returns(mockSearch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetAllVenuesAsync().ConfigureAwait(true);

        Assert.NotNull(capturedConfig);
        Assert.Equal("entity-type-index", capturedConfig!.IndexName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetVenuesNeedingGeocodingAsync
    // ─────────────────────────────────────────────────────────────────────────

    private static VenueRecord MakeVenueRecord(int venueNumber, string address = "123 Street", string postalCode = "T0T 0T0",
        string name = "Main Stage", string phone = "555-0000", double? latitude = null, double? longitude = null,
        string? coordinateSource = null, bool matchingHash = true)
    {
        return new VenueRecord
        {
            Pk = $"VENUE#{venueNumber}",
            VenueNumber = venueNumber,
            Name = name,
            Address = address,
            Phone = phone,
            PostalCode = postalCode,
            Latitude = latitude,
            Longitude = longitude,
            CoordinateSource = coordinateSource,
            AddressHash = matchingHash ? VenueAddressHasher.ComputeHash(venueNumber, address, postalCode) : "stale-hash"
        };
    }

    [Fact]
    public async Task GetVenuesNeedingGeocodingAsyncNoCoordinatesIsEligible()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord venue = MakeVenueRecord(1, latitude: null, longitude: null);
        _ = SetupSearch(mockDb, [venue], viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetVenuesNeedingGeocodingAsync().ConfigureAwait(true);

        _ = Assert.Single(result);
    }

    [Fact]
    public async Task GetVenuesNeedingGeocodingAsyncUnchangedAddressHashIsNotEligible()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord venue = MakeVenueRecord(1, latitude: 53.5, longitude: -113.5, coordinateSource: "OpenRouteService", matchingHash: true);
        _ = SetupSearch(mockDb, [venue], viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetVenuesNeedingGeocodingAsync().ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVenuesNeedingGeocodingAsyncChangedAddressHashIsEligible()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        // AddressHash on the stored record doesn't match what Address/PostalCode hash to now —
        // simulates the street address or postal code changing since the venue was last geocoded.
        VenueRecord venue = MakeVenueRecord(1, latitude: 53.5, longitude: -113.5, coordinateSource: "OpenRouteService", matchingHash: false);
        _ = SetupSearch(mockDb, [venue], viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetVenuesNeedingGeocodingAsync().ConfigureAwait(true);

        _ = Assert.Single(result);
    }

    [Fact]
    public async Task GetVenuesNeedingGeocodingAsyncNameOrPhoneChangeAloneDoesNotAffectEligibility()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        // Hash is computed from venue number + address + postal code only; Name/Phone here
        // differ from what a fresh import would carry, but the hash still matches because
        // MakeVenueRecord hashes off Address/PostalCode regardless of Name/Phone.
        VenueRecord venue = MakeVenueRecord(1, name: "Renamed Venue", phone: "999-9999", latitude: 53.5, longitude: -113.5,
            coordinateSource: "OpenRouteService", matchingHash: true);
        _ = SetupSearch(mockDb, [venue], viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetVenuesNeedingGeocodingAsync().ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVenuesNeedingGeocodingAsyncManualSourceIsNeverEligibleEvenWithChangedHash()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord venue = MakeVenueRecord(1, latitude: 53.5, longitude: -113.5,
            coordinateSource: FringeRepository.ManualCoordinateSource, matchingHash: false);
        _ = SetupSearch(mockDb, [venue], viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<VenueRecord> result = await repo.GetVenuesNeedingGeocodingAsync().ConfigureAwait(true);

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateVenueCoordinatesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateVenueCoordinatesAsyncVenueNotFoundReturnsFalseAndDoesNotWrite()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<VenueRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        bool result = await repo.UpdateVenueCoordinatesAsync(999, 53.5, -113.5, "OpenRouteService", DateTime.UtcNow).ConfigureAwait(true);

        Assert.False(result);
        mockDb.Verify(db => db.SaveAsync(It.IsAny<VenueRecord>(), default), Times.Never);
    }

    [Fact]
    public async Task UpdateVenueCoordinatesAsyncNewCoordinatesSavesLatitudeLongitudeHashSourceAndTimestamp()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord existing = MakeVenueRecord(1, latitude: null, longitude: null, coordinateSource: null);
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var enrichedAt = new DateTime(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        bool result = await repo.UpdateVenueCoordinatesAsync(1, 53.5461, -113.4938, "OpenRouteService", enrichedAt).ConfigureAwait(true);

        Assert.True(result);
        Assert.NotNull(saved);
        Assert.Equal(53.5461, saved!.Latitude);
        Assert.Equal(-113.4938, saved.Longitude);
        Assert.Equal("OpenRouteService", saved.CoordinateSource);
        Assert.Equal(enrichedAt.ToString("O"), saved.EnrichedAt);
        Assert.Equal(VenueAddressHasher.ComputeHash(1, existing.Address, existing.PostalCode), saved.AddressHash);
    }

    [Fact]
    public async Task UpdateVenueCoordinatesAsyncAutomaticSourceCannotOverwriteManualCoordinate()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord existing = MakeVenueRecord(1, latitude: 53.1, longitude: -113.1, coordinateSource: FringeRepository.ManualCoordinateSource);
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        FringeRepository repo = BuildRepo(mockDb.Object);

        bool result = await repo.UpdateVenueCoordinatesAsync(1, 53.9, -113.9, "OpenRouteService", DateTime.UtcNow).ConfigureAwait(true);

        Assert.False(result);
        mockDb.Verify(db => db.SaveAsync(It.IsAny<VenueRecord>(), default), Times.Never);
        Assert.Equal(53.1, existing.Latitude);
        Assert.Equal(-113.1, existing.Longitude);
    }

    [Fact]
    public async Task UpdateVenueCoordinatesAsyncManualSourceCanOverwriteAutomaticCoordinate()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        VenueRecord existing = MakeVenueRecord(1, latitude: 53.1, longitude: -113.1, coordinateSource: "OpenRouteService");
        _ = mockDb.Setup(db => db.LoadAsync<VenueRecord>("VENUE#1", "METADATA", default))
                  .Returns(Task.FromResult(existing));
        VenueRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<VenueRecord>(), default))
                  .Callback<VenueRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        bool result = await repo.UpdateVenueCoordinatesAsync(1, 53.9, -113.9, FringeRepository.ManualCoordinateSource, DateTime.UtcNow).ConfigureAwait(true);

        Assert.True(result);
        Assert.NotNull(saved);
        Assert.Equal(53.9, saved!.Latitude);
        Assert.Equal(FringeRepository.ManualCoordinateSource, saved.CoordinateSource);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetShowTimesForShowAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetShowTimesForShowAsyncReturnsTimesForShow()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var times = new List<ShowTimeRecord>
        {
            new() { Pk = "SHOW#1", Sk = "SHOWTIME#2025-07-15T19:00:00Z", DateTime = "2025-07-15T19:00:00Z", PerformanceDate = "", PerformanceTime = "", PresentationFormat = "" }
        };
        _ = SetupSearch(mockDb, times);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<ShowTimeRecord> result = await repo.GetShowTimesForShowAsync(1).ConfigureAwait(true);

        _ = Assert.Single(result);
        Assert.Equal("2025-07-15T19:00:00Z", result[0].DateTime);
    }

    [Fact]
    public async Task GetShowTimesForShowAsyncNoTimesReturnsEmptyList()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = SetupSearch(mockDb, new List<ShowTimeRecord>());
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<ShowTimeRecord> result = await repo.GetShowTimesForShowAsync(99).ConfigureAwait(true);

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpsertVoteAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertVoteAsyncSavesWithCorrectPkAndSk()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        UserVoteRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserVoteRecord>(), default))
                  .Callback<UserVoteRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpsertVoteAsync("alice", 42, 3).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("USER#alice", saved!.Pk);
        Assert.Equal("VOTE#SHOW#42", saved.Sk);
        Assert.Equal(3, saved.Score);
        Assert.NotEmpty(saved.UpdatedAt);
    }

    [Fact]
    public async Task UpsertVoteAsyncUpdatedAtIsUtcNowFormat()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        UserVoteRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserVoteRecord>(), default))
                  .Callback<UserVoteRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpsertVoteAsync("user1", 1, 1).ConfigureAwait(true);

        Assert.True(DateTime.TryParse(saved!.UpdatedAt, out _));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DeleteVotesAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteVotesAsyncDeletesCorrectKeys()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var deletedKeys = new List<(object pk, object sk)>();
        var mockBatch = new Mock<IBatchWrite<UserVoteRecord>>();
        _ = mockBatch.Setup(b => b.AddDeleteKey(It.IsAny<object>(), It.IsAny<object>()))
                     .Callback<object, object>((pk, sk) => deletedKeys.Add((pk, sk)));
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<UserVoteRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.DeleteVotesAsync("bob", [10, 20]).ConfigureAwait(true);

        Assert.Contains(("USER#bob", "VOTE#SHOW#10"), deletedKeys.Select(k => ((string)k.pk, (string)k.sk)));
        Assert.Contains(("USER#bob", "VOTE#SHOW#20"), deletedKeys.Select(k => ((string)k.pk, (string)k.sk)));
    }

    [Fact]
    public async Task DeleteVotesAsyncEmptyListExecutesBatchWithNoDeletes()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        Mock<IBatchWrite<UserVoteRecord>> mockBatch = SetupBatchWrite<UserVoteRecord>(mockDb);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.DeleteVotesAsync("user1", []).ConfigureAwait(true);

        mockBatch.Verify(b => b.AddDeleteKey(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        mockBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetVotesForUserAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVotesForUserAsyncReturnsUserVotes()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var votes = new List<UserVoteRecord>
        {
            new() { Pk = "USER#u1", Sk = "VOTE#SHOW#1", Score = 1, UpdatedAt = "" },
            new() { Pk = "USER#u1", Sk = "VOTE#SHOW#2", Score = 2, UpdatedAt = "" },
        };
        _ = SetupSearch(mockDb, votes);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<UserVoteRecord> result = await repo.GetVotesForUserAsync("u1").ConfigureAwait(true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetVotesForUserAsyncNoVotesReturnsEmptyList()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = SetupSearch(mockDb, new List<UserVoteRecord>());
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<UserVoteRecord> result = await repo.GetVotesForUserAsync("u1").ConfigureAwait(true);

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpsertUserAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertUserAsyncCallsSaveAsync()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        UserRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default))
                  .Callback<UserRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "User" };
        await repo.UpsertUserAsync(user).ConfigureAwait(true);

        Assert.Same(user, saved);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateDisplayNameAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDisplayNameAsyncUserExistsUpdatesNameAndSaves()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "Old", GroupId = null };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));
        UserRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default))
                  .Callback<UserRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpdateDisplayNameAsync("u1", "New Name").ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("New Name", saved!.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncUserNotFoundDoesNotThrow()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#missing", "PROFILE", default))
                  .Returns(Task.FromResult<UserRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpdateDisplayNameAsync("missing", "Any Name").ConfigureAwait(true);
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncUserInGroupAlsoUpdatesGroupMemberRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "Old", GroupId = "grp1" };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));

        var member = new GroupMemberRecord { Pk = "GROUP#grp1", Sk = "MEMBER#u1", UserId = "u1", DisplayName = "Old", Email = "u@e.com", JoinedAt = "" };
        _ = mockDb.Setup(db => db.LoadAsync<GroupMemberRecord>("GROUP#grp1", "MEMBER#u1", default))
                  .Returns(Task.FromResult(member));

        var savedRecords = new List<object>();
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default))
                  .Callback<UserRecord, CancellationToken>((r, _) => savedRecords.Add(r))
                  .Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<GroupMemberRecord>(), default))
                  .Callback<GroupMemberRecord, CancellationToken>((r, _) => savedRecords.Add(r))
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpdateDisplayNameAsync("u1", "Updated Name").ConfigureAwait(true);

        UserRecord? savedUser = savedRecords.OfType<UserRecord>().FirstOrDefault();
        GroupMemberRecord? savedMember = savedRecords.OfType<GroupMemberRecord>().FirstOrDefault();
        Assert.NotNull(savedUser);
        Assert.NotNull(savedMember);
        Assert.Equal("Updated Name", savedUser!.DisplayName);
        Assert.Equal("Updated Name", savedMember!.DisplayName);
    }

    [Fact]
    public async Task UpdateDisplayNameAsyncUserInGroupMemberRecordNullNoThrow()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "Old", GroupId = "grp1" };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));
        _ = mockDb.Setup(db => db.LoadAsync<GroupMemberRecord>("GROUP#grp1", "MEMBER#u1", default))
                  .Returns(Task.FromResult<GroupMemberRecord>(null!));
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default)).Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.UpdateDisplayNameAsync("u1", "New Name").ConfigureAwait(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetUserAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAsyncExistingUserReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "User" };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));
        FringeRepository repo = BuildRepo(mockDb.Object);

        UserRecord? result = await repo.GetUserAsync("u1").ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal("u@e.com", result!.Email);
    }

    [Fact]
    public async Task GetUserAsyncNonExistentUserReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<UserRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        UserRecord? result = await repo.GetUserAsync("nobody").ConfigureAwait(true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUserAsyncLoadsCorrectKeys()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<UserRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetUserAsync("alice").ConfigureAwait(true);

        mockDb.Verify(db => db.LoadAsync<UserRecord>("USER#alice", "PROFILE", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // DeleteUserDataAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteUserDataAsyncUserWithVotesAndGroupDeletesAll()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();

        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "User", GroupId = "grp1" };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));

        var votes = new List<UserVoteRecord>
        {
            new() { Pk = "USER#u1", Sk = "VOTE#SHOW#1", Score = 1, UpdatedAt = "" }
        };
        _ = SetupSearch(mockDb, votes);

        var mockVoteBatch = new Mock<IBatchWrite<UserVoteRecord>>();
        _ = mockVoteBatch.Setup(b => b.AddDeleteItem(It.IsAny<UserVoteRecord>()));
        _ = mockVoteBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<UserVoteRecord>()).Returns(mockVoteBatch.Object);

        _ = mockDb.Setup(db => db.DeleteAsync<GroupMemberRecord>("GROUP#grp1", "MEMBER#u1", default))
                  .Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.DeleteAsync(It.IsAny<UserRecord>(), default))
                  .Returns(Task.CompletedTask);

        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.DeleteUserDataAsync("u1").ConfigureAwait(true);

        mockVoteBatch.Verify(b => b.AddDeleteItem(It.IsAny<UserVoteRecord>()), Times.Once);
        mockVoteBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
        mockDb.Verify(db => db.DeleteAsync<GroupMemberRecord>("GROUP#grp1", "MEMBER#u1", default), Times.Once);
        mockDb.Verify(db => db.DeleteAsync(It.IsAny<UserRecord>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteUserDataAsyncUserWithNoVotesOrGroupOnlyDeletesUser()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var user = new UserRecord { Pk = "USER#u1", Email = "u@e.com", DisplayName = "User", GroupId = null };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(user));
        _ = SetupSearch(mockDb, new List<UserVoteRecord>());
        _ = mockDb.Setup(db => db.DeleteAsync(It.IsAny<UserRecord>(), default)).Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.DeleteUserDataAsync("u1").ConfigureAwait(true);

        mockDb.Verify(db => db.DeleteAsync<GroupMemberRecord>(It.IsAny<string>(), It.IsAny<string>(), default), Times.Never);
        mockDb.Verify(db => db.DeleteAsync(It.IsAny<UserRecord>(), default), Times.Once);
    }

    [Fact]
    public async Task DeleteUserDataAsyncUserRecordNotFoundDoesNotThrow()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#missing", "PROFILE", default))
                  .Returns(Task.FromResult<UserRecord>(null!));
        _ = SetupSearch(mockDb, new List<UserVoteRecord>());
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.DeleteUserDataAsync("missing").ConfigureAwait(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateGroupAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroupAsyncSavesGroupAndInviteCodeRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var savedObjects = new List<object>();
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<GroupRecord>(), default))
                  .Callback<GroupRecord, CancellationToken>((r, _) => savedObjects.Add(r))
                  .Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<InviteCodeRecord>(), default))
                  .Callback<InviteCodeRecord, CancellationToken>((r, _) => savedObjects.Add(r))
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var group = new GroupRecord
        {
            Pk = "GROUP#grp1",
            GroupId = "grp1",
            Name = "Test Group",
            OwnerId = "owner",
            InviteCode = "ABC123",
            CreatedAt = DateTime.UtcNow.ToString("O")
        };
        await repo.CreateGroupAsync(group).ConfigureAwait(true);

        GroupRecord? savedGroup = savedObjects.OfType<GroupRecord>().FirstOrDefault();
        InviteCodeRecord? savedCode = savedObjects.OfType<InviteCodeRecord>().FirstOrDefault();
        Assert.NotNull(savedGroup);
        Assert.NotNull(savedCode);
        Assert.Equal("INVITE#ABC123", savedCode!.Pk);
        Assert.Equal("grp1", savedCode.GroupId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetGroupAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGroupAsyncExistingGroupReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var group = new GroupRecord { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Group", OwnerId = "o", InviteCode = "X", CreatedAt = "" };
        _ = mockDb.Setup(db => db.LoadAsync<GroupRecord>("GROUP#grp1", "METADATA", default))
                  .Returns(Task.FromResult(group));
        FringeRepository repo = BuildRepo(mockDb.Object);

        GroupRecord? result = await repo.GetGroupAsync("grp1").ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal("grp1", result!.GroupId);
    }

    [Fact]
    public async Task GetGroupAsyncNonExistentGroupReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<GroupRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<GroupRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        GroupRecord? result = await repo.GetGroupAsync("missing").ConfigureAwait(true);

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetGroupByInviteCodeAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGroupByInviteCodeAsyncValidCodeReturnsGroup()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var codeRecord = new InviteCodeRecord { Pk = "INVITE#ABC123", GroupId = "grp1" };
        _ = mockDb.Setup(db => db.LoadAsync<InviteCodeRecord>("INVITE#ABC123", "METADATA", default))
                  .Returns(Task.FromResult(codeRecord));
        var group = new GroupRecord { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Group", OwnerId = "o", InviteCode = "ABC123", CreatedAt = "" };
        _ = mockDb.Setup(db => db.LoadAsync<GroupRecord>("GROUP#grp1", "METADATA", default))
                  .Returns(Task.FromResult(group));
        FringeRepository repo = BuildRepo(mockDb.Object);

        GroupRecord? result = await repo.GetGroupByInviteCodeAsync("ABC123").ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal("grp1", result!.GroupId);
    }

    [Fact]
    public async Task GetGroupByInviteCodeAsyncInvalidCodeReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<InviteCodeRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<InviteCodeRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        GroupRecord? result = await repo.GetGroupByInviteCodeAsync("BADCODE").ConfigureAwait(true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetGroupByInviteCodeAsyncLoadsInviteCodeWithCorrectPk()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        object? loadedPk = null;
        _ = mockDb.Setup(db => db.LoadAsync<InviteCodeRecord>(It.IsAny<object>(), It.IsAny<object>(), default))
                  .Callback<object, object, CancellationToken>((pk, _, _) => loadedPk = pk)
                  .Returns(Task.FromResult<InviteCodeRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetGroupByInviteCodeAsync("XYZ789").ConfigureAwait(true);

        Assert.Equal("INVITE#XYZ789", loadedPk);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetGroupMembersAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGroupMembersAsyncReturnsMembersForGroup()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var members = new List<GroupMemberRecord>
        {
            new() { Pk = "GROUP#grp1", Sk = "MEMBER#u1", UserId = "u1", DisplayName = "Alice", Email = "a@e.com", JoinedAt = "" },
            new() { Pk = "GROUP#grp1", Sk = "MEMBER#u2", UserId = "u2", DisplayName = "Bob", Email = "b@e.com", JoinedAt = "" },
        };
        _ = SetupSearch(mockDb, members);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<GroupMemberRecord> result = await repo.GetGroupMembersAsync("grp1").ConfigureAwait(true);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetGroupMembersAsyncEmptyGroupReturnsEmptyList()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = SetupSearch(mockDb, new List<GroupMemberRecord>());
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<GroupMemberRecord> result = await repo.GetGroupMembersAsync("empty-grp").ConfigureAwait(true);

        Assert.Empty(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // JoinGroupAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGroupAsyncCreatesMemberRecordAndUpdatesUser()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var savedObjects = new List<object>();

        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult<UserRecord>(null!));
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<GroupMemberRecord>(), default))
                  .Callback<GroupMemberRecord, CancellationToken>((r, _) => savedObjects.Add(r))
                  .Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default))
                  .Callback<UserRecord, CancellationToken>((r, _) => savedObjects.Add(r))
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.JoinGroupAsync("grp1", "u1", "Alice", "alice@e.com").ConfigureAwait(true);

        GroupMemberRecord? savedMember = savedObjects.OfType<GroupMemberRecord>().FirstOrDefault();
        UserRecord? savedUser = savedObjects.OfType<UserRecord>().FirstOrDefault();

        Assert.NotNull(savedMember);
        Assert.Equal("GROUP#grp1", savedMember!.Pk);
        Assert.Equal("MEMBER#u1", savedMember.Sk);
        Assert.Equal("u1", savedMember.UserId);
        Assert.Equal("Alice", savedMember.DisplayName);

        Assert.NotNull(savedUser);
        Assert.Equal("grp1", savedUser!.GroupId);
    }

    [Fact]
    public async Task JoinGroupAsyncExistingUserPreservesExistingUserRecordAndSetsGroupId()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var existingUser = new UserRecord
        {
            Pk = "USER#u1",
            Email = "alice@e.com",
            DisplayName = "Alice",
            GroupId = null
        };
        _ = mockDb.Setup(db => db.LoadAsync<UserRecord>("USER#u1", "PROFILE", default))
                  .Returns(Task.FromResult(existingUser));

        UserRecord? savedUser = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<GroupMemberRecord>(), default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserRecord>(), default))
                  .Callback<UserRecord, CancellationToken>((r, _) => savedUser = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.JoinGroupAsync("grp1", "u1", "Alice", "alice@e.com").ConfigureAwait(true);

        Assert.NotNull(savedUser);
        Assert.Same(existingUser, savedUser);
        Assert.Equal("grp1", savedUser!.GroupId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetAvailabilityAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityAsyncExistingRecordReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var record = new UserAvailabilityRecord { Pk = "USER#u1", Sk = "AVAILABILITY" };
        record.Windows.Add(new() { Start = "2025-07-15T09:00:00Z", End = "2025-07-15T17:00:00Z" });
        _ = mockDb.Setup(db => db.LoadAsync<UserAvailabilityRecord>("USER#u1", "AVAILABILITY", default))
                  .Returns(Task.FromResult(record));
        FringeRepository repo = BuildRepo(mockDb.Object);

        UserAvailabilityRecord? result = await repo.GetAvailabilityAsync("u1").ConfigureAwait(true);

        Assert.NotNull(result);
        _ = Assert.Single(result!.Windows);
    }

    [Fact]
    public async Task GetAvailabilityAsyncNotFoundReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserAvailabilityRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<UserAvailabilityRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        UserAvailabilityRecord? result = await repo.GetAvailabilityAsync("u1").ConfigureAwait(true);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAvailabilityAsyncLoadsCorrectKeys()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<UserAvailabilityRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<UserAvailabilityRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetAvailabilityAsync("alice").ConfigureAwait(true);

        mockDb.Verify(db => db.LoadAsync<UserAvailabilityRecord>("USER#alice", "AVAILABILITY", default), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveAvailabilityAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAvailabilityAsyncSavesCorrectRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        UserAvailabilityRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserAvailabilityRecord>(), default))
                  .Callback<UserAvailabilityRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var windows = new Collection<AvailabilityWindowData>
        {
            new() { Start = "2025-07-15T09:00:00Z", End = "2025-07-15T17:00:00Z" }
        };
        await repo.SaveAvailabilityAsync("alice", windows).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("USER#alice", saved!.Pk);
        Assert.Equal("AVAILABILITY", saved.Sk);
        _ = Assert.Single(saved.Windows);
        Assert.Equal("2025-07-15T09:00:00Z", saved.Windows[0].Start);
    }

    [Fact]
    public async Task SaveAvailabilityAsyncEmptyWindowsSavesEmptyList()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        UserAvailabilityRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<UserAvailabilityRecord>(), default))
                  .Callback<UserAvailabilityRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveAvailabilityAsync("alice", []).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Empty(saved!.Windows);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ToShowTimeRecord mapping (tested via SaveShowTimesAsync)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveShowTimesAsyncSkFormatIsShowtimePrefix()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        ShowTimeRecord? saved = null;
        var mockBatch = new Mock<IBatchWrite<ShowTimeRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<ShowTimeRecord>()))
                     .Callback<ShowTimeRecord>(r => saved = r);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<ShowTimeRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var dt = new DateTime(2025, 7, 15, 19, 0, 0, DateTimeKind.Utc);
        await repo.SaveShowTimesAsync([new ShowTime
        {
            ShowId = 10,
            DateTime = dt,
            PerformanceTime = new TimeOnly(19, 0),
            PerformanceDate = "Tue",
            PresentationFormat = "Std",
            Reserved = false
        }]).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.StartsWith("SHOWTIME#", saved!.Sk, StringComparison.Ordinal);
        Assert.Contains(dt.ToString("O"), saved.Sk, StringComparison.Ordinal);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // SaveTransferMatrixAsync
    // ─────────────────────────────────────────────────────────────────────────

    private static TransferMatrixVersion MakeMatrixVersion(string inputHash = "hash-1", int pairCount = 2)
    {
        TransferMatrixVersion version = new()
        {
            InputHash = inputHash,
            VenueCount = 2,
            GeneratedAt = new DateTime(2026, 7, 19, 3, 0, 0, DateTimeKind.Utc),
            Source = "OpenRouteService"
        };
        for (int i = 0; i < pairCount; i++)
        {
            version.Pairs.Add(new TransferPair
            {
                FromVenueNumber = i + 1,
                ToVenueNumber = i + 2,
                WalkingDurationSeconds = 300,
                WalkingDistanceMeters = 400,
                CyclingDurationSeconds = 120,
                CyclingDistanceMeters = 400,
                DrivingDurationSeconds = 90,
                DrivingDistanceMeters = 500,
                Source = "OpenRouteService"
            });
        }
        return version;
    }

    [Fact]
    public async Task SaveTransferMatrixAsyncSavesMetadataRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        TransferMatrixMetadataRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<TransferMatrixMetadataRecord>(), default))
                  .Callback<TransferMatrixMetadataRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        Mock<IBatchWrite<TransferMatrixPairRecord>> mockBatch = SetupBatchWrite<TransferMatrixPairRecord>(mockDb);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveTransferMatrixAsync(MakeMatrixVersion("hash-1", pairCount: 2)).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("TRANSFER_MATRIX#hash-1", saved!.Pk);
        Assert.Equal("METADATA", saved.Sk);
        Assert.Equal("hash-1", saved.InputHash);
        Assert.Equal(2, saved.VenueCount);
        Assert.Equal(2, saved.PairCount);
        Assert.Equal("OpenRouteService", saved.Source);
        mockBatch.Verify(b => b.ExecuteAsync(default), Times.Once);
    }

    [Fact]
    public async Task SaveTransferMatrixAsyncSavesOnePairRecordPerPairViaBatch()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<TransferMatrixMetadataRecord>(), default)).Returns(Task.CompletedTask);
        var putItems = new List<TransferMatrixPairRecord>();
        var mockBatch = new Mock<IBatchWrite<TransferMatrixPairRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<TransferMatrixPairRecord>()))
                     .Callback<TransferMatrixPairRecord>(putItems.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<TransferMatrixPairRecord>()).Returns(mockBatch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.SaveTransferMatrixAsync(MakeMatrixVersion("hash-1", pairCount: 3)).ConfigureAwait(true);

        Assert.Equal(3, putItems.Count);
        Assert.All(putItems, p => Assert.Equal("TRANSFER_MATRIX#hash-1", p.Pk));
        Assert.Contains(putItems, p => p.Sk == "FROM#1#TO#2" && p.FromVenueNumber == 1 && p.ToVenueNumber == 2);
        Assert.Contains(putItems, p => p.WalkingDurationSeconds == 300 && p.DrivingDistanceMeters == 500);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetTransferMatrixPairsAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransferMatrixPairsAsyncQueriesByPartitionKey()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        QueryOperationConfig? capturedConfig = null;
        var mockSearch = new Mock<IAsyncSearch<TransferMatrixPairRecord>>();
        _ = mockSearch.Setup(s => s.GetRemainingAsync(default)).ReturnsAsync([]);
        _ = mockDb.Setup(db => db.FromQueryAsync<TransferMatrixPairRecord>(It.IsAny<QueryOperationConfig>()))
                  .Callback<QueryOperationConfig>(cfg => capturedConfig = cfg)
                  .Returns(mockSearch.Object);
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetTransferMatrixPairsAsync("hash-1").ConfigureAwait(true);

        Assert.NotNull(capturedConfig);
        Assert.Equal("TRANSFER_MATRIX#hash-1", ((Primitive)capturedConfig!.KeyExpression.ExpressionAttributeValues[":pk"]).Value);
    }

    [Fact]
    public async Task GetTransferMatrixPairsAsyncExcludesMetadataItem()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var items = new List<TransferMatrixPairRecord>
        {
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "METADATA" },
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "FROM#1#TO#2", FromVenueNumber = 1, ToVenueNumber = 2, Source = "OpenRouteService" },
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "FROM#2#TO#1", FromVenueNumber = 2, ToVenueNumber = 1, Source = "OpenRouteService" }
        };
        _ = SetupSearch(mockDb, items, viaFromQuery: true);
        FringeRepository repo = BuildRepo(mockDb.Object);

        List<TransferMatrixPairRecord> result = await repo.GetTransferMatrixPairsAsync("hash-1").ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.StartsWith("FROM#", p.Sk, StringComparison.Ordinal));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetTransferMatrixMetadataAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransferMatrixMetadataAsyncExistingVersionReturnsRecord()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var record = new TransferMatrixMetadataRecord { Pk = "TRANSFER_MATRIX#hash-1", InputHash = "hash-1", GeneratedAt = "", Source = "OpenRouteService" };
        _ = mockDb.Setup(db => db.LoadAsync<TransferMatrixMetadataRecord>("TRANSFER_MATRIX#hash-1", "METADATA", default))
                  .Returns(Task.FromResult(record));
        FringeRepository repo = BuildRepo(mockDb.Object);

        TransferMatrixMetadataRecord? result = await repo.GetTransferMatrixMetadataAsync("hash-1").ConfigureAwait(true);

        Assert.NotNull(result);
        Assert.Equal("hash-1", result!.InputHash);
    }

    [Fact]
    public async Task GetTransferMatrixMetadataAsyncMissingVersionReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<TransferMatrixMetadataRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<TransferMatrixMetadataRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        TransferMatrixMetadataRecord? result = await repo.GetTransferMatrixMetadataAsync("missing").ConfigureAwait(true);

        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetActiveTransferMatrixPointerAsync / SetActiveTransferMatrixAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetActiveTransferMatrixPointerAsyncLoadsConfigKey()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<ActiveTransferMatrixRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<ActiveTransferMatrixRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        _ = await repo.GetActiveTransferMatrixPointerAsync().ConfigureAwait(true);

        mockDb.Verify(db => db.LoadAsync<ActiveTransferMatrixRecord>("CONFIG", "ACTIVE_TRANSFER_MATRIX", default), Times.Once);
    }

    [Fact]
    public async Task GetActiveTransferMatrixPointerAsyncNoVersionEverPublishedReturnsNull()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<ActiveTransferMatrixRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<ActiveTransferMatrixRecord>(null!));
        FringeRepository repo = BuildRepo(mockDb.Object);

        ActiveTransferMatrixRecord? result = await repo.GetActiveTransferMatrixPointerAsync().ConfigureAwait(true);

        Assert.Null(result);
    }

    [Fact]
    public async Task SetActiveTransferMatrixAsyncSavesPointerWithHashAndTimestamp()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        ActiveTransferMatrixRecord? saved = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<ActiveTransferMatrixRecord>(), default))
                  .Callback<ActiveTransferMatrixRecord, CancellationToken>((r, _) => saved = r)
                  .Returns(Task.CompletedTask);
        FringeRepository repo = BuildRepo(mockDb.Object);

        var promotedAt = new DateTime(2026, 7, 19, 4, 0, 0, DateTimeKind.Utc);
        await repo.SetActiveTransferMatrixAsync("hash-2", promotedAt).ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal("CONFIG", saved!.Pk);
        Assert.Equal("ACTIVE_TRANSFER_MATRIX", saved.Sk);
        Assert.Equal("hash-2", saved.InputHash);
        Assert.Equal(promotedAt.ToString("O"), saved.PromotedAt);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // MarkTransferMatrixStaleAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkTransferMatrixStaleAsyncSetsTtlOnMetadataAndAllPairs()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        var metadata = new TransferMatrixMetadataRecord { Pk = "TRANSFER_MATRIX#hash-1", InputHash = "hash-1", GeneratedAt = "", Source = "OpenRouteService" };
        _ = mockDb.Setup(db => db.LoadAsync<TransferMatrixMetadataRecord>("TRANSFER_MATRIX#hash-1", "METADATA", default))
                  .Returns(Task.FromResult(metadata));

        var pairs = new List<TransferMatrixPairRecord>
        {
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "FROM#1#TO#2", FromVenueNumber = 1, ToVenueNumber = 2, Source = "OpenRouteService" },
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "FROM#2#TO#1", FromVenueNumber = 2, ToVenueNumber = 1, Source = "OpenRouteService" }
        };
        _ = SetupSearch(mockDb, pairs, viaFromQuery: true);

        TransferMatrixMetadataRecord? savedMetadata = null;
        _ = mockDb.Setup(db => db.SaveAsync(It.IsAny<TransferMatrixMetadataRecord>(), default))
                  .Callback<TransferMatrixMetadataRecord, CancellationToken>((r, _) => savedMetadata = r)
                  .Returns(Task.CompletedTask);

        var savedPairs = new List<TransferMatrixPairRecord>();
        var mockBatch = new Mock<IBatchWrite<TransferMatrixPairRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<TransferMatrixPairRecord>()))
                     .Callback<TransferMatrixPairRecord>(savedPairs.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<TransferMatrixPairRecord>()).Returns(mockBatch.Object);

        FringeRepository repo = BuildRepo(mockDb.Object);
        var expiresAt = new DateTime(2026, 8, 18, 3, 0, 0, DateTimeKind.Utc);

        await repo.MarkTransferMatrixStaleAsync("hash-1", expiresAt).ConfigureAwait(true);

        long expectedTtl = ((DateTimeOffset)expiresAt).ToUnixTimeSeconds();
        Assert.NotNull(savedMetadata);
        Assert.Equal(expectedTtl, savedMetadata!.Ttl);
        Assert.Equal(2, savedPairs.Count);
        Assert.All(savedPairs, p => Assert.Equal(expectedTtl, p.Ttl));
    }

    [Fact]
    public async Task MarkTransferMatrixStaleAsyncMissingMetadataStillMarksPairs()
    {
        Mock<IDynamoDBContext> mockDb = BuildMockDb();
        _ = mockDb.Setup(db => db.LoadAsync<TransferMatrixMetadataRecord>(It.IsAny<string>(), It.IsAny<string>(), default))
                  .Returns(Task.FromResult<TransferMatrixMetadataRecord>(null!));

        var pairs = new List<TransferMatrixPairRecord>
        {
            new() { Pk = "TRANSFER_MATRIX#hash-1", Sk = "FROM#1#TO#2", FromVenueNumber = 1, ToVenueNumber = 2, Source = "OpenRouteService" }
        };
        _ = SetupSearch(mockDb, pairs, viaFromQuery: true);

        var savedPairs = new List<TransferMatrixPairRecord>();
        var mockBatch = new Mock<IBatchWrite<TransferMatrixPairRecord>>();
        _ = mockBatch.Setup(b => b.AddPutItem(It.IsAny<TransferMatrixPairRecord>()))
                     .Callback<TransferMatrixPairRecord>(savedPairs.Add);
        _ = mockBatch.Setup(b => b.ExecuteAsync(default)).Returns(Task.CompletedTask);
        _ = mockDb.Setup(db => db.CreateBatchWrite<TransferMatrixPairRecord>()).Returns(mockBatch.Object);

        FringeRepository repo = BuildRepo(mockDb.Object);

        await repo.MarkTransferMatrixStaleAsync("hash-1", DateTime.UtcNow.AddDays(30)).ConfigureAwait(true);

        mockDb.Verify(db => db.SaveAsync(It.IsAny<TransferMatrixMetadataRecord>(), default), Times.Never);
        _ = Assert.Single(savedPairs);
    }
}
