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
        foreach (Show show in shows)
            batch.AddPutItem(ToShowRecord(show));
        await batch.ExecuteAsync();
    }

    public async Task SaveShowTimesAsync(IEnumerable<ShowTime> showTimes)
    {
        var batch = db.CreateBatchWrite<ShowTimeRecord>();
        foreach (ShowTime st in showTimes)
            batch.AddPutItem(ToShowTimeRecord(st));
        await batch.ExecuteAsync();
    }

    public Task<List<ShowRecord>> GetAllShowsAsync() =>
        db.QueryAsync<ShowRecord>("SHOW", new QueryConfig { IndexName = "entity-type-index" })
          .GetRemainingAsync();

    public Task<ShowRecord?> GetShowAsync(int showId) =>
        db.LoadAsync<ShowRecord>($"SHOW#{showId}", "METADATA");

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

    public Task<UserRecord?> GetUserAsync(string userId) =>
        db.LoadAsync<UserRecord>($"USER#{userId}", "PROFILE");

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

    public Task<GroupRecord?> GetGroupAsync(string groupId) =>
        db.LoadAsync<GroupRecord>($"GROUP#{groupId}", "METADATA");

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
