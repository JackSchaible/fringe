using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Fringe.API.Tests.Controllers;

/// <summary>Tests for VotesController.</summary>
public sealed class VotesControllerTests
{
    private const string userId = "user123";

    private static VotesController BuildController(Mock<FringeRepository> mockRepo)
    {
        return new VotesController(mockRepo.Object)
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

    // ── GetVotes ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetVotesReturnsEmptyListWhenNoVotes()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([]);
        VotesController controller = BuildController(mockRepo);

        List<VoteDto> result = await controller.GetVotes().ConfigureAwait(true);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetVotesParsesSkAndMapsToDto()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#42", Score = 1, UpdatedAt = "2025-01-01" },
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#99", Score = 3, UpdatedAt = "2025-01-01" },
        ]);
        VotesController controller = BuildController(mockRepo);

        List<VoteDto> result = await controller.GetVotes().ConfigureAwait(true);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, v => v.ShowId == 42 && v.Rank == 1);
        Assert.Contains(result, v => v.ShowId == 99 && v.Rank == 3);
    }

    [Fact]
    public async Task GetVotesSingleVoteCorrectParsing()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#1234", Score = 2, UpdatedAt = "2025-01-01" },
        ]);
        VotesController controller = BuildController(mockRepo);

        List<VoteDto> result = await controller.GetVotes().ConfigureAwait(true);

        _ = Assert.Single(result);
        Assert.Equal(1234, result[0].ShowId);
        Assert.Equal(2, result[0].Rank);
    }

    // ── SaveVotes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveVotesEmptyListDeletesAllExisting()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        List<UserVoteRecord> existing =
        [
            new() { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#10", Score = 1, UpdatedAt = "2025-01-01" },
            new() { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#20", Score = 2, UpdatedAt = "2025-01-01" },
        ];
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(existing);
        // Deleted show IDs should be 10 and 20 — capture what was deleted
        IEnumerable<int>? capturedDeletes = null;
        _ = mockRepo.Setup(r => r.DeleteVotesAsync(userId, It.IsAny<IEnumerable<int>>()))
                .Callback<string, IEnumerable<int>>((_, ids) => capturedDeletes = ids.ToList())
                .Returns(Task.CompletedTask);

        VotesController controller = BuildController(mockRepo);
        IActionResult result = await controller.SaveVotes([]).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.NotNull(capturedDeletes);
        Assert.Contains(10, capturedDeletes!);
        Assert.Contains(20, capturedDeletes!);
    }

    [Fact]
    public async Task SaveVotesNewListUpsertsAllVotes()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([]);
        _ = mockRepo.Setup(r => r.DeleteVotesAsync(userId, It.IsAny<IEnumerable<int>>())).Returns(Task.CompletedTask);

        List<(string userId, int showId, int rank)> upsertCalls = [];
        _ = mockRepo.Setup(r => r.UpsertVoteAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .Callback<string, int, int>((u, s, r2) => upsertCalls.Add((u, s, r2)))
                .Returns(Task.CompletedTask);

        VotesController controller = BuildController(mockRepo);
        List<VoteDto> votes = [new(5, 1), new(7, 2)];
        IActionResult result = await controller.SaveVotes(votes).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.Equal(2, upsertCalls.Count);
        Assert.Contains(upsertCalls, x => x.userId == userId && x.showId == 5 && x.rank == 1);
        Assert.Contains(upsertCalls, x => x.userId == userId && x.showId == 7 && x.rank == 2);
    }

    [Fact]
    public async Task SaveVotesDeletesShowsRemovedFromList()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        // Show 10 and 30 already exist; new list only contains 30 → 10 should be deleted
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#10", Score = 2, UpdatedAt = "" },
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#30", Score = 1, UpdatedAt = "" },
        ]);
        IEnumerable<int>? deleted = null;
        _ = mockRepo.Setup(r => r.DeleteVotesAsync(userId, It.IsAny<IEnumerable<int>>()))
                .Callback<string, IEnumerable<int>>((_, ids) => deleted = ids.ToList())
                .Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.UpsertVoteAsync(userId, It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);

        VotesController controller = BuildController(mockRepo);
        _ = await controller.SaveVotes([new VoteDto(30, 1)]).ConfigureAwait(true);

        Assert.NotNull(deleted);
        Assert.Contains(10, deleted!);
        Assert.DoesNotContain(30, deleted!);
    }

    [Fact]
    public async Task SaveVotesDoesNotDeleteShowsStillInNewList()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync(
        [
            new UserVoteRecord { Pk = $"USER#{userId}", Sk = "VOTE#SHOW#5", Score = 1, UpdatedAt = "" },
        ]);
        IEnumerable<int>? deleted = null;
        _ = mockRepo.Setup(r => r.DeleteVotesAsync(userId, It.IsAny<IEnumerable<int>>()))
                .Callback<string, IEnumerable<int>>((_, ids) => deleted = ids.ToList())
                .Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.UpsertVoteAsync(userId, It.IsAny<int>(), It.IsAny<int>())).Returns(Task.CompletedTask);

        VotesController controller = BuildController(mockRepo);
        _ = await controller.SaveVotes([new VoteDto(5, 1)]).ConfigureAwait(true);

        Assert.NotNull(deleted);
        Assert.DoesNotContain(5, deleted!);
    }

    [Fact]
    public async Task SaveVotesReturnsNoContent()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetVotesForUserAsync(userId)).ReturnsAsync([]);
        _ = mockRepo.Setup(r => r.DeleteVotesAsync(userId, It.IsAny<IEnumerable<int>>())).Returns(Task.CompletedTask);
        _ = mockRepo.Setup(r => r.UpsertVoteAsync(userId, 1, 1)).Returns(Task.CompletedTask);

        VotesController controller = BuildController(mockRepo);
        IActionResult result = await controller.SaveVotes([new VoteDto(1, 1)]).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
    }
}
