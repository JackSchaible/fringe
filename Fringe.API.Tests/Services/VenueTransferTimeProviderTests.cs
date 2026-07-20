using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Services;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;
using Moq;

namespace Fringe.API.Tests.Services;

/// <summary>
/// Tests for VenueTransferTimeProvider.GetRequiredGapAsync, covering every precedence branch
/// (same venue &gt; directional override &gt; matrix &gt; missing-data fallback) and the
/// conservative-fallback guarantee. Uses <see cref="MockBehavior.Strict"/> for the repository
/// mock (matching this test project's convention) so an unexpected DynamoDB call — e.g. the
/// same-venue or override branches incorrectly querying the matrix — fails the test immediately.
/// </summary>
public sealed class VenueTransferTimeProviderTests
{
    private const string activeHash = "hash-1";

    private static Mock<FringeRepository> BuildMockRepo()
    {
        return new Mock<FringeRepository>(MockBehavior.Strict, Mock.Of<IDynamoDBContext>());
    }

    private static TransferMatrixPairRecord MakePair(int from, int to, double walkSeconds, double cycleSeconds, double driveSeconds)
    {
        return new TransferMatrixPairRecord
        {
            Pk = $"TRANSFER_MATRIX#{activeHash}",
            Sk = $"FROM#{from}#TO#{to}",
            FromVenueNumber = from,
            ToVenueNumber = to,
            WalkingDurationSeconds = walkSeconds,
            WalkingDistanceMeters = 0,
            CyclingDurationSeconds = cycleSeconds,
            CyclingDistanceMeters = 0,
            DrivingDurationSeconds = driveSeconds,
            DrivingDistanceMeters = 0,
            Source = "OpenRouteService"
        };
    }

    private static void SetupActiveMatrix(Mock<FringeRepository> mockRepo, List<TransferMatrixPairRecord> pairs)
    {
        _ = mockRepo.Setup(r => r.GetActiveTransferMatrixPointerAsync())
                    .ReturnsAsync(new ActiveTransferMatrixRecord { InputHash = activeHash, PromotedAt = DateTime.UtcNow.ToString("O") });
        _ = mockRepo.Setup(r => r.GetTransferMatrixPairsAsync(activeHash)).ReturnsAsync(pairs);
    }

    private static void SetupNoActiveMatrix(Mock<FringeRepository> mockRepo)
    {
        _ = mockRepo.Setup(r => r.GetActiveTransferMatrixPointerAsync()).ReturnsAsync((ActiveTransferMatrixRecord?)null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Same venue
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequiredGapAsyncSameVenueReturnsConfiguredGapWithoutTouchingRepository()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo(); // no setups — Strict mode fails if anything is called
        var options = new TransferPolicyOptions { SameVenueGap = TimeSpan.FromMinutes(3) };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(5, 5, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.SameVenue, result.AppliedRule);
        Assert.Equal(TimeSpan.FromMinutes(3), result.RequiredGap);
        Assert.Null(result.RawDuration);
        Assert.Equal(TimeSpan.Zero, result.Overhead);
    }

    [Fact]
    public async Task GetRequiredGapAsyncSameVenueDefaultsToZeroGap()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        TransferGapResult result = await provider.GetRequiredGapAsync(7, 7, TravelMode.Driving).ConfigureAwait(true);

        Assert.Equal(TimeSpan.Zero, result.RequiredGap);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Directional override
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequiredGapAsyncOverrideAppliesConfiguredDurationPlusOverheadWithoutTouchingRepository()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo(); // no setups — override must short-circuit before any repo call
        var options = new TransferPolicyOptions
        {
            DepartureOverhead = TimeSpan.FromMinutes(2),
            ArrivalOverhead = TimeSpan.FromMinutes(3),
            ReliabilityBuffer = TimeSpan.FromMinutes(1),
            Overrides = { new TransferOverride { FromVenueNumber = 1, ToVenueNumber = 2, Mode = TravelMode.Walking, Duration = TimeSpan.FromMinutes(20) } }
        };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.DirectionalOverride, result.AppliedRule);
        Assert.Equal(TimeSpan.FromMinutes(20), result.RawDuration);
        Assert.Equal(TimeSpan.FromMinutes(6), result.Overhead);
        Assert.Equal(TimeSpan.FromMinutes(26), result.RequiredGap);
    }

    [Fact]
    public async Task GetRequiredGapAsyncOverrideIsDirectional()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo); // reverse direction isn't overridden, falls through to fallback
        var options = new TransferPolicyOptions
        {
            Overrides = { new TransferOverride { FromVenueNumber = 1, ToVenueNumber = 2, Mode = TravelMode.Walking, Duration = TimeSpan.FromMinutes(20) } }
        };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult forward = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        TransferGapResult backward = await provider.GetRequiredGapAsync(2, 1, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.DirectionalOverride, forward.AppliedRule);
        Assert.Equal(TransferRuleApplied.MissingDataFallback, backward.AppliedRule);
    }

    [Fact]
    public async Task GetRequiredGapAsyncOverrideIsModeSpecific()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo); // driving isn't overridden for this pair, falls through to fallback
        var options = new TransferPolicyOptions
        {
            Overrides = { new TransferOverride { FromVenueNumber = 1, ToVenueNumber = 2, Mode = TravelMode.Walking, Duration = TimeSpan.FromMinutes(20) } }
        };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult walking = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        TransferGapResult driving = await provider.GetRequiredGapAsync(1, 2, TravelMode.Driving).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.DirectionalOverride, walking.AppliedRule);
        Assert.Equal(TransferRuleApplied.MissingDataFallback, driving.AppliedRule);
    }

    [Fact]
    public async Task GetRequiredGapAsyncOverrideTakesPrecedenceOverMatrixDataForSamePair()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo(); // no setups — override must win before the matrix is ever consulted
        var options = new TransferPolicyOptions
        {
            Overrides = { new TransferOverride { FromVenueNumber = 1, ToVenueNumber = 2, Mode = TravelMode.Walking, Duration = TimeSpan.FromMinutes(20) } }
        };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.DirectionalOverride, result.AppliedRule);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Matrix
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequiredGapAsyncMatrixUsesDurationForRequestedMode()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo, [MakePair(1, 2, walkSeconds: 600, cycleSeconds: 200, driveSeconds: 90)]);
        var options = new TransferPolicyOptions { DepartureOverhead = TimeSpan.Zero, ArrivalOverhead = TimeSpan.Zero, ReliabilityBuffer = TimeSpan.Zero };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult walking = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        TransferGapResult cycling = await provider.GetRequiredGapAsync(1, 2, TravelMode.Cycling).ConfigureAwait(true);
        TransferGapResult driving = await provider.GetRequiredGapAsync(1, 2, TravelMode.Driving).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(600), walking.RawDuration);
        Assert.Equal(TimeSpan.FromSeconds(200), cycling.RawDuration);
        Assert.Equal(TimeSpan.FromSeconds(90), driving.RawDuration);
        Assert.All(new[] { walking, cycling, driving }, r => Assert.Equal(TransferRuleApplied.Matrix, r.AppliedRule));
    }

    [Fact]
    public async Task GetRequiredGapAsyncMatrixAddsConfiguredOverheadOnTopOfRawDuration()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo, [MakePair(1, 2, walkSeconds: 300, cycleSeconds: 0, driveSeconds: 0)]);
        var options = new TransferPolicyOptions
        {
            DepartureOverhead = TimeSpan.FromMinutes(5),
            ArrivalOverhead = TimeSpan.FromMinutes(5),
            ReliabilityBuffer = TimeSpan.FromMinutes(10)
        };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(300), result.RawDuration);
        Assert.Equal(TimeSpan.FromMinutes(20), result.Overhead);
        Assert.Equal(TimeSpan.FromSeconds(300) + TimeSpan.FromMinutes(20), result.RequiredGap);
    }

    [Fact]
    public async Task GetRequiredGapAsyncMatrixIsDirectional()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo,
        [
            MakePair(1, 2, walkSeconds: 300, cycleSeconds: 0, driveSeconds: 0),
            MakePair(2, 1, walkSeconds: 900, cycleSeconds: 0, driveSeconds: 0)
        ]);
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        TransferGapResult forward = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        TransferGapResult backward = await provider.GetRequiredGapAsync(2, 1, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromSeconds(300), forward.RawDuration);
        Assert.Equal(TimeSpan.FromSeconds(900), backward.RawDuration);
        Assert.NotEqual(forward.RequiredGap, backward.RequiredGap);
    }

    [Fact]
    public async Task GetRequiredGapAsyncPairAbsentFromActiveMatrixFallsBackToMissingData()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo, [MakePair(1, 2, 300, 200, 90)]); // no pair for (3, 4)
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        TransferGapResult result = await provider.GetRequiredGapAsync(3, 4, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.MissingDataFallback, result.AppliedRule);
    }

    [Fact]
    public async Task GetRequiredGapAsyncNoActiveMatrixEverPublishedFallsBackToMissingData()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo);
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TransferRuleApplied.MissingDataFallback, result.AppliedRule);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Missing-data fallback
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequiredGapAsyncMissingDataFallbackUsesConfiguredValue()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo);
        var options = new TransferPolicyOptions { MissingDataFallback = TimeSpan.FromMinutes(45) };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.Equal(TimeSpan.FromMinutes(45), result.RequiredGap);
        Assert.Null(result.RawDuration);
        Assert.Equal(TimeSpan.Zero, result.Overhead);
    }

    [Fact]
    public async Task GetRequiredGapAsyncMissingDataFallbackNeverZeroEvenWhenMisconfiguredAsZero()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo);
        var options = new TransferPolicyOptions { MissingDataFallback = TimeSpan.Zero };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.True(result.RequiredGap > TimeSpan.Zero);
    }

    [Fact]
    public async Task GetRequiredGapAsyncMissingDataFallbackNeverZeroWhenConfiguredNegative()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupNoActiveMatrix(mockRepo);
        var options = new TransferPolicyOptions { MissingDataFallback = TimeSpan.FromMinutes(-5) };
        var provider = new VenueTransferTimeProvider(mockRepo.Object, options);

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        Assert.True(result.RequiredGap > TimeSpan.Zero);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Caching / metadata
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRequiredGapAsyncLoadsActiveMatrixAtMostOncePerProviderInstance()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo, [MakePair(1, 2, 300, 200, 90), MakePair(3, 4, 400, 250, 100)]);
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        _ = await provider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        _ = await provider.GetRequiredGapAsync(3, 4, TravelMode.Driving).ConfigureAwait(true);
        _ = await provider.GetRequiredGapAsync(1, 2, TravelMode.Cycling).ConfigureAwait(true);

        mockRepo.Verify(r => r.GetActiveTransferMatrixPointerAsync(), Times.Once);
        mockRepo.Verify(r => r.GetTransferMatrixPairsAsync(activeHash), Times.Once);
    }

    [Fact]
    public async Task GetRequiredGapAsyncResultIncludesFromToModeAndAppliedRuleMetadata()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupActiveMatrix(mockRepo, [MakePair(1, 2, 300, 200, 90)]);
        var provider = new VenueTransferTimeProvider(mockRepo.Object, new TransferPolicyOptions());

        TransferGapResult result = await provider.GetRequiredGapAsync(1, 2, TravelMode.Cycling).ConfigureAwait(true);

        Assert.Equal(1, result.FromVenueNumber);
        Assert.Equal(2, result.ToVenueNumber);
        Assert.Equal(TravelMode.Cycling, result.Mode);
        Assert.Equal(TransferRuleApplied.Matrix, result.AppliedRule);
    }

    [Fact]
    public async Task GetRequiredGapAsyncRawDurationAndOverheadChangeIndependently()
    {
        Mock<FringeRepository> mockRepoLowOverhead = BuildMockRepo();
        SetupActiveMatrix(mockRepoLowOverhead, [MakePair(1, 2, 300, 0, 0)]);
        var lowOverheadProvider = new VenueTransferTimeProvider(
            mockRepoLowOverhead.Object,
            new TransferPolicyOptions { DepartureOverhead = TimeSpan.Zero, ArrivalOverhead = TimeSpan.Zero, ReliabilityBuffer = TimeSpan.Zero });

        Mock<FringeRepository> mockRepoHighOverhead = BuildMockRepo();
        SetupActiveMatrix(mockRepoHighOverhead, [MakePair(1, 2, 300, 0, 0)]);
        var highOverheadProvider = new VenueTransferTimeProvider(
            mockRepoHighOverhead.Object,
            new TransferPolicyOptions { DepartureOverhead = TimeSpan.FromMinutes(30), ArrivalOverhead = TimeSpan.Zero, ReliabilityBuffer = TimeSpan.Zero });

        TransferGapResult low = await lowOverheadProvider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);
        TransferGapResult high = await highOverheadProvider.GetRequiredGapAsync(1, 2, TravelMode.Walking).ConfigureAwait(true);

        // Same raw matrix duration in both — only the policy-driven overhead differs, and only
        // RequiredGap moves as a result, proving the two are independent.
        Assert.Equal(low.RawDuration, high.RawDuration);
        Assert.NotEqual(low.RequiredGap, high.RequiredGap);
    }
}
