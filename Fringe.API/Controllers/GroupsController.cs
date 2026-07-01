using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GroupsController(FringeRepository repo) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateGroup([FromBody] CreateGroupRequest req)
    {
        string userId = GetUserId();

        var user = await repo.GetUserAsync(userId);
        if (user?.GroupId != null)
            return BadRequest("You are already in a group.");

        string groupId = Guid.NewGuid().ToString("N")[..12];
        string inviteCode = GenerateInviteCode();

        await repo.CreateGroupAsync(new GroupRecord
        {
            Pk = $"GROUP#{groupId}",
            GroupId = groupId,
            Name = req.Name,
            OwnerId = userId,
            InviteCode = inviteCode,
            CreatedAt = DateTime.UtcNow.ToString("O")
        });

        await repo.JoinGroupAsync(groupId, userId, user?.DisplayName ?? "", user?.Email ?? "");

        return Ok(new GroupDto(groupId, req.Name, inviteCode, []));
    }

    [HttpPost("join")]
    public async Task<ActionResult<GroupDto>> JoinGroup([FromBody] JoinGroupRequest req)
    {
        string userId = GetUserId();

        var user = await repo.GetUserAsync(userId);
        if (user?.GroupId != null)
            return BadRequest("You are already in a group.");

        var group = await repo.GetGroupByInviteCodeAsync(req.InviteCode.ToUpperInvariant());
        if (group == null)
            return NotFound("Invalid invite code.");

        await repo.JoinGroupAsync(group.GroupId, userId, user?.DisplayName ?? "", user?.Email ?? "");

        var members = await repo.GetGroupMembersAsync(group.GroupId);
        return Ok(ToDto(group, members));
    }

    [HttpGet("me")]
    public async Task<ActionResult<GroupDto>> GetMyGroup()
    {
        string userId = GetUserId();
        var user = await repo.GetUserAsync(userId);
        if (user?.GroupId == null)
            return NotFound();

        var group = await repo.GetGroupAsync(user.GroupId);
        if (group == null)
            return NotFound();

        var members = await repo.GetGroupMembersAsync(user.GroupId);

        // Attach vote counts
        var voteCounts = await Task.WhenAll(
            members.Select(async m =>
            {
                var votes = await repo.GetVotesForUserAsync(m.UserId);
                return (m, votes.Count);
            }));

        var memberDtos = voteCounts
            .Select(x => new GroupMemberDto(x.m.UserId, x.m.DisplayName, x.m.Email, x.Item2))
            .ToList();

        return Ok(new GroupDto(group.GroupId, group.Name, group.InviteCode, memberDtos));
    }

    private string GetUserId() => User.FindFirst("sub")?.Value ?? "";

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no ambiguous chars
        return new string(Enumerable.Range(0, 6)
            .Select(_ => chars[Random.Shared.Next(chars.Length)])
            .ToArray());
    }

    private static GroupDto ToDto(GroupRecord g, List<GroupMemberRecord> members) =>
        new(g.GroupId, g.Name, g.InviteCode,
            members.Select(m => new GroupMemberDto(m.UserId, m.DisplayName, m.Email, 0)).ToList());
}
