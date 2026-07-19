using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Collections.ObjectModel;
using System.Security.Claims;

namespace Fringe.API.Tests.Controllers;

/// <summary>Tests for AvailabilityController.</summary>
public sealed class AvailabilityControllerTests
{
    private const string userId = "user123";

    private static AvailabilityController BuildController(Mock<FringeRepository> mockRepo)
    {
        return new AvailabilityController(mockRepo.Object)
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

    // ── GetAvailability ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetAvailabilityNullRecordReturnsEmptyWindowList()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync((UserAvailabilityRecord?)null);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult<UserAvailabilityDto> result = await controller.GetAvailability().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserAvailabilityDto dto = Assert.IsType<UserAvailabilityDto>(ok.Value);
        Assert.Empty(dto.Windows);
    }

    [Fact]
    public async Task GetAvailabilityRecordWithEmptyWindowsReturnsEmpty()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        UserAvailabilityRecord record = new() { Pk = $"USER#{userId}", Sk = "AVAILABILITY" };
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync(record);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult<UserAvailabilityDto> result = await controller.GetAvailability().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserAvailabilityDto dto = Assert.IsType<UserAvailabilityDto>(ok.Value);
        Assert.Empty(dto.Windows);
    }

    [Fact]
    public async Task GetAvailabilityRecordWithWindowsMapsToDto()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        UserAvailabilityRecord record = new() { Pk = $"USER#{userId}", Sk = "AVAILABILITY" };
        record.Windows.Add(new AvailabilityWindowData { Start = "2025-07-15T09:00:00Z", End = "2025-07-15T17:00:00Z" });
        record.Windows.Add(new AvailabilityWindowData { Start = "2025-07-16T10:00:00Z", End = "2025-07-16T18:00:00Z" });
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync(record);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult<UserAvailabilityDto> result = await controller.GetAvailability().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserAvailabilityDto dto = Assert.IsType<UserAvailabilityDto>(ok.Value);
        Assert.Equal(2, dto.Windows.Count);
        Assert.Equal("2025-07-15T09:00:00Z", dto.Windows[0].Start);
        Assert.Equal("2025-07-15T17:00:00Z", dto.Windows[0].End);
        Assert.Equal("2025-07-16T10:00:00Z", dto.Windows[1].Start);
        Assert.Equal("2025-07-16T18:00:00Z", dto.Windows[1].End);
    }

    [Fact]
    public async Task GetAvailabilitySingleWindowMapsCorrectly()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        UserAvailabilityRecord record = new() { Pk = $"USER#{userId}", Sk = "AVAILABILITY" };
        record.Windows.Add(new AvailabilityWindowData { Start = "2025-07-20T08:00:00Z", End = "2025-07-20T22:00:00Z" });
        _ = mockRepo.Setup(r => r.GetAvailabilityAsync(userId)).ReturnsAsync(record);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult<UserAvailabilityDto> result = await controller.GetAvailability().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserAvailabilityDto dto = Assert.IsType<UserAvailabilityDto>(ok.Value);
        _ = Assert.Single(dto.Windows);
        Assert.Equal("2025-07-20T08:00:00Z", dto.Windows[0].Start);
        Assert.Equal("2025-07-20T22:00:00Z", dto.Windows[0].End);
    }

    // ── SaveAvailability ──────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAvailabilityEmptyWindowsCallsRepoWithEmptyList()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        Collection<AvailabilityWindowData>? saved = null;
        _ = mockRepo.Setup(r => r.SaveAvailabilityAsync(userId, It.IsAny<Collection<AvailabilityWindowData>>()))
                .Callback<string, Collection<AvailabilityWindowData>>((_, w) => saved = w)
                .Returns(Task.CompletedTask);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult result = await controller.SaveAvailability(new UserAvailabilityDto([])).ConfigureAwait(true);

        _ = Assert.IsType<OkResult>(result);
        Assert.NotNull(saved);
        Assert.Empty(saved!);
    }

    [Fact]
    public async Task SaveAvailabilityWithWindowsMapsToWindowData()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        Collection<AvailabilityWindowData>? saved = null;
        _ = mockRepo.Setup(r => r.SaveAvailabilityAsync(userId, It.IsAny<Collection<AvailabilityWindowData>>()))
                .Callback<string, Collection<AvailabilityWindowData>>((_, w) => saved = w)
                .Returns(Task.CompletedTask);
        AvailabilityController controller = BuildController(mockRepo);

        UserAvailabilityDto dto = new(
        [
            new AvailabilityWindowDto("2025-07-15T09:00:00Z", "2025-07-15T17:00:00Z"),
            new AvailabilityWindowDto("2025-07-16T10:00:00Z", "2025-07-16T18:00:00Z"),
        ]);
        ActionResult result = await controller.SaveAvailability(dto).ConfigureAwait(true);

        _ = Assert.IsType<OkResult>(result);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Count);
        Assert.Equal("2025-07-15T09:00:00Z", saved[0].Start);
        Assert.Equal("2025-07-15T17:00:00Z", saved[0].End);
        Assert.Equal("2025-07-16T10:00:00Z", saved[1].Start);
        Assert.Equal("2025-07-16T18:00:00Z", saved[1].End);
    }

    [Fact]
    public async Task SaveAvailabilityPassesUserIdToRepo()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        string? savedUserId = null;
        _ = mockRepo.Setup(r => r.SaveAvailabilityAsync(It.IsAny<string>(), It.IsAny<Collection<AvailabilityWindowData>>()))
                .Callback<string, Collection<AvailabilityWindowData>>((u, _) => savedUserId = u)
                .Returns(Task.CompletedTask);
        AvailabilityController controller = BuildController(mockRepo);

        _ = await controller.SaveAvailability(new UserAvailabilityDto([])).ConfigureAwait(true);

        Assert.Equal(userId, savedUserId);
    }

    [Fact]
    public async Task SaveAvailabilityReturnsOk()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.SaveAvailabilityAsync(userId, It.IsAny<Collection<AvailabilityWindowData>>()))
                .Returns(Task.CompletedTask);
        AvailabilityController controller = BuildController(mockRepo);

        ActionResult result = await controller.SaveAvailability(new UserAvailabilityDto([])).ConfigureAwait(true);

        _ = Assert.IsType<OkResult>(result);
    }
}
