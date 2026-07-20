using System.Globalization;
using Fringe.API.Services;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Computes and returns the optimal group schedule.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class ScheduleController(FringeRepository repo, IScheduleBuilder scheduleBuilder) : ControllerBase
{
    /// <summary>Returns the computed schedule for the current user's group.</summary>
    /// <param name="mode">
    /// The group travel mode to assume for every transfer check in this schedule — "walking",
    /// "cycling", or "driving" (case-insensitive). Omitted or empty is treated as "walking".
    /// The scheduler applies exactly one mode uniformly; it never mixes modes to find a faster
    /// transition (FA-37).
    /// </param>
    [HttpGet]
    public async Task<ActionResult<ScheduleResponseDto>> GetSchedule([FromQuery] string? mode = null)
    {
        if (!TryParseTravelMode(mode, out TravelMode travelMode))
        {
            return BadRequest($"Invalid travel mode '{mode}'. Supported values: walking, cycling, driving.");
        }

        string userId = GetUserId();
        UserRecord? user = await repo.GetUserAsync(userId).ConfigureAwait(false);
        if (user?.GroupId == null)
        {
            return BadRequest("Join a group before viewing the schedule.");
        }

        List<GroupMemberRecord> members = [.. (await repo.GetGroupMembersAsync(user.GroupId).ConfigureAwait(false))
            .Where(m => !string.IsNullOrEmpty(m.UserId))];

        List<UserVoteRecord>[] memberVotesList = await Task.WhenAll(
            members.Select(m => repo.GetVotesForUserAsync(m.UserId))).ConfigureAwait(false);
        UserAvailabilityRecord?[] availabilityRecords = await Task.WhenAll(
            members.Select(m => repo.GetAvailabilityAsync(m.UserId))).ConfigureAwait(false);

        // If nobody has set availability, treat everyone as unconstrained (legacy behaviour).
        // Once any member has a record, members without one are treated as unavailable.
        bool anyoneHasAvailability = availabilityRecords.Any(r => r != null && r.Windows.Count > 0);

        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap = members
            .Zip(availabilityRecords)
            .ToDictionary(
                x => x.First.UserId,
                x => anyoneHasAvailability
                    ? ParseWindows(x.Second?.Windows)
                    : new List<(DateTime, DateTime)>());

        var scores = new Dictionary<int, int>();
        foreach (List<UserVoteRecord> memberVotes in memberVotesList)
        {
            int totalRanked = memberVotes.Count;
            foreach (UserVoteRecord v in memberVotes)
            {
                int showId = int.Parse(v.Sk.Replace("VOTE#SHOW#", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
                int points = totalRanked - v.Score + 1;
                scores[showId] = scores.GetValueOrDefault(showId) + points;
            }
        }

        string travelModeString = TravelModeToApiString(travelMode);

        if (scores.Count == 0)
        {
            return Ok(new ScheduleResponseDto([], [], [], HasVotes: false, travelModeString));
        }

        List<ShowRecord> allShows = await repo.GetAllShowsAsync().ConfigureAwait(false);
        var votedShows = allShows
            .Where(s => scores.ContainsKey(s.ShowId))
            .OrderByDescending(s => scores[s.ShowId])
            .ToList();

        List<ShowTimeRecord>[] showTimeLists = await Task.WhenAll(
            votedShows.Select(s => repo.GetShowTimesForShowAsync(s.ShowId))).ConfigureAwait(false);

        var showTimesMap = votedShows
            .Zip(showTimeLists)
            .ToDictionary(
                x => x.First.ShowId,
                x => x.Second.Select(st => st.DateTime).Order().ToList());

        var memberNames = members.ToDictionary(m => m.UserId, m => (string?)(m.DisplayName ?? m.Email));

        List<ScheduleItemDto> mainItems = await scheduleBuilder.BuildScheduleAsync(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: null, travelMode).ConfigureAwait(false);

        var proposals = new List<AlternateProposalDto>();
        foreach (GroupMemberRecord member in members)
        {
            if (availabilityMap[member.UserId].Count == 0 && !anyoneHasAvailability)
            {
                continue;
            }

            List<ScheduleItemDto> altItems = await scheduleBuilder.BuildScheduleAsync(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: member.UserId, travelMode).ConfigureAwait(false);
            int extra = altItems.Count - mainItems.Count;
            if (extra <= 0)
            {
                continue;
            }

            string name = member.DisplayName ?? member.Email ?? "a member";
            string showWord = extra == 1 ? "show" : "shows";
            string verb = mainItems.Count == 0 ? "fit" : "could be added";
            proposals.Add(new AlternateProposalDto(
                $"{extra} more {showWord} {verb} without {name}'s availability restrictions",
                name,
                altItems));
        }

        List<MissedShowDto> missedShows = await ComputeMissedShowsAsync(votedShows, showTimesMap, mainItems, availabilityMap, memberNames, scheduleBuilder, travelMode).ConfigureAwait(false);

        return Ok(new ScheduleResponseDto(mainItems, proposals, missedShows, HasVotes: true, travelModeString));
    }

    private static bool TryParseTravelMode(string? raw, out TravelMode mode)
    {
        if (string.IsNullOrEmpty(raw))
        {
            mode = TravelMode.Walking;
            return true;
        }

        foreach (TravelMode candidate in Enum.GetValues<TravelMode>())
        {
            if (string.Equals(raw, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                mode = candidate;
                return true;
            }
        }

        mode = default;
        return false;
    }

    private static string TravelModeToApiString(TravelMode mode)
    {
        return mode switch
        {
            TravelMode.Walking => "walking",
            TravelMode.Cycling => "cycling",
            TravelMode.Driving => "driving",
            _ => "walking",
        };
    }

    private static List<string> GetBlockingMembers(
        DateTime start, DateTime end,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        Dictionary<string, string?> memberNames)
    {
        return [..availabilityMap
            .Where(kvp =>
            {
                List<(DateTime Start, DateTime End)> windows = kvp.Value;
                return !(windows.Count == 0 || windows.Any(w => w.Start <= start && w.End >= end));
            })
            .Select(kvp => memberNames.GetValueOrDefault(kvp.Key) ?? kvp.Key)];
    }

    private const int unknownVenueNumber = -1;

    private static async Task<List<MissedShowDto>> ComputeMissedShowsAsync(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        List<ScheduleItemDto> mainSchedule,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        Dictionary<string, string?> memberNames,
        IScheduleBuilder scheduleBuilder,
        TravelMode travelMode)
    {
        var scheduledIds = mainSchedule.Select(i => i.Show.ShowId).ToHashSet();

        var venueNumberByShowId = votedShows
            .ToDictionary(s => s.ShowId, s => s.Venue?.VenueNumber ?? unknownVenueNumber);
        var venueNameByNumber = votedShows
            .GroupBy(s => s.Venue?.VenueNumber ?? unknownVenueNumber)
            .ToDictionary(g => g.Key, g => g.First().Venue?.Name);

        List<(DateTime Start, DateTime End, int VenueNumber)> bookedSlots = [..mainSchedule.Select(i =>
        {
            var s = DateTime.Parse(i.ShowTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return (s, s.AddMinutes(i.Show.LengthInMinutes), venueNumberByShowId.GetValueOrDefault(i.Show.ShowId, unknownVenueNumber));
        })];

        var missed = new List<MissedShowDto>();

        foreach (ShowRecord show in votedShows)
        {
            if (scheduledIds.Contains(show.ShowId))
            {
                continue;
            }

            if (!showTimesMap.TryGetValue(show.ShowId, out List<string>? times))
            {
                continue;
            }

            bool conflictsWithScheduled = false;
            var allBlockers = new HashSet<string>();
            TransferConflictDto? transferConflict = null;
            int venueNumber = show.Venue?.VenueNumber ?? unknownVenueNumber;

            foreach (string timeStr in times)
            {
                var start = DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                DateTime end = start.AddMinutes(show.LengthInMinutes);

                if (bookedSlots.Any(s => start < s.End && end > s.Start))
                {
                    conflictsWithScheduled = true;
                    continue;
                }

                List<string> blockers = GetBlockingMembers(start, end, availabilityMap, memberNames);
                foreach (string blocker in blockers)
                {
                    _ = allBlockers.Add(blocker);
                }

                if (blockers.Count > 0 || transferConflict != null)
                {
                    continue;
                }

                TransferConflictDetail? detail = await scheduleBuilder
                    .FindTransferConflictAsync(start, end, venueNumber, bookedSlots, travelMode)
                    .ConfigureAwait(false);
                if (detail != null)
                {
                    transferConflict = ToTransferConflictDto(detail.Value, venueNameByNumber);
                }
            }

            missed.Add(new MissedShowDto(ShowsController.ToDto(show, times), conflictsWithScheduled, [.. allBlockers], transferConflict));
        }

        return missed;
    }

    private static TransferConflictDto ToTransferConflictDto(
        TransferConflictDetail detail,
        Dictionary<int, string?> venueNameByNumber)
    {
        return new TransferConflictDto(
            venueNameByNumber.GetValueOrDefault(detail.OriginVenueNumber),
            venueNameByNumber.GetValueOrDefault(detail.DestinationVenueNumber),
            RoundToMinutes(detail.AvailableGap),
            RoundToMinutes(detail.RequiredGap),
            TravelModeToApiString(detail.Mode),
            TransferRuleAppliedToApiString(detail.AppliedRule));
    }

    private static int RoundToMinutes(TimeSpan span)
    {
        return (int)Math.Round(span.TotalMinutes, MidpointRounding.AwayFromZero);
    }

    private static string TransferRuleAppliedToApiString(TransferRuleApplied rule)
    {
        return rule switch
        {
            TransferRuleApplied.SameVenue => "same-venue",
            TransferRuleApplied.DirectionalOverride => "override",
            TransferRuleApplied.Matrix => "matrix",
            TransferRuleApplied.MissingDataFallback => "fallback",
            _ => "fallback",
        };
    }

    private static List<(DateTime Start, DateTime End)> ParseWindows(IEnumerable<AvailabilityWindowData>? raw)
    {
        return [..(raw ?? [])
            .Select(w => (
                Start: DateTime.Parse(w.Start, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                End: DateTime.Parse(w.End, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)))];
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? "";
    }
}
