using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Fringe.API.Tests.Controllers;

/// <summary>
/// Exhaustive tests for ScheduleController's scheduling algorithm.
///
/// Scheduling algorithm summary (BuildSchedule):
/// - Iterates voted shows in descending score order.
/// - For each show, tries each showtime in chronological order.
/// - Skips a showtime if it conflicts with an already-booked slot.
/// - Skips a showtime if any member (not excluded) has an availability window set
///   but none of their windows covers [start, end].
/// - When a member has NO windows (empty list) AND anyoneHasAvailability=true,
///   they are treated as never available (blocks everything).
/// - When NO member has availability (anyoneHasAvailability=false), empty windows
///   means unconstrained.
/// - First non-conflicting, available showtime wins; show is added and we move on.
/// - Final list is sorted by ShowTime.
/// </summary>
public sealed class ScheduleControllerTests
{
    private const string userId = "user123";

    private static ScheduleController BuildController(Mock<FringeRepository> mockRepo)
    {
        return new ScheduleController(mockRepo.Object)
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

    private static ShowRecord MakeShow(int id, string title, int lengthMinutes = 60)
    {
        return new()
        {
            Pk = $"SHOW#{id}",
            Sk = "METADATA",
            ShowId = id,
            Title = title,
            Price = "10.00",
            Fee = "1.00",
            LengthInMinutes = lengthMinutes,
        };
    }

    private static ShowTimeRecord MakeShowTime(int showId, string iso)
    {
        return new()
        {
            Pk = $"SHOW#{showId}",
            Sk = $"SHOWTIME#{iso}",
            DateTime = iso,
            PerformanceDate = "",
            PerformanceTime = "",
            PresentationFormat = ""
        };
    }

    private static GroupMemberRecord MakeMember(string memberId, string displayName = "")
    {
        return new()
        {
            Pk = "GROUP#grp1",
            Sk = $"MEMBER#{memberId}",
            UserId = memberId,
            DisplayName = displayName,
            Email = $"{memberId}@test.com",
            JoinedAt = ""
        };
    }

    private static UserVoteRecord MakeVote(string voteUserId, int showId, int score)
    {
        return new()
        {
            Pk = $"USER#{voteUserId}",
            Sk = $"VOTE#SHOW#{showId}",
            Score = score
        };
    }

    private static UserAvailabilityRecord MakeAvailability(string availUserId, params (string start, string end)[] windows)
    {
        UserAvailabilityRecord record = new() { Pk = $"USER#{availUserId}", Sk = "AVAILABILITY" };
        foreach ((string start, string end) in windows)
        {
            record.Windows.Add(new AvailabilityWindowData { Start = start, End = end });
        }

        return record;
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleUserNotInGroupReturnsBadRequest()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@test.com",
            DisplayName = "User",
            GroupId = null
        });
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetScheduleUserRecordNullReturnsBadRequest()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        _ = Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ── HasVotes=false ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleNoVotesInGroupReturnsHasVotesFalse()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.False(dto.HasVotes);
        Assert.Empty(dto.Items);
        Assert.Empty(dto.AlternateProposals);
        Assert.Empty(dto.MissedShows);
    }

    // ── Single member, no availability constraints ─────────────────────────────

    [Fact]
    public async Task GetScheduleSingleMemberNoAvailabilitySchedulesTopVotedShows()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        List<GroupMemberRecord> members = [MakeMember(userId)];
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);

        // Show 1 rank=1 (most wanted), Show 2 rank=2
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        List<ShowRecord> shows = [MakeShow(1, "Show One"), MakeShow(2, "Show Two")];
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(shows);

        // Non-overlapping times: 10:00–11:00 and 13:00–14:00
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T13:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.True(dto.HasVotes);
        Assert.Equal(2, dto.Items.Count);
        Assert.Empty(dto.MissedShows);
    }

    [Fact]
    public async Task GetScheduleSingleMemberShowSortedByShowTime()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Show 1 scored higher (rank 1 = 2 pts with 2 shows), Show 2 lower — but schedule should be time-sorted
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Late Show"),
            MakeShow(2, "Early Show")
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T19:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // Results sorted by showtime
        Assert.Equal("2025-07-15T10:00:00Z", dto.Items[0].ShowTime);
        Assert.Equal("2025-07-15T19:00:00Z", dto.Items[1].ShowTime);
    }

    // ── Conflict detection ────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleOverlappingShowTimesOnlyFirstScheduled()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        // Two shows with same rank (1 vote each)
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Both shows: 60 minutes at 10:00 — they overlap completely
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Show One", 60),
            MakeShow(2, "Show Two", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // Only one show should be scheduled
        _ = Assert.Single(dto.Items);
        _ = Assert.Single(dto.MissedShows);
    }

    [Fact]
    public async Task GetScheduleShowStartsBeforeOtherEndsConflictsDetected()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),  // scores: show 1 = 2pts, show 2 = 1pt
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Show 1: 10:00-11:00 (60 min), Show 2: 10:30-11:30 — overlap of 30 min
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "First", 60),
            MakeShow(2, "Overlap", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T10:30:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        _ = Assert.Single(dto.Items);
        Assert.Equal("First", dto.Items[0].Show.Title);
        _ = Assert.Single(dto.MissedShows);
        Assert.True(dto.MissedShows[0].ConflictsWithScheduled);
    }

    [Fact]
    public async Task GetScheduleShowBackToBackNoConflict()
    {
        // Show ends exactly when next starts — should NOT conflict
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Show 1: 10:00-11:00 (60min), Show 2: 11:00-12:00 — back to back, no overlap
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "First", 60),
            MakeShow(2, "Second", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T11:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.Equal(2, dto.Items.Count);
        Assert.Empty(dto.MissedShows);
    }

    [Fact]
    public async Task GetScheduleShowWithMultipleTimeslotsUsesFirstNonConflicting()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Show 1 scheduled at 10:00-11:00
        // Show 2 has slots at 10:00 (conflicts) and 13:00 (free) — should pick 13:00
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "First", 60),
            MakeShow(2, "Multi-slot", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
        [
            MakeShowTime(2, "2025-07-15T10:00:00Z"),
            MakeShowTime(2, "2025-07-15T13:00:00Z"),
        ]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.Equal(2, dto.Items.Count);
        ScheduleItemDto multiSlot = dto.Items.First(i => i.Show.Title == "Multi-slot");
        Assert.Equal("2025-07-15T13:00:00Z", multiSlot.ShowTime);
    }

    // ── Availability filtering ────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleMemberUnavailableDuringShowShowExcluded()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        // Give the member a real display name so it appears in BlockedByMembers
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId, "Alice")]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([MakeVote(userId, 1, 1)]);

        // Member is only available 14:00–20:00; show is at 10:00–11:00
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync(
            MakeAvailability(userId, ("2025-07-15T14:00:00Z", "2025-07-15T20:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Morning Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.Empty(dto.Items);
        _ = Assert.Single(dto.MissedShows);
        Assert.False(dto.MissedShows[0].ConflictsWithScheduled);
        Assert.Contains("Alice", dto.MissedShows[0].BlockedByMembers);
    }

    [Fact]
    public async Task GetScheduleMemberAvailableForShowShowScheduled()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId, "Alice")]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([MakeVote(userId, 1, 1)]);

        // Window covers 09:00–12:00; show is at 10:00–11:00 — fits
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync(
            MakeAvailability(userId, ("2025-07-15T09:00:00Z", "2025-07-15T12:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Morning Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        _ = Assert.Single(dto.Items);
        Assert.Empty(dto.MissedShows);
    }

    [Fact]
    public async Task GetScheduleNoMemberHasAvailabilityEveryoneUnconstrained()
    {
        // When anyoneHasAvailability=false, empty windows = no constraint
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1"),
            MakeMember("u2")
        ];
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync([MakeVote("u1", 1, 1)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        // Neither u1 nor u2 has set any availability
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync((UserAvailabilityRecord?)null);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync((UserAvailabilityRecord?)null);

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);

        // Override user to be u1
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "U1",
            GroupId = "grp1"
        });
        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // Unconstrained — show should be scheduled
        _ = Assert.Single(dto.Items);
    }

    [Fact]
    public async Task GetScheduleOneMemberHasAvailabilityOtherDoesNotOtherTreatedAsUnconstrained()
    {
        // IsAvailableForAll: when windows.Count == 0, it returns true (unconstrained).
        // So a member with no availability record is treated as always available,
        // even when anyoneHasAvailability=true (the ParseWindows branch returns []).
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1", "Alice"),
            MakeMember("u2", "Bob")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync([MakeVote("u1", 1, 1)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        // u1 has availability covering the show; u2 has no record → empty windows → unconstrained
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T09:00:00Z", "2025-07-15T20:00:00Z")));
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync((UserAvailabilityRecord?)null);

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // u1 is available (window covers show), u2 has no windows → treated as unconstrained
        // show IS scheduled
        _ = Assert.Single(dto.Items);
    }

    // ── Score computation ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleMultiMemberVotesScoresAggregatedCorrectly()
    {
        // 3 shows, 2 members, both rank show 1 highest → show 1 wins
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1"),
            MakeMember("u2")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "U1",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        // u1: show1=rank1 (3pts from 3 shows), show2=rank2 (2pts), show3=rank3 (1pt)
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync(
        [
            MakeVote("u1", 1, 1),
            MakeVote("u1", 2, 2),
            MakeVote("u1", 3, 3),
        ]);
        // u2: show1=rank1 (3pts), show3=rank2 (2pts)
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync(
        [
            MakeVote("u2", 1, 1),
            MakeVote("u2", 3, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync((UserAvailabilityRecord?)null);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync((UserAvailabilityRecord?)null);

        List<ShowRecord> shows = [MakeShow(1, "Favorite"), MakeShow(2, "Middle"), MakeShow(3, "Third")];
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(shows);
        // All non-overlapping
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync([MakeShowTime(2, "2025-07-15T12:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(3)).ReturnsAsync([MakeShowTime(3, "2025-07-15T14:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // Show 1: 3+3=6pts, Show 3: 1+2=3pts, Show 2: 2pts
        // All scheduled (no conflicts)
        // u1: 3 shows ranked → show1=rank1 → 3-1+1=3pts; u2: 2 shows ranked → show1=rank1 → 2-1+1=2pts
        // Total show1 = 3+2 = 5
        ScheduleItemDto? show1 = dto.Items.FirstOrDefault(i => i.Show.Title == "Favorite");
        Assert.NotNull(show1);
        Assert.Equal(5, show1!.GroupScore);
    }

    // ── Alternate proposals ───────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleExcludingMemberUnlocksMoreShowsGeneratesProposal()
    {
        // u2 blocks a show — excluding u2 allows it → alternate proposal generated
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1", "Alice"),
            MakeMember("u2", "Bob")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync(
        [
            MakeVote("u1", 1, 1),
            MakeVote("u1", 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        // u1 available all day; u2 only afternoon (blocks 10:00 show)
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T08:00:00Z", "2025-07-15T20:00:00Z")));
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync(
            MakeAvailability("u2", ("2025-07-15T14:00:00Z", "2025-07-15T20:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Morning Show", 60),
            MakeShow(2, "Afternoon Show", 60),
        ]);
        // Show 1 at 10:00 (u2 not available), Show 2 at 15:00 (both available)
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
            [MakeShowTime(2, "2025-07-15T15:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);

        // Main schedule: only Show 2 (u2 blocks Show 1)
        _ = Assert.Single(dto.Items);
        Assert.Equal("Afternoon Show", dto.Items[0].Show.Title);

        // Alternate proposal: excluding Bob adds 1 more show
        Assert.NotEmpty(dto.AlternateProposals);
        AlternateProposalDto proposal = dto.AlternateProposals.First(p => p.ExcludedMemberName == "Bob");
        Assert.Equal(2, proposal.Items.Count);
        Assert.Contains("1 more show", proposal.Description, StringComparison.Ordinal);
        Assert.Contains("Bob", proposal.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetScheduleExcludingMemberDoesNotAddShowsNoProposalGenerated()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members = [MakeMember("u1", "Alice")];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync([MakeVote("u1", 1, 1)]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T09:00:00Z", "2025-07-15T20:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        // Excluding the only member who is already available doesn't help
        Assert.Empty(dto.AlternateProposals);
    }

    [Fact]
    public async Task GetScheduleProposalDescriptionUsesCorrectGrammarPluralShows()
    {
        // Excluding member unlocks 2 shows → "shows" (plural)
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1", "Alice"),
            MakeMember("u2", "Bob")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync(
        [
            MakeVote("u1", 1, 1),
            MakeVote("u1", 2, 2),
            MakeVote("u1", 3, 3),
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        // u1 available all day; u2 only in the evening (blocks morning and afternoon shows)
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T08:00:00Z", "2025-07-15T23:00:00Z")));
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync(
            MakeAvailability("u2", ("2025-07-15T19:00:00Z", "2025-07-15T23:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Morning", 60),
            MakeShow(2, "Afternoon", 60),
            MakeShow(3, "Evening", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync([MakeShowTime(2, "2025-07-15T14:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(3)).ReturnsAsync([MakeShowTime(3, "2025-07-15T20:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);

        AlternateProposalDto? bobProposal = dto.AlternateProposals.FirstOrDefault(p => p.ExcludedMemberName == "Bob");
        Assert.NotNull(bobProposal);
        Assert.Contains("shows", bobProposal!.Description, StringComparison.Ordinal); // plural
    }

    // ── MissedShows ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleScheduledShowsNotInMissed()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Show A", 60),
            MakeShow(2, "Show B", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync([MakeShowTime(2, "2025-07-15T13:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        HashSet<int> scheduledIds = [.. dto.Items.Select(i => i.Show.ShowId)];
        HashSet<int> missedIds = [.. dto.MissedShows.Select(m => m.Show.ShowId)];
        Assert.Empty(scheduledIds.Intersect(missedIds));
    }

    [Fact]
    public async Task GetScheduleMissedShowConflictsWithScheduledFlagSet()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
            MakeVote(userId, 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Show 1 at 10:00-11:00, Show 2 also at 10:00-11:00 — conflict
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "A", 60), MakeShow(2, "B", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync([MakeShowTime(2, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        _ = Assert.Single(dto.MissedShows);
        Assert.True(dto.MissedShows[0].ConflictsWithScheduled);
        Assert.Empty(dto.MissedShows[0].BlockedByMembers);
    }

    [Fact]
    public async Task GetScheduleMissedShowBlockedByMemberBlockerListed()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1", "Alice"),
            MakeMember("u2", "Bob")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync([MakeVote("u1", 1, 1)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T09:00:00Z", "2025-07-15T20:00:00Z")));
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync(
            MakeAvailability("u2", ("2025-07-15T14:00:00Z", "2025-07-15T20:00:00Z")));

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Morning Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync(
            [MakeShowTime(1, "2025-07-15T10:00:00Z")]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.Empty(dto.Items);
        _ = Assert.Single(dto.MissedShows);
        Assert.False(dto.MissedShows[0].ConflictsWithScheduled);
        Assert.Contains("Bob", dto.MissedShows[0].BlockedByMembers);
    }

    [Fact]
    public async Task GetScheduleMissedShowWithMultipleTimeslotsChecksAllTimesForBlockers()
    {
        // A show has two timeslots: slot A conflicts with scheduled show, slot B is blocked by member.
        // MissedShow should have ConflictsWithScheduled=true AND the blocker listed from slot B.
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<GroupMemberRecord> members =
        [
            MakeMember("u1", "Alice"),
            MakeMember("u2", "Bob")
        ];
        _ = mockRepo.Setup(r => r.GetUserAsync("u1")).ReturnsAsync(new UserRecord
        {
            Pk = "USER#u1",
            Email = "u1@test.com",
            DisplayName = "Alice",
            GroupId = "grp1"
        });
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync(members);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u1")).ReturnsAsync(
        [
            MakeVote("u1", 1, 1),
            MakeVote("u1", 2, 2),
        ]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync("u2")).ReturnsAsync([]);

        // Both available for Show 1 at 10:00; Bob not available for Show 2 at 10:00 or 13:00
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u1")).ReturnsAsync(
            MakeAvailability("u1", ("2025-07-15T08:00:00Z", "2025-07-15T20:00:00Z")));
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync("u2")).ReturnsAsync(
            MakeAvailability("u2", ("2025-07-15T08:00:00Z", "2025-07-15T11:30:00Z"))); // only until 11:30

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "First", 60), MakeShow(2, "Missed", 60)]);
        // Show 1: 10:00-11:00 (both available)
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        // Show 2: 10:30 (conflicts with Show 1) and 13:00 (Bob not available)
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(2)).ReturnsAsync(
        [
            MakeShowTime(2, "2025-07-15T10:30:00Z"),  // conflicts
            MakeShowTime(2, "2025-07-15T13:00:00Z"),  // Bob unavailable
        ]);

        ScheduleController controller = new(mockRepo.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "u1")], "test"))
                }
            }
        };

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        _ = Assert.Single(dto.Items); // Show 1 scheduled
        _ = Assert.Single(dto.MissedShows);
        Assert.True(dto.MissedShows[0].ConflictsWithScheduled); // 10:30 slot conflicts
        Assert.Contains("Bob", dto.MissedShows[0].BlockedByMembers); // 13:00 blocked by Bob
    }

    [Fact]
    public async Task GetScheduleShowNotInVotedListNotInMissedShows()
    {
        // Shows in DB that nobody voted for should not appear in missed shows
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            MakeVote(userId, 1, 1),
        ]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        // Shows 1 and 2 in DB, but only show 1 was voted
        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync(
        [
            MakeShow(1, "Voted Show", 60),
            MakeShow(2, "Unvoted Show", 60),
        ]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.DoesNotContain(dto.MissedShows, m => m.Show.Title == "Unvoted Show");
    }

    // ── GroupScore in schedule items ──────────────────────────────────────────

    [Fact]
    public async Task GetScheduleScheduledItemHasCorrectGroupScore()
    {
        // 1 member, 1 vote at rank 1 with total 1 show ranked → 1 point (totalRanked - score + 1 = 1-1+1=1)
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([MakeVote(userId, 1, 1)]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "Show", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([MakeShowTime(1, "2025-07-15T10:00:00Z")]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        _ = Assert.Single(dto.Items);
        Assert.Equal(1, dto.Items[0].GroupScore);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetScheduleShowWithNoShowTimesSkippedSilently()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([MakeMember(userId)]);
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([MakeVote(userId, 1, 1)]);
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);

        _ = mockRepo.Setup(r => r.GetAllShowsAsync()).ReturnsAsync([MakeShow(1, "No Times", 60)]);
        _ = mockRepo.Setup(r => r.GetShowTimesForShowAsync(1)).ReturnsAsync([]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.True(dto.HasVotes);
        Assert.Empty(dto.Items);
    }

    [Fact]
    public async Task GetScheduleNoGroupMembersStillReturnsHasVotesFalse()
    {
        // Edge case: no members means no votes, return HasVotes=false
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        SetupUserInGroup(mockRepo);
        _ = mockRepo.Setup(r => r.GetGroupMembersAsync("grp1")).ReturnsAsync([]);
        ScheduleController controller = BuildController(mockRepo);

        ActionResult<ScheduleResponseDto> result = await controller.GetSchedule().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        ScheduleResponseDto dto = Assert.IsType<ScheduleResponseDto>(ok.Value);
        Assert.False(dto.HasVotes);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void SetupUserInGroup(Mock<FringeRepository> mockRepo)
    {
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "u@test.com",
            DisplayName = "User",
            GroupId = "grp1"
        });
    }
}
