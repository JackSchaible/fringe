using System.Globalization;
using Fringe.API.Controllers;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;

namespace Fringe.API.Services;

/// <inheritdoc cref="IScheduleBuilder"/>
internal sealed class ScheduleBuilder(IVenueTransferTimeProvider transferTimeProvider) : IScheduleBuilder
{
    /// <summary>
    /// Sentinel used when a show has no resolvable venue number (a null embedded <c>Venue</c>,
    /// or — matching <c>DetailScraper</c>'s own convention for an unparsed venue — a
    /// <see cref="VenueData.VenueNumber"/> of -1). Routed through the same
    /// <see cref="IVenueTransferTimeProvider"/> lookup as any other venue number: since it never
    /// matches a real venue number, override, or matrix pair, it always resolves to the
    /// conservative missing-data fallback — never a silent zero-minute transfer. The one gap this
    /// doesn't cover: two shows that *both* have an unresolvable venue would compare equal and
    /// hit the same-venue rule instead of the fallback. Rare enough (it requires two adjacent,
    /// independently-broken venue records) not to warrant extra plumbing for now.
    /// </summary>
    private const int unknownVenueNumber = -1;

    /// <inheritdoc/>
    public async Task<List<ScheduleItemDto>> BuildScheduleAsync(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        Dictionary<int, int> scores,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        string? excludedUserId,
        TravelMode travelMode)
    {
        List<ScheduleItemDto> schedule = [];
        List<(DateTime Start, DateTime End, int VenueNumber, string ShowTitle)> bookedSlots = [];

        var effectiveAvailability = availabilityMap
            .Where(kvp => kvp.Key != excludedUserId)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (ShowRecord show in votedShows)
        {
            if (!showTimesMap.TryGetValue(show.ShowId, out List<string>? times))
            {
                continue;
            }

            int venueNumber = show.Venue?.VenueNumber ?? unknownVenueNumber;

            foreach (string timeStr in times)
            {
                var start = DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                DateTime end = start.AddMinutes(show.LengthInMinutes);

                bool conflicts = bookedSlots.Any(s => start < s.End && end > s.Start);
                if (conflicts)
                {
                    continue;
                }

                if (!IsAvailableForAll(start, end, effectiveAvailability))
                {
                    continue;
                }

                if (!await IsTransferFeasibleAsync(start, end, venueNumber, show.Title, bookedSlots, travelMode).ConfigureAwait(false))
                {
                    continue;
                }

                schedule.Add(new ScheduleItemDto(ShowsController.ToDto(show, times), timeStr, scores[show.ShowId]));
                bookedSlots.Add((start, end, venueNumber, show.Title));
                break;
            }
        }

        return [.. schedule.OrderBy(s => s.ShowTime)];
    }

    /// <summary>
    /// A candidate is feasible only if the group can reach it from whichever booked performance
    /// ends nearest before it, <em>and</em> reach the next booked performance from it — checked
    /// against every booked slot each time (not just the most recently inserted one), since
    /// candidates are considered in score order, not chronological order.
    /// </summary>
    private async Task<bool> IsTransferFeasibleAsync(
        DateTime start,
        DateTime end,
        int venueNumber,
        string showTitle,
        List<(DateTime Start, DateTime End, int VenueNumber, string ShowTitle)> bookedSlots,
        TravelMode travelMode)
    {
        return await FindTransferConflictAsync(start, end, venueNumber, showTitle, bookedSlots, travelMode)
            .ConfigureAwait(false) is null;
    }

    /// <inheritdoc/>
    public async Task<TransferConflictDetail?> FindTransferConflictAsync(
        DateTime start,
        DateTime end,
        int venueNumber,
        string showTitle,
        List<(DateTime Start, DateTime End, int VenueNumber, string ShowTitle)> bookedSlots,
        TravelMode travelMode)
    {
        List<(DateTime Start, DateTime End, int VenueNumber, string ShowTitle)> before = [.. bookedSlots.Where(s => s.End <= start)];
        if (before.Count > 0)
        {
            (_, DateTime previousEnd, int previousVenueNumber, string previousShowTitle) = before.OrderByDescending(s => s.End).First();
            TransferGapResult required = await transferTimeProvider
                .GetRequiredGapAsync(previousVenueNumber, venueNumber, travelMode)
                .ConfigureAwait(false);
            TimeSpan availableGap = start - previousEnd;
            if (availableGap < required.RequiredGap)
            {
                return new TransferConflictDetail(previousVenueNumber, venueNumber, previousShowTitle, showTitle, availableGap, required.RequiredGap, travelMode, required.AppliedRule);
            }
        }

        List<(DateTime Start, DateTime End, int VenueNumber, string ShowTitle)> after = [.. bookedSlots.Where(s => s.Start >= end)];
        if (after.Count > 0)
        {
            (DateTime nextStart, _, int nextVenueNumber, string nextShowTitle) = after.OrderBy(s => s.Start).First();
            TransferGapResult required = await transferTimeProvider
                .GetRequiredGapAsync(venueNumber, nextVenueNumber, travelMode)
                .ConfigureAwait(false);
            TimeSpan availableGap = nextStart - end;
            if (availableGap < required.RequiredGap)
            {
                return new TransferConflictDetail(venueNumber, nextVenueNumber, showTitle, nextShowTitle, availableGap, required.RequiredGap, travelMode, required.AppliedRule);
            }
        }

        return null;
    }

    private static bool IsAvailableForAll(
        DateTime start, DateTime end,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap)
    {
        return availabilityMap.All(kvp =>
        {
            List<(DateTime Start, DateTime End)> windows = kvp.Value;
            return windows.Count == 0 || windows.Any(w => w.Start <= start && w.End >= end);
        });
    }
}
