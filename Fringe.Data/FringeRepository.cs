using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Fringe.Data.DynamoRecords;
using FringeScraper.Models;

namespace Fringe.Data;

public class FringeRepository(IDynamoDBContext db)
{
    // ── Shows ────────────────────────────────────────────────────────────────

    public async Task SaveShowsAsync(IEnumerable<Show> shows)
    {
        var batch = db.CreateBatchWrite<ShowRecord>();
        foreach (Show show in shows.DistinctBy(s => s.Id))
            batch.AddPutItem(ToShowRecord(show));
        await batch.ExecuteAsync();
    }

    public async Task SaveShowTimesAsync(IEnumerable<ShowTime> showTimes)
    {
        var batch = db.CreateBatchWrite<ShowTimeRecord>();
        foreach (ShowTime st in showTimes.DistinctBy(st => (st.ShowId, st.DateTime)))
            batch.AddPutItem(ToShowTimeRecord(st));
        await batch.ExecuteAsync();
    }

    public Task<List<ShowRecord>> GetAllShowsAsync() =>
        db.FromQueryAsync<ShowRecord>(new QueryOperationConfig
        {
            IndexName = "entity-type-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "entityType = :et",
                ExpressionAttributeValues = { [":et"] = new Primitive("SHOW") }
            }
        }).GetRemainingAsync();

    public async Task<ShowRecord?> GetShowAsync(int showId) =>
        await db.LoadAsync<ShowRecord>($"SHOW#{showId}", "METADATA");

    public Task<List<ShowTimeRecord>> GetShowTimesForShowAsync(int showId) =>
        db.QueryAsync<ShowTimeRecord>(
                $"SHOW#{showId}",
                QueryOperator.BeginsWith,
                ["SHOWTIME#"],
                new QueryConfig())
          .GetRemainingAsync();

    // ── Votes ────────────────────────────────────────────────────────────────

    public async Task UpsertVoteAsync(string userId, int showId, int rank) =>
        await db.SaveAsync(new UserVoteRecord
        {
            Pk = $"USER#{userId}",
            Sk = $"VOTE#SHOW#{showId}",
            Score = rank,
            UpdatedAt = DateTime.UtcNow.ToString("O")
        });

    public async Task DeleteVotesAsync(string userId, IEnumerable<int> showIds)
    {
        var batch = db.CreateBatchWrite<UserVoteRecord>();
        foreach (int id in showIds)
            batch.AddDeleteKey($"USER#{userId}", $"VOTE#SHOW#{id}");
        await batch.ExecuteAsync();
    }

    public Task<List<UserVoteRecord>> GetVotesForUserAsync(string userId) =>
        db.QueryAsync<UserVoteRecord>(
                $"USER#{userId}",
                QueryOperator.BeginsWith,
                ["VOTE#SHOW#"],
                new QueryConfig())
          .GetRemainingAsync();

    // ── Users ────────────────────────────────────────────────────────────────

    public async Task UpsertUserAsync(UserRecord user) =>
        await db.SaveAsync(user);

    public async Task<UserRecord?> GetUserAsync(string userId) =>
        await db.LoadAsync<UserRecord>($"USER#{userId}", "PROFILE");

    public async Task DeleteUserDataAsync(string userId)
    {
        var user = await GetUserAsync(userId);

        var deleteTasks = new List<Task>();

        var votes = await GetVotesForUserAsync(userId);
        if (votes.Count > 0)
        {
            var voteBatch = db.CreateBatchWrite<UserVoteRecord>();
            foreach (var vote in votes)
                voteBatch.AddDeleteItem(vote);
            deleteTasks.Add(voteBatch.ExecuteAsync());
        }

        if (user?.GroupId != null)
            deleteTasks.Add(db.DeleteAsync<GroupMemberRecord>($"GROUP#{user.GroupId}", $"MEMBER#{userId}"));

        if (user != null)
            deleteTasks.Add(db.DeleteAsync(user));

        await Task.WhenAll(deleteTasks);
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    public async Task CreateGroupAsync(GroupRecord group)
    {
        await db.SaveAsync(group);
        await db.SaveAsync(new InviteCodeRecord
        {
            Pk = $"INVITE#{group.InviteCode}",
            GroupId = group.GroupId
        });
    }

    public async Task<GroupRecord?> GetGroupAsync(string groupId) =>
        await db.LoadAsync<GroupRecord>($"GROUP#{groupId}", "METADATA");

    public async Task<GroupRecord?> GetGroupByInviteCodeAsync(string inviteCode)
    {
        var code = await db.LoadAsync<InviteCodeRecord>($"INVITE#{inviteCode}", "METADATA");
        if (code == null) return null;
        return await GetGroupAsync(code.GroupId);
    }

    public Task<List<GroupMemberRecord>> GetGroupMembersAsync(string groupId) =>
        db.QueryAsync<GroupMemberRecord>(
                $"GROUP#{groupId}",
                QueryOperator.BeginsWith,
                ["MEMBER#"],
                new QueryConfig())
          .GetRemainingAsync();

    public async Task JoinGroupAsync(string groupId, string userId, string displayName, string email)
    {
        await db.SaveAsync(new GroupMemberRecord
        {
            Pk = $"GROUP#{groupId}",
            Sk = $"MEMBER#{userId}",
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            JoinedAt = DateTime.UtcNow.ToString("O")
        });

        var user = await GetUserAsync(userId) ?? new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = email,
            DisplayName = displayName
        };
        user.GroupId = groupId;
        await db.SaveAsync(user);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static ShowRecord ToShowRecord(Show show) => new()
    {
        Pk = $"SHOW#{show.Id}",
        Sk = "METADATA",
        EntityType = "SHOW",
        ShowId = show.Id,
        Title = show.Title,
        Description = show.Description,
        PlainTextDescription = show.PlainTextDescription,
        ImageUrl = show.ImageUrl,
        Tag = show.Tag,
        Price = show.Price.ToString("F2"),
        Fee = show.Fee.ToString("F2"),
        FirstShowDate = show.FirstShowDate == DateOnly.MinValue ? null : show.FirstShowDate.ToString("yyyy-MM-dd"),
        LengthInMinutes = show.LengthInMinutes,
        Venue = new VenueData
        {
            VenueNumber = show.Venue.VenueNumber,
            Name = show.Venue.Name,
            Address = show.Venue.Address,
            Phone = show.Venue.Phone,
            PostalCode = show.Venue.PostalCode
        },
        ContentRating = new ContentRatingData
        {
            Name = show.ContentRating.Name,
            Code = show.ContentRating.Code,
            Description = show.ContentRating.Description
        }
    };

    private static ShowTimeRecord ToShowTimeRecord(ShowTime st) => new()
    {
        Pk = $"SHOW#{st.ShowId}",
        Sk = $"SHOWTIME#{st.DateTime:O}",
        DateTime = st.DateTime.ToString("O"),
        PerformanceTime = st.PerformanceTime.ToString("HH:mm"),
        PerformanceDate = st.PerformanceDate,
        PresentationFormat = st.PresentationFormat,
        Reserved = st.Reserved
    };
}
