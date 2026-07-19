using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2.DataModel;
using Fringe.API.Controllers;
using Fringe.Data;
using Fringe.Data.DynamoRecords;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Fringe.API.Tests.Controllers;

/// <summary>Tests for UsersController.</summary>
public sealed class UsersControllerTests : IDisposable
{
    private const string userId = "user123";

    // Store original env var to restore after each test
    private readonly string? originalPoolId = Environment.GetEnvironmentVariable("COGNITO_USER_POOL_ID");

    private static UsersController BuildController(
        Mock<FringeRepository> mockRepo,
        Mock<IAmazonCognitoIdentityProvider>? mockCognito = null,
        string? cognitoUsername = null)
    {
        Mock<IAmazonCognitoIdentityProvider> cognito = mockCognito ?? new Mock<IAmazonCognitoIdentityProvider>(MockBehavior.Strict);
        List<Claim> claims = [new Claim("sub", userId)];
        if (cognitoUsername != null)
        {
            claims.Add(new Claim("cognito:username", cognitoUsername));
        }

        return new UsersController(mockRepo.Object, cognito.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    private static Mock<FringeRepository> BuildMockRepo()
    {
        return new Mock<FringeRepository>(MockBehavior.Strict, Mock.Of<IDynamoDBContext>());
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Restore env var
        if (originalPoolId == null)
        {
            Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", originalPoolId);
        }

        GC.SuppressFinalize(this);
    }

    // ── GetMe ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMeUserNotFoundReturns404()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        UsersController controller = BuildController(mockRepo);

        ActionResult<UserDto> result = await controller.GetMe().ConfigureAwait(true);

        _ = Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetMeUserFoundReturnsUserDto()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "user@example.com",
            DisplayName = "Test User",
            GroupId = "group-abc"
        });
        UsersController controller = BuildController(mockRepo);

        ActionResult<UserDto> result = await controller.GetMe().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserDto dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Equal(userId, dto.UserId);
        Assert.Equal("user@example.com", dto.Email);
        Assert.Equal("Test User", dto.DisplayName);
        Assert.Equal("group-abc", dto.GroupId);
    }

    [Fact]
    public async Task GetMeUserWithoutGroupReturnsNullGroupId()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = "solo@example.com",
            DisplayName = "Solo User",
            GroupId = null
        });
        UsersController controller = BuildController(mockRepo);

        ActionResult<UserDto> result = await controller.GetMe().ConfigureAwait(true);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        UserDto dto = Assert.IsType<UserDto>(ok.Value);
        Assert.Null(dto.GroupId);
    }

    // ── UpsertMe ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertMeNewUserCreatesRecordAndReturnsNoContent()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync((UserRecord?)null);
        UserRecord? saved = null;
        _ = mockRepo.Setup(r => r.UpsertUserAsync(It.IsAny<UserRecord>()))
                .Callback<UserRecord>(u => saved = u)
                .Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        IActionResult result = await controller.UpsertMe(new UpsertUserRequest("new@example.com", "New User")).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.NotNull(saved);
        Assert.Equal($"USER#{userId}", saved!.Pk);
        Assert.Equal("new@example.com", saved.Email);
        Assert.Equal("New User", saved.DisplayName);
    }

    [Fact]
    public async Task UpsertMeExistingUserUpdatesFieldsAndReturnsNoContent()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        UserRecord existing = new()
        {
            Pk = $"USER#{userId}",
            Email = "old@example.com",
            DisplayName = "Old Name",
            GroupId = "grp-1"
        };
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(existing);
        UserRecord? saved = null;
        _ = mockRepo.Setup(r => r.UpsertUserAsync(It.IsAny<UserRecord>()))
                .Callback<UserRecord>(u => saved = u)
                .Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        IActionResult result = await controller.UpsertMe(new UpsertUserRequest("new@example.com", "New Name")).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.NotNull(saved);
        // Same object mutated in place
        Assert.Equal("grp-1", saved!.GroupId); // group should be preserved
        Assert.Equal("new@example.com", saved.Email);
        Assert.Equal("New Name", saved.DisplayName);
    }

    [Fact]
    public async Task UpsertMeExistingUserPreservesGroupId()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        UserRecord existing = new()
        {
            Pk = $"USER#{userId}",
            Email = "e@example.com",
            DisplayName = "Existing",
            GroupId = "preserved-group"
        };
        _ = mockRepo.Setup(r => r.GetUserAsync(userId)).ReturnsAsync(existing);
        UserRecord? saved = null;
        _ = mockRepo.Setup(r => r.UpsertUserAsync(It.IsAny<UserRecord>()))
                .Callback<UserRecord>(u => saved = u)
                .Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        _ = await controller.UpsertMe(new UpsertUserRequest("e@example.com", "Existing")).ConfigureAwait(true);

        Assert.Equal("preserved-group", saved!.GroupId);
    }

    // ── UpdateDisplayName ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDisplayNameTrimsWhitespaceAndReturnsNoContent()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        string? savedName = null;
        _ = mockRepo.Setup(r => r.UpdateDisplayNameAsync(userId, It.IsAny<string>()))
                .Callback<string, string>((_, n) => savedName = n)
                .Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        IActionResult result = await controller.UpdateDisplayName(new UpdateDisplayNameRequest("  Trimmed Name  ")).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.Equal("Trimmed Name", savedName);
    }

    [Fact]
    public async Task UpdateDisplayNameNoLeadingTrailingSpacesPassesThrough()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        string? savedName = null;
        _ = mockRepo.Setup(r => r.UpdateDisplayNameAsync(userId, It.IsAny<string>()))
                .Callback<string, string>((_, n) => savedName = n)
                .Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        _ = await controller.UpdateDisplayName(new UpdateDisplayNameRequest("Clean Name")).ConfigureAwait(true);

        Assert.Equal("Clean Name", savedName);
    }

    [Fact]
    public async Task UpdateDisplayNameReturnsNoContent()
    {
        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.UpdateDisplayNameAsync(userId, It.IsAny<string>())).Returns(Task.CompletedTask);
        UsersController controller = BuildController(mockRepo);

        IActionResult result = await controller.UpdateDisplayName(new UpdateDisplayNameRequest("Name")).ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
    }

    // ── DeleteMe ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteMeNoCognitoPoolIdDoesNotCallCognito()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", null);

        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.DeleteUserDataAsync(userId)).Returns(Task.CompletedTask);

        Mock<IAmazonCognitoIdentityProvider> mockCognito = new(MockBehavior.Strict);
        // No Cognito setup — strict mock will fail if any method is called
        UsersController controller = BuildController(mockRepo, mockCognito);

        IActionResult result = await controller.DeleteMe().ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        mockCognito.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteMeWithCognitoPoolIdCallsAdminDeleteUser()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "us-east-1_TestPool");

        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.DeleteUserDataAsync(userId)).Returns(Task.CompletedTask);

        Mock<IAmazonCognitoIdentityProvider> mockCognito = new();
        AdminDeleteUserRequest? captured = null;
        _ = mockCognito.Setup(c => c.AdminDeleteUserAsync(It.IsAny<AdminDeleteUserRequest>(), default))
                   .Callback<AdminDeleteUserRequest, CancellationToken>((req, _) => captured = req)
                   .ReturnsAsync(new AdminDeleteUserResponse());

        // cognito:username claim present
        UsersController controller = BuildController(mockRepo, mockCognito, "testuser");

        IActionResult result = await controller.DeleteMe().ConfigureAwait(true);

        _ = Assert.IsType<NoContentResult>(result);
        Assert.NotNull(captured);
        Assert.Equal("us-east-1_TestPool", captured!.UserPoolId);
        Assert.Equal("testuser", captured.Username);
    }

    [Fact]
    public async Task DeleteMeWithCognitoPoolIdFallsBackToSubWhenNoCognitoUsernameClaim()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", "us-east-1_TestPool");

        Mock<FringeRepository> mockRepo = BuildMockRepo();
        _ = mockRepo.Setup(r => r.DeleteUserDataAsync(userId)).Returns(Task.CompletedTask);

        Mock<IAmazonCognitoIdentityProvider> mockCognito = new();
        AdminDeleteUserRequest? captured = null;
        _ = mockCognito.Setup(c => c.AdminDeleteUserAsync(It.IsAny<AdminDeleteUserRequest>(), default))
                   .Callback<AdminDeleteUserRequest, CancellationToken>((req, _) => captured = req)
                   .ReturnsAsync(new AdminDeleteUserResponse());

        // No cognitoUsername → falls back to userId
        UsersController controller = BuildController(mockRepo, mockCognito, cognitoUsername: null);

        _ = await controller.DeleteMe().ConfigureAwait(true);

        Assert.Equal(userId, captured?.Username);
    }

    [Fact]
    public async Task DeleteMeAlwaysDeletesRepoData()
    {
        Environment.SetEnvironmentVariable("COGNITO_USER_POOL_ID", null);

        Mock<FringeRepository> mockRepo = BuildMockRepo();
        bool repoCalled = false;
        _ = mockRepo.Setup(r => r.DeleteUserDataAsync(userId))
                .Callback(() => repoCalled = true)
                .Returns(Task.CompletedTask);

        UsersController controller = BuildController(mockRepo);

        _ = await controller.DeleteMe().ConfigureAwait(true);

        Assert.True(repoCalled);
    }
}
