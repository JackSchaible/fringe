using System.Globalization;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Manages the current user's show votes.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class VotesController(FringeRepository repo) : ControllerBase
{
    /// <summary>Returns the current user's votes.</summary>
    [HttpGet]
    public async Task<List<VoteDto>> GetVotes()
    {
        string userId = GetUserId();
        List<UserVoteRecord> records = await repo.GetVotesForUserAsync(userId).ConfigureAwait(false);
        return [..records.Select(r => new VoteDto(
            int.Parse(r.Sk.Replace("VOTE#SHOW#", "", StringComparison.Ordinal), CultureInfo.InvariantCulture),
            r.Score))];
    }

    /// <summary>Replaces the current user's votes with the provided list.</summary>
    [HttpPut]
    public async Task<IActionResult> SaveVotes([FromBody] IEnumerable<VoteDto> votes)
    {
        string userId = GetUserId();
        List<VoteDto> voteList = [.. votes];

        List<UserVoteRecord> existing = await repo.GetVotesForUserAsync(userId).ConfigureAwait(false);
        var newShowIds = voteList.Select(v => v.ShowId).ToHashSet();
        IEnumerable<int> toDelete = existing
            .Select(r => int.Parse(r.Sk.Replace("VOTE#SHOW#", "", StringComparison.Ordinal), CultureInfo.InvariantCulture))
            .Where(id => !newShowIds.Contains(id));
        await repo.DeleteVotesAsync(userId, toDelete).ConfigureAwait(false);

        foreach (VoteDto vote in voteList)
        {
            await repo.UpsertVoteAsync(userId, vote.ShowId, vote.Rank).ConfigureAwait(false);
        }

        return NoContent();
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? "";
    }
}
