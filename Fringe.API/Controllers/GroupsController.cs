using System.Globalization;
using System.Security.Cryptography;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fringe.API.Controllers;

/// <summary>Manages groups and group membership.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
internal sealed class GroupsController(FringeRepository repo) : ControllerBase
{
    /// <summary>Creates a new group and joins the current user to it.</summary>
    [HttpPost]
    public async Task<ActionResult<GroupDto>> CreateGroup([FromBody] CreateGroupRequest req)
    {
        string userId = GetUserId();

        UserRecord? user = await repo.GetUserAsync(userId).ConfigureAwait(false);
        if (user?.GroupId != null)
        {
            return BadRequest("You are already in a group.");
        }

        string groupId = Guid.NewGuid().ToString("N")[..12];
        string inviteCode = GenerateInviteCode();

        await repo.CreateGroupAsync(new GroupRecord
        {
            Pk = $"GROUP#{groupId}",
            GroupId = groupId,
            Name = req.Name,
            OwnerId = userId,
            InviteCode = inviteCode,
            CreatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }).ConfigureAwait(false);

        await repo.JoinGroupAsync(groupId, userId, user?.DisplayName ?? "", user?.Email ?? "").ConfigureAwait(false);

        return Ok(new GroupDto(groupId, req.Name, inviteCode, []));
    }

    /// <summary>Joins the current user to an existing group using an invite code.</summary>
    [HttpPost("join")]
    public async Task<ActionResult<GroupDto>> JoinGroup([FromBody] JoinGroupRequest req)
    {
        string userId = GetUserId();

        UserRecord? user = await repo.GetUserAsync(userId).ConfigureAwait(false);
        if (user?.GroupId != null)
        {
            return BadRequest("You are already in a group.");
        }

        GroupRecord? group = await repo.GetGroupByInviteCodeAsync(req.InviteCode.ToUpperInvariant()).ConfigureAwait(false);
        if (group == null)
        {
            return NotFound("Invalid invite code.");
        }

        await repo.JoinGroupAsync(group.GroupId, userId, user?.DisplayName ?? "", user?.Email ?? "").ConfigureAwait(false);

        List<GroupMemberRecord> members = await repo.GetGroupMembersAsync(group.GroupId).ConfigureAwait(false);
        return Ok(ToDto(group, members));
    }

    /// <summary>Returns the current user's group with member vote counts.</summary>
    [HttpGet("me")]
    public async Task<ActionResult<GroupDto>> GetMyGroup()
    {
        string userId = GetUserId();
        UserRecord? user = await repo.GetUserAsync(userId).ConfigureAwait(false);
        if (user?.GroupId == null)
        {
            return NotFound();
        }

        GroupRecord? group = await repo.GetGroupAsync(user.GroupId).ConfigureAwait(false);
        if (group == null)
        {
            return NotFound();
        }

        List<GroupMemberRecord> members = await repo.GetGroupMembersAsync(user.GroupId).ConfigureAwait(false);

        (GroupMemberRecord m, int)[] voteCounts = await Task.WhenAll(
            members.Select(async m =>
            {
                List<UserVoteRecord> votes = await repo.GetVotesForUserAsync(m.UserId).ConfigureAwait(false);
                return (m, votes.Count);
            })).ConfigureAwait(false);

        List<GroupMemberDto> memberDtos = [.. voteCounts.Select(x => new GroupMemberDto(x.m.UserId, x.m.DisplayName, x.m.Email, x.Item2))];

        return Ok(new GroupDto(group.GroupId, group.Name, group.InviteCode, memberDtos));
    }

    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value ?? "";
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string([.. Enumerable.Range(0, 6).Select(_ => chars[RandomNumberGenerator.GetInt32(chars.Length)])]);
    }

    private static GroupDto ToDto(GroupRecord g, List<GroupMemberRecord> members)
    {
        return new GroupDto(
            g.GroupId,
            g.Name,
            g.InviteCode,
            [.. members.Select(m => new GroupMemberDto(m.UserId, m.DisplayName, m.Email, 0))]);
    }
}
