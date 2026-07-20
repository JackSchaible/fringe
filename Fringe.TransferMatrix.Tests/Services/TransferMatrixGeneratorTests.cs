using Amazon.DynamoDBv2.DataModel;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;
using FringeTransferMatrix.Services;
using Moq;
using Xunit;

namespace Fringe.TransferMatrix.Tests.Services;

/// <summary>
/// Tests for TransferMatrixGenerator.GenerateAsync, using a mocked <see cref="IMatrixProvider"/>
/// (a fake — no real network calls) and a partial mock of <see cref="FringeRepository"/>, the
/// same pattern used by Fringe.Scraper.Tests' VenueEnrichmentServiceTests. requestDelay is
/// always TimeSpan.Zero so tests don't pay the real inter-request rate-limiting pause.
/// </summary>
public sealed class TransferMatrixGeneratorTests
{
    private static VenueRecord MakeVenue(int venueNumber, double? latitude, double? longitude)
    {
        return new VenueRecord
        {
            Pk = $"VENUE#{venueNumber}",
            VenueNumber = venueNumber,
            Name = $"Venue {venueNumber}",
            Address = "",
            Phone = "",
            PostalCode = "",
            Latitude = latitude,
            Longitude = longitude
        };
    }

    /// <summary>Builds a successful NxN outcome where duration/distance are a deterministic function of (i, j), so directionality is provable.</summary>
    private static MatrixOutcome MakeOutcome(int n, double baseValue)
    {
        List<IReadOnlyList<double?>> matrix = [];
        for (int i = 0; i < n; i++)
        {
            List<double?> row = [];
            for (int j = 0; j < n; j++)
            {
                row.Add(i == j ? 0 : baseValue + (i * 10) + j);
            }
            matrix.Add(row);
        }
        return MatrixOutcome.Succeeded(matrix, matrix);
    }

    private static Mock<FringeRepository> MockRepository(List<VenueRecord> venues, ActiveTransferMatrixRecord? active)
    {
        var dbContext = new Mock<IDynamoDBContext>();
        var repoMock = new Mock<FringeRepository>(dbContext.Object) { CallBase = false };
        _ = repoMock.Setup(r => r.GetAllVenuesAsync()).ReturnsAsync(venues);
        _ = repoMock.Setup(r => r.GetActiveTransferMatrixPointerAsync()).ReturnsAsync(active);
        _ = repoMock.Setup(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>())).Returns(Task.CompletedTask);
        _ = repoMock.Setup(r => r.SetActiveTransferMatrixAsync(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        _ = repoMock.Setup(r => r.MarkTransferMatrixStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>())).Returns(Task.CompletedTask);
        return repoMock;
    }

    private static Mock<IMatrixProvider> MockProviderReturning(MatrixOutcome outcome)
    {
        var providerMock = new Mock<IMatrixProvider>();
        _ = providerMock
            .Setup(p => p.GetMatrixAsync(It.IsAny<TravelMode>(), It.IsAny<IReadOnlyList<(int VenueNumber, double Latitude, double Longitude)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(outcome);
        return providerMock;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task GenerateAsyncFewerThanTwoRoutableVenuesSkipsProvider(int routableCount)
    {
        List<VenueRecord> venues = [.. Enumerable.Range(1, routableCount).Select(i => MakeVenue(i, 53.5, -113.5))];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        var providerMock = new Mock<IMatrixProvider>();
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GetMatrixAsync(It.IsAny<TravelMode>(), It.IsAny<IReadOnlyList<(int, double, double)>>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncVenuesWithoutCoordinatesAreExcludedFromRouting()
    {
        List<VenueRecord> venues =
        [
            MakeVenue(1, 53.5, -113.5),
            MakeVenue(2, 53.6, -113.6),
            MakeVenue(3, null, null)
        ];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        TransferMatrixVersion? saved = null;
        _ = repoMock.Setup(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()))
                    .Callback<TransferMatrixVersion>(v => saved = v)
                    .Returns(Task.CompletedTask);
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.VenueCount);
        Assert.Equal(2, saved.Pairs.Count); // 2 venues -> 2*(2-1) = 2 directional pairs, venue 3 excluded
    }

    [Fact]
    public async Task GenerateAsyncNoActiveMatrixGeneratesAndPublishesWithoutMarkingAnythingStale()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Once);
        repoMock.Verify(r => r.SetActiveTransferMatrixAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        repoMock.Verify(r => r.MarkTransferMatrixStaleAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncUnchangedHashAndNotStaleSkipsProviderEntirely()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        string currentHash = TransferMatrixHasher.ComputeHash([(1, 53.5, -113.5), (2, 53.6, -113.6)]);
        var active = new ActiveTransferMatrixRecord { InputHash = currentHash, PromotedAt = DateTime.UtcNow.ToString("O") };
        Mock<FringeRepository> repoMock = MockRepository(venues, active);
        var providerMock = new Mock<IMatrixProvider>();
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GetMatrixAsync(It.IsAny<TravelMode>(), It.IsAny<IReadOnlyList<(int, double, double)>>(), It.IsAny<CancellationToken>()), Times.Never);
        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncChangedHashRegeneratesAndMarksOldVersionStale()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        var active = new ActiveTransferMatrixRecord { InputHash = "stale-hash", PromotedAt = DateTime.UtcNow.ToString("O") };
        Mock<FringeRepository> repoMock = MockRepository(venues, active);
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Once);
        repoMock.Verify(r => r.SetActiveTransferMatrixAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Once);
        repoMock.Verify(r => r.MarkTransferMatrixStaleAsync("stale-hash", It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsyncUnchangedHashButRefreshDueRegeneratesAnyway()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        string currentHash = TransferMatrixHasher.ComputeHash([(1, 53.5, -113.5), (2, 53.6, -113.6)]);
        var active = new ActiveTransferMatrixRecord { InputHash = currentHash, PromotedAt = DateTime.UtcNow.AddDays(-90).ToString("O") };
        Mock<FringeRepository> repoMock = MockRepository(venues, active);
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero, refreshInterval: TimeSpan.FromDays(30));

        await generator.GenerateAsync().ConfigureAwait(true);

        providerMock.Verify(p => p.GetMatrixAsync(It.IsAny<TravelMode>(), It.IsAny<IReadOnlyList<(int, double, double)>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsyncProviderFailureAbortsWithoutPublishing()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        var providerMock = new Mock<IMatrixProvider>();
        _ = providerMock
            .Setup(p => p.GetMatrixAsync(It.IsAny<TravelMode>(), It.IsAny<IReadOnlyList<(int, double, double)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MatrixOutcome.Failed("quota exceeded"));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Never);
        repoMock.Verify(r => r.SetActiveTransferMatrixAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncMissingPairValueAbortsWithoutPublishing()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6)];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);

        List<IReadOnlyList<double?>> matrixWithHole =
        [
            new List<double?> { 0, null }, // missing duration/distance for venue 1 -> venue 2
            new List<double?> { 200, 0 }
        ];
        var outcomeWithHole = MatrixOutcome.Succeeded(matrixWithHole, matrixWithHole);
        Mock<IMatrixProvider> providerMock = MockProviderReturning(outcomeWithHole);
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Never);
        repoMock.Verify(r => r.SetActiveTransferMatrixAsync(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncWrongMatrixDimensionsAbortsWithoutPublishing()
    {
        List<VenueRecord> venues = [MakeVenue(1, 53.5, -113.5), MakeVenue(2, 53.6, -113.6), MakeVenue(3, 53.7, -113.7)];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        // Provider returns a 2x2 matrix when 3x3 was expected (e.g. a malformed/truncated response).
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        repoMock.Verify(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsyncBuildsDirectionalPairsWithCorrectFromToAndValues()
    {
        List<VenueRecord> venues = [MakeVenue(11, 53.5, -113.5), MakeVenue(22, 53.6, -113.6)];
        Mock<FringeRepository> repoMock = MockRepository(venues, active: null);
        TransferMatrixVersion? saved = null;
        _ = repoMock.Setup(r => r.SaveTransferMatrixAsync(It.IsAny<TransferMatrixVersion>()))
                    .Callback<TransferMatrixVersion>(v => saved = v)
                    .Returns(Task.CompletedTask);
        // durations[i][j] = 100 + i*10 + j — so (0,1) = 101 and (1,0) = 110, directionally distinct.
        Mock<IMatrixProvider> providerMock = MockProviderReturning(MakeOutcome(2, 100));
        TransferMatrixGenerator generator = new(repoMock.Object, providerMock.Object, TimeSpan.Zero);

        await generator.GenerateAsync().ConfigureAwait(true);

        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Pairs.Count);
        TransferPair forward = Assert.Single(saved.Pairs, p => p.FromVenueNumber == 11 && p.ToVenueNumber == 22);
        TransferPair backward = Assert.Single(saved.Pairs, p => p.FromVenueNumber == 22 && p.ToVenueNumber == 11);
        Assert.Equal(101, forward.WalkingDurationSeconds);
        Assert.Equal(110, backward.WalkingDurationSeconds);
        Assert.NotEqual(forward.WalkingDurationSeconds, backward.WalkingDurationSeconds);
    }
}
