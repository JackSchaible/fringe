using Fringe.API.Controllers;
using Fringe.Data.DynamoRecords;

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
    /// </summary>
    Task<List<ScheduleItemDto>> BuildScheduleAsync(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        Dictionary<int, int> scores,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        string? excludedUserId);
}
