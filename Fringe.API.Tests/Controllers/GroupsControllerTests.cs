using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Fringe.API.Tests.Controllers;

/// <summary>Tests for GroupsController.</summary>
public sealed class GroupsControllerTests
{
    private const string userId = "user123";

    private static GroupsController BuildController(Mock<FringeRepository> mockRepo)
    {
        return new GroupsController(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("sub", userId)], "test"))
                }
            }
        };
    }

    private static Mock<FringeRepository> BuildMockRepo()
    {
        return new Mock<FringeRepository>(MockBehavior.Strict, Mock.Of<IDynamoDBContext>());
    }

    // ── CreateGroup ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroupUserAlreadyInGroupReturnsBadRequest()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = "existing-group"
        });
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.CreateGroup(new CreateGroupRequest("My Group")).ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task CreateGroupUserNotInGroupCreatesGroupAndJoins()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = null
        });
        GroupRecord? savedGroup = null;
        _ = mockRepo.Setup(r => r.CreateGroupAsync(It.IsAny<GroupRecord>()))
                .Callback<GroupRecord>(g => savedGroup = g)
                .Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.JoinGroupAsync(It.IsAny<string>(), userId, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.CreateGroup(new CreateGroupRequest("Festival Squad")).ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.Equal("Festival Squad", dto.Name);
        Assert.NotNull(savedGroup);
        Assert.Equal("Festival Squad", savedGroup!.Name);
        Assert.Equal(userId, savedGroup.OwnerId);
    }

    [Fact]
    public async Task CreateGroupNullUserStillCreatesGroup()
    {
        // If user record doesn't exist, user has no group — should proceed
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        _ = mockRepo.Setup(r => r.CreateGroupAsync(It.IsAny<GroupRecord>())).Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.JoinGroupAsync(It.IsAny<string>(), userId, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.CreateGroup(new CreateGroupRequest("New Group")).ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.Equal("New Group", dto.Name);
        Assert.Empty(dto.Members);
    }

    [Fact]
    public async Task CreateGroupGeneratesNonEmptyInviteCode()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        GroupRecord? savedGroup = null;
        _ = mockRepo.Setup(r => r.CreateGroupAsync(It.IsAny<GroupRecord>()))
                .Callback<GroupRecord>(g => savedGroup = g)
                .Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.JoinGroupAsync(It.IsAny<string>(), userId, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        GroupsController controller = BuildController(mockRepo);

        _ = await controller.CreateGroup(new CreateGroupRequest("Test")).ConfigureAwait(true);

        Assert.NotNull(savedGroup?.InviteCode);
        Assert.Equal(6, savedGroup!.InviteCode.Length);
        // Must use only chars from unambiguous set
        Assert.All(savedGroup.InviteCode, c => Assert.Contains(c, "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"));
    }

    [Fact]
    public async Task CreateGroupInviteCodeHasNoAmbiguousChars()
    {
        // Run many times to improve confidence the ambiguous characters are never used
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        _ = mockRepo.Setup(r => r.CreateGroupAsync(It.IsAny<GroupRecord>())).Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.JoinGroupAsync(It.IsAny<string>(), userId, It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);
        GroupsController controller = BuildController(mockRepo);

        for (int i = 0; i < 50; i++)
        {
            ActionResult<GroupDto> result = await controller.CreateGroup(new CreateGroupRequest("Test")).ConfigureAwait(true);
            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
            Assert.DoesNotContain('O', dto.InviteCode);
            Assert.DoesNotContain('I', dto.InviteCode);
            Assert.DoesNotContain('0', dto.InviteCode);
            Assert.DoesNotContain('1', dto.InviteCode);
        }
    }

    // ── JoinGroup ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGroupUserAlreadyInGroupReturnsBadRequest()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = "already-group"
        });
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.JoinGroup(new JoinGroupRequest("ABC123")).ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task JoinGroupInvalidCodeReturnsNotFound()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = null
        });
        _ = mockRepo.Setup(r => r.GetGroupByInviteCodeAsync("BADCODE"))
                .ReturnsAsync((GroupRecord?)null);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.JoinGroup(new JoinGroupRequest("badcode")).ConfigureAwait(true);

        _ = Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task JoinGroupValidCodeUppercasesCode()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = null
        });
        string? lookupCode = null;
        GroupRecord group = new() { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Test Group", OwnerId = "owner", InviteCode = "ABC123", CreatedAt = "" };
        _ = mockRepo.Setup(r => r.GetGroupByInviteCodeAsync(It.IsAny<string>()))
                .Callback<string>(c => lookupCode = c)
                .ReturnsAsync(group);
        _ = mockRepo.Setup(r => r.JoinGroupAsync("grp1", userId, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([]);
        GroupsController controller = BuildController(mockRepo);

        _ = await controller.JoinGroup(new JoinGroupRequest("abc123")).ConfigureAwait(true);

        Assert.Equal("ABC123", lookupCode);
    }

    [Fact]
    public async Task JoinGroupValidCodeReturnsGroupDto()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = null
        });
        GroupRecord group = new() { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Festival Squad", OwnerId = "owner", InviteCode = "XYZ789", CreatedAt = "" };
        _ = mockRepo.Setup(r => r.GetGroupByInviteCodeAsync("XYZ789")).ReturnsAsync(group);
        _ = mockRepo.Setup(r => r.JoinGroupAsync("grp1", userId, It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(
        [
            new GroupMemberRecord { Pk = "GROUP#grp1", Sk = "MEMBER#other", UserId = "other", DisplayName = "Other", Email = "other@e.com", JoinedAt = "" }
        ]);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.JoinGroup(new JoinGroupRequest("XYZ789")).ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.Equal("grp1", dto.GroupId);
        Assert.Equal("Festival Squad", dto.Name);
        Assert.Equal("XYZ789", dto.InviteCode);
        _ = Assert.Single(dto.Members);
    }

    // ── GetMyGroup ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMyGroupUserNotInGroupReturnsNotFound()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = null
        });
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.GetMyGroup().ConfigureAwait(true);

        _ = Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMyGroupUserNotFoundReturnsNotFound()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.GetMyGroup().ConfigureAwait(true);

        _ = Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMyGroupGroupRecordNotFoundReturnsNotFound()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupAsync("grp1")).ReturnsAsync((GroupRecord?)null);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.GetMyGroup().ConfigureAwait(true);

        _ = Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMyGroupWithMembersAttachesVoteCounts()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = "grp1"
        });
        GroupRecord group = new() { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Squad", OwnerId = userId, InviteCode = "ABC123", CreatedAt = "" };
        _ = mockRepo.Setup(r => r.GetGroupAsync("grp1")).ReturnsAsync(group);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(
        [
            new GroupMemberRecord { Pk = "GROUP#grp1", Sk = "MEMBER#user123", UserId = "user123", DisplayName = "User", Email = "u@example.com", JoinedAt = "" },
            new GroupMemberRecord { Pk = "GROUP#grp1", Sk = "MEMBER#user456", UserId = "user456", DisplayName = "Other", Email = "other@example.com", JoinedAt = "" },
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("user123")).ReturnsAsync(
        [
            new UserVoteRecord { Pk = "USER#user123", Sk = "VOTE#SHOW#1", Score = 1, UpdatedAt = "" },
            new UserVoteRecord { Pk = "USER#user123", Sk = "VOTE#SHOW#2", Score = 2, UpdatedAt = "" },
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("user456")).ReturnsAsync([]);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.GetMyGroup().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.Equal("grp1", dto.GroupId);
        Assert.Equal("Squad", dto.Name);
        Assert.Equal("ABC123", dto.InviteCode);
        Assert.Equal(2, dto.Members.Count);

        GroupMemberDto me = dto.Members.First(m => m.UserId == "user123");
        GroupMemberDto other = dto.Members.First(m => m.UserId == "user456");
        Assert.Equal(2, me.VoteCount);
        Assert.Equal(0, other.VoteCount);
    }

    [Fact]
    public async Task GetMyGroupNoVotesReturnsZeroVoteCountsForAllMembers()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@example.com",
            DisplayName = "User",
            GroupId = "grp1"
        });
        GroupRecord group = new() { Pk = "GROUP#grp1", GroupId = "grp1", Name = "Group", OwnerId = userId, InviteCode = "111222", CreatedAt = "" };
        _ = mockRepo.Setup(r => r.GetGroupAsync("grp1")).ReturnsAsync(group);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(
        [
            new GroupMemberRecord { Pk = "GROUP#grp1", Sk = "MEMBER#user123", UserId = "user123", DisplayName = "User", Email = "u@example.com", JoinedAt = "" }
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("user123")).ReturnsAsync([]);
        GroupsController controller = BuildController(mockRepo);

        ActionResult<GroupDto> result = await controller.GetMyGroup().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        GroupDto dto = Assert.IsType<GroupDto>(ok.Value);
        Assert.All(dto.Members, m => Assert.Equal(0, m.VoteCount));
    }
}
