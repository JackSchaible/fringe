using Amazon.DynamoDBv2.DataModel;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using FringeScraper.Services;
using Moq;
using Xunit;

namespace Fringe.Scraper.Tests.Services;

/// <summary>
/// Tests for VenueEnrichmentService.EnrichAsync, using a mocked <see cref="IGeocodingProvider"/>
/// (a fake — no real network calls) and a partial mock of <see cref="FringeRepository"/>, the
/// same pattern used by DatabaseInserterTests. requestDelay is always TimeSpan.Zero so tests
/// don't pay the real inter-request rate-limiting pause.
/// </summary>
public sealed class VenueEnrichmentServiceTests
{
    private static VenueRecord MakeVenue(int venueNumber = 1, string name = "Main Stage")
    {
        return new VenueRecord
        {
            Pk = $"VENUE#{venueNumber}",
            VenueNumber = venueNumber,
            Name = name,
            Address = $"{venueNumber} Street",
            Phone = "555-0000",
            PostalCode = "T0T 0T0"
        };
    }

    private static Mock<FringeRepository> MockRepository(List<VenueRecord> venuesNeedingGeocoding)
    {
        var dbContext = new Mock<IDynamoDBContext>();
        var repoMock = new Mock<FringeRepository>(dbContext.Object) { CallBase = false };
        _ = repoMock.Setup(r => r.GetVenuesNeedingGeocodingAsync()).ReturnsAsync(venuesNeedingGeocoding);
        _ = repoMock
            .Setup(r => r.UpdateVenueCoordinatesAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<DateTime>()))
            .ReturnsAsync(true);
        return repoMock;
    }

    [Fact]
    public async Task EnrichAsyncNoVenuesNeedingGeocodingProviderNeverCalled()
    {
        Mock<FringeRepository> repoMock = MockRepository([]);
        var providerMock = new Mock<IGeocodingProvider>();
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await service.EnrichAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GeocodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrichAsyncSuccessfulGeocodeUpdatesVenueCoordinates()
    {
        Mock<FringeRepository> repoMock = MockRepository([MakeVenue(1)]);
        var providerMock = new Mock<IGeocodingProvider>();
        _ = providerMock
            .Setup(p => p.GeocodeAsync("1 Street", "T0T 0T0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeocodeOutcome.Succeeded(53.5461, -113.4938));
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await service.EnrichAsync().ConfigureAwait(true);

        repoMock.Verify(
            r => r.UpdateVenueCoordinatesAsync(1, 53.5461, -113.4938, "OpenRouteService", It.IsAny<DateTime>()),
            Times.Once);
    }

    [Fact]
    public async Task EnrichAsyncFailedGeocodeDoesNotUpdateVenueCoordinates()
    {
        Mock<FringeRepository> repoMock = MockRepository([MakeVenue(1)]);
        var providerMock = new Mock<IGeocodingProvider>();
        _ = providerMock
            .Setup(p => p.GeocodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeocodeOutcome.Failed("Low confidence match (0.20)."));
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await service.EnrichAsync().ConfigureAwait(true);

        repoMock.Verify(
            r => r.UpdateVenueCoordinatesAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Never);
    }

    [Fact]
    public async Task EnrichAsyncMultipleVenuesGeocodesEachOne()
    {
        Mock<FringeRepository> repoMock = MockRepository([MakeVenue(1), MakeVenue(2), MakeVenue(3)]);
        var providerMock = new Mock<IGeocodingProvider>();
        _ = providerMock
            .Setup(p => p.GeocodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeocodeOutcome.Succeeded(53.5, -113.5));
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await service.EnrichAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GeocodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        repoMock.Verify(
            r => r.UpdateVenueCoordinatesAsync(It.IsAny<int>(), It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<DateTime>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task EnrichAsyncOneVenueThrowsOtherVenuesStillProcessed()
    {
        Mock<FringeRepository> repoMock = MockRepository([MakeVenue(1, "Venue One"), MakeVenue(2, "Venue Two")]);
        var providerMock = new Mock<IGeocodingProvider>();
        _ = providerMock
            .Setup(p => p.GeocodeAsync("1 Street", "T0T 0T0", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network error"));
        _ = providerMock
            .Setup(p => p.GeocodeAsync("2 Street", "T0T 0T0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GeocodeOutcome.Succeeded(53.5, -113.5));
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        // Should not throw — a single venue's failure must not abort the batch, and venue 2
        // (a different address) must still get geocoded and saved.
        await service.EnrichAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GeocodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        repoMock.Verify(r => r.UpdateVenueCoordinatesAsync(1, It.IsAny<double>(), It.IsAny<double>(), It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
        repoMock.Verify(r => r.UpdateVenueCoordinatesAsync(2, 53.5, -113.5, "OpenRouteService", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task EnrichAsyncNoVenuesNeedingGeocodingDoesNotThrow()
    {
        Mock<FringeRepository> repoMock = MockRepository([]);
        var providerMock = new Mock<IGeocodingProvider>();
        var service = new VenueEnrichmentService(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await service.EnrichAsync().ConfigureAwait(true);
    }
}
