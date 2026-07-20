using Fringe.API.Controllers;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <summary>
/// Builds a greedy, score-ordered group schedule, rejecting a candidate performance whose
/// venue can't be reasonably reached from (or left in time for) its chronologically nearest
/// neighbours. Extracted out of <see cref="ScheduleController"/> so the construction algorithm
/// is testable on its own, without a controller/HTTP context.
/// </summary>
internal interface IScheduleBuilder
{
    /// <summary>
    /// Returns the accepted schedule items, in show-time order. <paramref name="votedShows"/>
    /// must already be in descending-score order — that's the priority candidates are considered in.
    /// <paramref name="travelMode"/> is applied uniformly to every transfer check in this build;
    /// the scheduler never mixes modes to produce a faster transition (FA-37).
    /// </summary>
    Task<List<ScheduleItemDto>> BuildScheduleAsync(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        Dictionary<int, int> scores,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        string? excludedUserId,
        TravelMode travelMode);

    /// <summary>
    /// Checks a candidate performance's venue-transfer feasibility against its nearest
    /// chronological neighbours in <paramref name="bookedSlots"/>, using the exact same
    /// neighbour-selection and short-circuit precedence as <see cref="BuildScheduleAsync"/>
    /// (previous-transition failures are reported before next-transition ones are even checked).
    /// Returns <see langword="null"/> when feasible (or when there are no neighbours to check
    /// against); otherwise the diagnostic detail for the first infeasible transition found — for
    /// missed-show diagnostics (FA-35), so a rejection reported here is guaranteed to match the
    /// reason <see cref="BuildScheduleAsync"/> would have rejected the same candidate for.
    /// </summary>
    Task<TransferConflictDetail?> FindTransferConflictAsync(
        DateTime start,
        DateTime end,
        int venueNumber,
        List<(DateTime Start, DateTime End, int VenueNumber)> bookedSlots,
        TravelMode travelMode);
}
