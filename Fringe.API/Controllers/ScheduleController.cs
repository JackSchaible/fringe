using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ScheduleController(FringeRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ScheduleResponseDto>> GetSchedule()
    {
        string userId = GetUserId();
        var user = await repo.GetUserAsync(userId);
        if (user?.GroupId == null)
            return BadRequest("Join a group before viewing the schedule.");

        var members = await repo.GetGroupMembersAsync(user.GroupId);

        // Fetch votes and availability for all members concurrently
        var memberVotesList = await Task.WhenAll(
            members.Select(m => repo.GetVotesForUserAsync(m.UserId)));
        var availabilityRecords = await Task.WhenAll(
            members.Select(m => repo.GetAvailabilityAsync(m.UserId)));

        // Build availability map: userId → parsed UTC windows (empty list = no constraints)
        var availabilityMap = members
            .Zip(availabilityRecords)
            .ToDictionary(
                x => x.First.UserId,
                x => ParseWindows(x.Second?.Windows));

        // Aggregate priority scores: rank 1 = most wanted → highest points
        var scores = new Dictionary<int, int>();
        foreach (var votes in memberVotesList)
        {
            int totalRanked = votes.Count;
            foreach (var v in votes)
            {
                int showId = int.Parse(v.Sk.Replace("VOTE#SHOW#", ""));
                int points = totalRanked - v.Score + 1;
                scores[showId] = scores.GetValueOrDefault(showId) + points;
            }
        }

        if (scores.Count == 0)
            return Ok(new ScheduleResponseDto([], []));

        // Load shows, filter to voted, sorted by descending score
        var allShows = await repo.GetAllShowsAsync();
        var votedShows = allShows
            .Where(s => scores.ContainsKey(s.ShowId))
            .OrderByDescending(s => scores[s.ShowId])
            .ToList();

        // Fetch showtimes concurrently
        var showTimeLists = await Task.WhenAll(
            votedShows.Select(s => repo.GetShowTimesForShowAsync(s.ShowId)));

        var showTimesMap = votedShows
            .Zip(showTimeLists)
            .ToDictionary(
                x => x.First.ShowId,
                x => x.Second.Select(st => st.DateTime).Order().ToList());

        // Main schedule with all members' availability constraints
        var mainItems = BuildSchedule(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: null);

        // Alternate proposals: re-run without each member's constraints; surface if more shows fit
        var proposals = new List<AlternateProposalDto>();
        foreach (var (member, availability) in members.Zip(availabilityRecords))
        {
            if ((availability?.Windows ?? []).Count == 0) continue;

            var altItems = BuildSchedule(votedShows, showTimesMap, scores, availabilityMap, excludedUserId: member.UserId);
            int extra = altItems.Count - mainItems.Count;
            if (extra <= 0) continue;

            string name = member.DisplayName ?? member.Email ?? "a member";
            string showWord = extra == 1 ? "show" : "shows";
            proposals.Add(new AlternateProposalDto(
                $"If {name} relaxes their availability, {extra} more {showWord} could be added",
                name,
                altItems));
        }

        return Ok(new ScheduleResponseDto(mainItems, proposals));
    }

    private static List<ScheduleItemDto> BuildSchedule(
        List<ShowRecord> votedShows,
        Dictionary<int, List<string>> showTimesMap,
        Dictionary<int, int> scores,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap,
        string? excludedUserId)
    {
        var schedule = new List<ScheduleItemDto>();
        var bookedSlots = new List<(DateTime Start, DateTime End)>();

        var effectiveAvailability = availabilityMap
            .Where(kvp => kvp.Key != excludedUserId)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        foreach (var show in votedShows)
        {
            if (!showTimesMap.TryGetValue(show.ShowId, out var times)) continue;

            foreach (var timeStr in times)
            {
                var start = DateTime.Parse(timeStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var end = start.AddMinutes(show.LengthInMinutes);

                bool conflicts = bookedSlots.Any(s => start < s.End && end > s.Start);
                if (conflicts) continue;

                if (!IsAvailableForAll(start, end, effectiveAvailability)) continue;

                schedule.Add(new ScheduleItemDto(ShowsController.ToDto(show, times), timeStr, scores[show.ShowId]));
                bookedSlots.Add((start, end));
                break;
            }
        }

        return schedule.OrderBy(s => s.ShowTime).ToList();
    }

    private static bool IsAvailableForAll(
        DateTime start, DateTime end,
        Dictionary<string, List<(DateTime Start, DateTime End)>> availabilityMap) =>
        availabilityMap.All(kvp =>
        {
            var windows = kvp.Value;
            return windows.Count == 0 || windows.Any(w => w.Start <= start && w.End >= end);
        });

    private static List<(DateTime Start, DateTime End)> ParseWindows(List<AvailabilityWindowData>? raw) =>
        (raw ?? [])
            .Select(w => (
                Start: DateTime.Parse(w.Start, null, System.Globalization.DateTimeStyles.RoundtripKind),
                End: DateTime.Parse(w.End, null, System.Globalization.DateTimeStyles.RoundtripKind)))
            .ToList();

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
