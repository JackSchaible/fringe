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
    public async Task<ActionResult<List<ScheduleItemDto>>> GetSchedule()
    {
        string userId = GetUserId();
        var user = await repo.GetUserAsync(userId);
        if (user?.GroupId == null)
            return BadRequest("Join a group before viewing the schedule.");

        var members = await repo.GetGroupMembersAsync(user.GroupId);

        // Fetch all member votes concurrently
        var memberVotesList = await Task.WhenAll(
            members.Select(m => repo.GetVotesForUserAsync(m.UserId)));

        // Aggregate priority scores: rank 1 = most wanted → highest points
        var scores = new Dictionary<int, int>();
        foreach (var votes in memberVotesList)
        {
            int totalRanked = votes.Count;
            foreach (var v in votes)
            {
                int showId = int.Parse(v.Sk.Replace("VOTE#SHOW#", ""));
                int points = totalRanked - v.Score + 1; // rank 1 → totalRanked points
                scores[showId] = scores.GetValueOrDefault(showId) + points;
            }
        }

        if (scores.Count == 0)
            return Ok(Array.Empty<ScheduleItemDto>());

        // Load all shows, filter to voted ones
        var allShows = await repo.GetAllShowsAsync();
        var votedShows = allShows
            .Where(s => scores.ContainsKey(s.ShowId))
            .OrderByDescending(s => scores[s.ShowId])
            .ToList();

        // Fetch showtimes for voted shows concurrently
        var showTimeLists = await Task.WhenAll(
            votedShows.Select(s => repo.GetShowTimesForShowAsync(s.ShowId)));

        var showTimesMap = votedShows
            .Zip(showTimeLists)
            .ToDictionary(
                x => x.First.ShowId,
                x => x.Second.Select(st => st.DateTime).Order().ToList());

        // Greedy schedule: pick highest-scored show that fits
        var schedule = new List<ScheduleItemDto>();
        var bookedSlots = new List<(DateTime Start, DateTime End)>();

        foreach (var show in votedShows)
        {
            if (!showTimesMap.TryGetValue(show.ShowId, out var times)) continue;

            foreach (var timeStr in times)
            {
                var start = DateTime.Parse(timeStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);
                var end = start.AddMinutes(show.LengthInMinutes);

                bool conflicts = bookedSlots.Any(s => start < s.End && end > s.Start);
                if (!conflicts)
                {
                    var showDto = ShowsController.ToDto(show, times);
                    schedule.Add(new ScheduleItemDto(showDto, timeStr, scores[show.ShowId]));
                    bookedSlots.Add((start, end));
                    break;
                }
            }
        }

        return Ok(schedule.OrderBy(s => s.ShowTime).ToList());
    }

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
