using System.Globalization;
using Fringe.API.Services;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
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
    [HttpGet]
    public async Task<ActionResult<ScheduleResponseDto>> GetSchedule()
    {
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

        if (scores.Count == 0)
        {
            return Ok(new ScheduleResponseDto([], [], [], HasVotes: false));
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

        List<ScheduleItemDto> mainItems = await scheduleBuilder.BuildScheduleAsync(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: null).ConfigureAwait(false);

        var proposals = new List<AlternateProposalDto>();
        foreach (GroupMemberRecord member in members)
        {
            if (availabilityMap[member.UserId].Count == 0 && !anyoneHasAvailability)
            {
                continue;
            }

            List<ScheduleItemDto> altItems = await scheduleBuilder.BuildScheduleAsync(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: member.UserId).ConfigureAwait(false);
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

        List<MissedShowDto> missedShows = ComputeMissedShows(votedShows, showTimesMap, mainItems, availabilityMap, memberNames);

        return Ok(new ScheduleResponseDto(mainItems, proposals, missedShows, HasVotes: true));
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

    private static List<MissedShowDto> ComputeMissedShows(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        List<ScheduleItemDto> mainSchedule,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        Dictionary<string, string?> memberNames)
    {
        var scheduledIds = mainSchedule.Select(i => i.Show.ShowId).ToHashSet();
        List<(DateTime, DateTime)> bookedSlots = [..mainSchedule.Select(i =>
        {
            var s = DateTime.Parse(i.ShowTime, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            return (s, s.AddMinutes(i.Show.LengthInMinutes));
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

            foreach (string timeStr in times)
            {
                var start = DateTime.Parse(timeStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                DateTime end = start.AddMinutes(show.LengthInMinutes);

                if (bookedSlots.Any(s => start < s.Item2 && end > s.Item1))
                {
                    conflictsWithScheduled = true;
                    continue;
                }

                foreach (string blocker in GetBlockingMembers(start, end, availabilityMap, memberNames))
                {
                    _ = allBlockers.Add(blocker);
                }
            }

            missed.Add(new MissedShowDto(ShowsController.ToDto(show, times), conflictsWithScheduled, [.. allBlockers]));
        }

        return missed;
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
