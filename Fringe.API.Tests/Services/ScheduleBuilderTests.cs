using Fringe.API.Services;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;
using Moq;

namespace Fringe.API.Tests.Services;

/// <summary>
/// Tests for ScheduleBuilder.BuildScheduleAsync's venue-transfer feasibility checks (FA-11).
/// Non-transfer behaviour (availability, scoring, conflict detection) is exhaustively covered
/// by ScheduleControllerTests using an always-feasible fake provider — these tests instead
/// pin every precedence/boundary case a real IVenueTransferTimeProvider can produce, using
/// MockBehavior.Strict so an unexpected or wrongly-directed lookup fails the test immediately.
/// </summary>
public sealed class ScheduleBuilderTests
{
    private static ShowRecord MakeShow(int id, int? venueNumber, int lengthMinutes = 60)
    {
        return new ShowRecord
        {
            Pk = $"SHOW#{id}",
            Sk = "METADATA",
            ShowId = id,
            Title = $"Show {id}",
            Price = "0",
            Fee = "0",
            LengthInMinutes = lengthMinutes,
            Venue = venueNumber == null
                ? null
                : new VenueData { VenueNumber = venueNumber.Value, Name = $"Venue {venueNumber}", Address = "", Phone = "", PostalCode = "" }
        };
    }

    private static Mock<IVenueTransferTimeProvider> MockProvider()
    {
        return new Mock<IVenueTransferTimeProvider>(MockBehavior.Strict);
    }

    private static void SetupGap(Mock<IVenueTransferTimeProvider> mock, int from, int to, TimeSpan requiredGap)
    {
        _ = mock.Setup(p => p.GetRequiredGapAsync(from, to, TravelMode.Walking))
                .ReturnsAsync(new TransferGapResult
                {
                    FromVenueNumber = from,
                    ToVenueNumber = to,
                    Mode = TravelMode.Walking,
                    AppliedRule = TransferRuleApplied.Matrix,
                    RequiredGap = requiredGap
                });
    }

    private static (Dictionary<int, List<string>> Times, Dictionary<int, int> Scores) TimesAndScores(params (int ShowId, string Time)[] entries)
    {
        Dictionary<int, List<string>> times = [];
        Dictionary<int, int> scores = [];
        foreach ((int showId, string time) in entries)
        {
            times[showId] = [time];
            scores[showId] = 1;
        }
        return (times, scores);
    }

    private static readonly Dictionary<string, List<(DateTime Start, DateTime End)>> noAvailabilityConstraints = [];

    // ─────────────────────────────────────────────────────────────────────────
    // Chronological neighbour identification (independent of insertion/score order)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CandidateBeforeAllExistingPerformancesOnlyChecksTheNextNeighbour()
    {
        // Show 1 (venue 10) is processed first and booked at 19:00-20:00.
        // Show 2 (venue 20) is evaluated second but its single showtime (17:00-18:00) is
        // chronologically *before* show 1 — so show 1 is show 2's "next" neighbour, and
        // there is no "previous" neighbour at all.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T19:00:00Z"),
            (2, "2026-07-10T17:00:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 20, to: 10, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        provider.Verify(p => p.GetRequiredGapAsync(20, 10, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task CandidateAfterAllExistingPerformancesOnlyChecksThePreviousNeighbour()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T12:00:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        provider.Verify(p => p.GetRequiredGapAsync(10, 20, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task CandidateInsertedBetweenTwoExistingPerformancesChecksBothNeighbours()
    {
        // Shows 1 and 3 are processed (and booked) first; show 2's single showtime falls
        // chronologically between them.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(3, 30), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (3, "2026-07-10T15:00:00Z"),
            (2, "2026-07-10T12:00:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        // Booking show 3 (venue 30) itself first checks back against the already-booked show 1
        // (venue 10) as its previous neighbour, before show 2 is ever evaluated.
        SetupGap(provider, from: 10, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 20, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(3, result.Count);
        provider.Verify(p => p.GetRequiredGapAsync(10, 20, TravelMode.Walking), Times.Once);
        provider.Verify(p => p.GetRequiredGapAsync(20, 30, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task InsertionOrderDoesNotAffectChronologicalNeighbourSelection()
    {
        // Show 3 (latest time, venue 30) is booked FIRST (highest priority), then show 1
        // (earliest time, venue 10) is booked SECOND — so bookedSlots contains [3, 1] in
        // insertion order even though chronologically 1 comes before 3. Show 2 (venue 20,
        // middle time) must still resolve show 1 as its previous neighbour and show 3 as
        // its next neighbour, based on TIME, not insertion order.
        List<ShowRecord> votedShows = [MakeShow(3, 30), MakeShow(1, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (3, "2026-07-10T15:00:00Z"),
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T12:00:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        // Booking show 1 (venue 10) checks forward against the already-booked show 3 (venue 30)
        // as its next chronological neighbour, before show 2 is ever evaluated.
        SetupGap(provider, from: 10, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 20, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(3, result.Count);
        // The wrong-direction pairs (30->20 and 20->10, i.e. treating insertion order as
        // chronological order) must never be queried.
        provider.Verify(p => p.GetRequiredGapAsync(30, 20, It.IsAny<TravelMode>()), Times.Never);
        provider.Verify(p => p.GetRequiredGapAsync(20, 10, It.IsAny<TravelMode>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rejection / acceptance boundaries
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RejectsCandidateWhenOnlyThePreviousTransitionIsInfeasible()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(3, 30), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (3, "2026-07-10T15:00:00Z"),
            (2, "2026-07-10T10:30:00Z")); // only 30 min after show 1 ends (10:00)

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        // Booking show 3 (venue 30) itself checks back against the already-booked show 1
        // (venue 10) first, before show 2 is ever evaluated.
        SetupGap(provider, from: 10, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        // Previous transition (10 -> 20) needs more than the available 30-minute gap.
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromHours(1));
        // The next transition (20 -> 30) must never be reached — no setup for it at all,
        // so Strict mode fails the test if ScheduleBuilder calls it anyway.
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.DoesNotContain(result, i => i.Show.ShowId == 2);
        Assert.Equal(2, result.Count); // shows 1 and 3 only
    }

    [Fact]
    public async Task RejectsCandidateWhenOnlyTheNextTransitionIsInfeasible()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(3, 30), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (3, "2026-07-10T11:45:00Z"), // only 30 min after show 2 ends (11:15)
            (2, "2026-07-10T10:15:00Z")); // exactly 15 min after show 1 ends (10:00)

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        // Booking show 3 (venue 30) itself checks back against the already-booked show 1
        // (venue 10) first, before show 2 is ever evaluated.
        SetupGap(provider, from: 10, to: 30, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(15)); // previous: feasible
        SetupGap(provider, from: 20, to: 30, requiredGap: TimeSpan.FromHours(1)); // next: infeasible
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.DoesNotContain(result, i => i.Show.ShowId == 2);
        Assert.Equal(2, result.Count); // shows 1 and 3 only
        provider.Verify(p => p.GetRequiredGapAsync(10, 20, TravelMode.Walking), Times.Once);
        provider.Verify(p => p.GetRequiredGapAsync(20, 30, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task AcceptsCandidateWhenGapExactlyEqualsRequiredTransferTime()
    {
        // Show 1 ends at 10:00; show 2 starts exactly 30 minutes later.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T10:30:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Contains(result, i => i.Show.ShowId == 2);
    }

    [Fact]
    public async Task RejectsCandidateWhenGapIsOneMinuteShortOfRequiredTransferTime()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T10:29:00Z")); // 1 minute short of the 30-minute requirement

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.DoesNotContain(result, i => i.Show.ShowId == 2);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Same-venue and directionality
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SameVenuePairStillQueriesTheProviderAndHonoursItsConfiguredBuffer()
    {
        // Back-to-back shows in the same venue, only 5 minutes apart — accepted because the
        // (fake) provider reports same-venue transfers need only 5 minutes, not because
        // ScheduleBuilder special-cases matching venue numbers itself.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, 10)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T10:05:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: 10, requiredGap: TimeSpan.FromMinutes(5));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        provider.Verify(p => p.GetRequiredGapAsync(10, 10, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task DirectionalTransferTimesAreRespectedIndependently()
    {
        // venue 10 -> venue 20 requires only 10 minutes, but the reverse direction requires
        // an hour. Show 2 (venue 20) sits between two venue-10 performances, so both
        // directions get exercised in the same run with different requirements.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(3, 10), MakeShow(2, 20)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"), // ends 10:00
            (3, "2026-07-10T11:40:00Z"), // starts 30 min after show 2 ends (11:10)
            (2, "2026-07-10T10:10:00Z")); // 10 min after show 1, 90 min before show 3

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        // Booking show 3 (venue 10) itself checks back against the already-booked show 1
        // (also venue 10, same-venue rule) first, before show 2 is ever evaluated.
        SetupGap(provider, from: 10, to: 10, requiredGap: TimeSpan.FromMinutes(30));
        SetupGap(provider, from: 10, to: 20, requiredGap: TimeSpan.FromMinutes(10)); // forward: tight but sufficient
        SetupGap(provider, from: 20, to: 10, requiredGap: TimeSpan.FromHours(1)); // reverse: stricter, and insufficient here
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        // Show 2 is rejected by the reverse (20 -> 10) direction's stricter requirement, even
        // though the forward (10 -> 20) direction was satisfied.
        Assert.DoesNotContain(result, i => i.Show.ShowId == 2);
        provider.Verify(p => p.GetRequiredGapAsync(10, 20, TravelMode.Walking), Times.Once);
        provider.Verify(p => p.GetRequiredGapAsync(20, 10, TravelMode.Walking), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Missing venue data
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ShowWithNoEmbeddedVenueIsLookedUpUsingTheUnknownVenueSentinel()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, venueNumber: null)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T12:00:00Z"));

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: -1, requiredGap: TimeSpan.FromMinutes(30));
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        provider.Verify(p => p.GetRequiredGapAsync(10, -1, TravelMode.Walking), Times.Once);
    }

    [Fact]
    public async Task MissingVenueTreatedAsInfeasibleIsRejectedNotSilentlyAccepted()
    {
        // The provider reports a large conservative fallback for the unknown-venue side —
        // proving a missing venue can reject a candidate, i.e. it is never a free pass.
        List<ShowRecord> votedShows = [MakeShow(1, 10), MakeShow(2, venueNumber: null)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"),
            (2, "2026-07-10T10:05:00Z")); // only 5 minutes after show 1 ends

        Mock<IVenueTransferTimeProvider> provider = MockProvider();
        SetupGap(provider, from: 10, to: -1, requiredGap: TimeSpan.FromHours(1)); // conservative fallback
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        Assert.DoesNotContain(result, i => i.Show.ShowId == 2);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Availability and scoring composed with transfer checks
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UnavailableMemberStillExcludesCandidateBeforeAnyTransferLookup()
    {
        // A candidate that fails availability must never reach the (Strict, unconfigured)
        // transfer provider at all.
        List<ShowRecord> votedShows = [MakeShow(1, 10)];
        (Dictionary<int, List<string>> times, Dictionary<int, int> scores) = TimesAndScores(
            (1, "2026-07-10T09:00:00Z"));
        Dictionary<string, List<(DateTime Start, DateTime End)>> unavailable = new()
        {
            ["alice"] = [(new DateTime(2026, 7, 11, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 7, 11, 1, 0, 0, DateTimeKind.Utc))]
        };
        Mock<IVenueTransferTimeProvider> provider = MockProvider(); // no setups at all
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, unavailable, excludedUserId: null).ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GroupScoreIsPreservedOnAcceptedItems()
    {
        List<ShowRecord> votedShows = [MakeShow(1, 10)];
        Dictionary<int, List<string>> times = new() { [1] = ["2026-07-10T09:00:00Z"] };
        Dictionary<int, int> scores = new() { [1] = 42 };
        Mock<IVenueTransferTimeProvider> provider = MockProvider(); // single show, no neighbours -> no calls
        var builder = new ScheduleBuilder(provider.Object);

        List<ScheduleItemDto> result = await builder.BuildScheduleAsync(votedShows, times, scores, noAvailabilityConstraints, excludedUserId: null).ConfigureAwait(true);

        _ = Assert.Single(result);
        Assert.Equal(42, result[0].GroupScore);
    }
}
