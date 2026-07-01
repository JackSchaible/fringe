using Fringe.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VotesController(FringeRepository repo) : ControllerBase
{
    [HttpGet]
    public async Task<List<VoteDto>> GetVotes()
    {
        string userId = GetUserId();
        var records = await repo.GetVotesForUserAsync(userId);
        return records
            .Select(r => new VoteDto(int.Parse(r.Sk.Replace("VOTE#SHOW#", "")), r.Score))
            .ToList();
    }

    [HttpPut]
    public async Task<IActionResult> SaveVotes([FromBody] List<VoteDto> votes)
    {
        string userId = GetUserId();

        // Delete existing votes not in the new list so removed shows are cleared
        var existing = await repo.GetVotesForUserAsync(userId);
        var newShowIds = votes.Select(v => v.ShowId).ToHashSet();
        var toDelete = existing
            .Select(r => int.Parse(r.Sk.Replace("VOTE#SHOW#", "")))
            .Where(id => !newShowIds.Contains(id));
        await repo.DeleteVotesAsync(userId, toDelete);

        foreach (var vote in votes)
            await repo.UpsertVoteAsync(userId, vote.ShowId, vote.Rank);

        return NoContent();
    }

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";
}
