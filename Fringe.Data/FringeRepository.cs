using System.Collections.ObjectModel;
using System.Globalization;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Fringe.Data.DynamoRecords;
using Fringe.Data.Models;

namespace Fringe.Data;

/// <summary>Data access layer for all Fringe DynamoDB operations.</summary>
public class FringeRepository(IDynamoDBContext db)
{
    // ── Shows ────────────────────────────────────────────────────────────────

    /// <summary>Saves or updates a batch of shows.</summary>
    public virtual async Task SaveShowsAsync(IEnumerable<Show> shows)
    {
        IBatchWrite<ShowRecord> batch = db.CreateBatchWrite<ShowRecord>();
        foreach (Show show in shows.DistinctBy(s => s.Id))
        {
            batch.AddPutItem(ToShowRecord(show));
        }
        await batch.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>Saves or updates a batch of show times.</summary>
    public virtual async Task SaveShowTimesAsync(IEnumerable<ShowTime> showTimes)
    {
        IBatchWrite<ShowTimeRecord> batch = db.CreateBatchWrite<ShowTimeRecord>();
        foreach (ShowTime st in showTimes.DistinctBy(st => (st.ShowId, st.DateTime)))
        {
            batch.AddPutItem(ToShowTimeRecord(st));
        }
        await batch.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>Returns all shows from the entity-type GSI.</summary>
    public virtual Task<List<ShowRecord>> GetAllShowsAsync()
    {
        return db.FromQueryAsync<ShowRecord>(new QueryOperationConfig
        {
            IndexName = "entity-type-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "entityType = :et",
                ExpressionAttributeValues = { [":et"] = new Primitive("SHOW") }
            }
        }).GetRemainingAsync();
    }

    /// <summary>Returns a single show by ID, or <see langword="null"/> if not found.</summary>
    public virtual async Task<ShowRecord?> GetShowAsync(int showId)
    {
        return await db.LoadAsync<ShowRecord>($"SHOW#{showId}", "METADATA").ConfigureAwait(false);
    }

    /// <summary>Returns all showtimes for the specified show.</summary>
    public virtual Task<List<ShowTimeRecord>> GetShowTimesForShowAsync(int showId)
    {
        return db.QueryAsync<ShowTimeRecord>(
                $"SHOW#{showId}",
                QueryOperator.BeginsWith,
                ["SHOWTIME#"],
                new QueryConfig())
          .GetRemainingAsync();
    }

    // ── Votes ────────────────────────────────────────────────────────────────

    /// <summary>Creates or updates a vote for a show.</summary>
    public virtual async Task UpsertVoteAsync(string userId, int showId, int rank)
    {
        await db.SaveAsync(new UserVoteRecord
        {
            Pk = $"USER#{userId}",
            Sk = $"VOTE#SHOW#{showId}",
            Score = rank,
            UpdatedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }).ConfigureAwait(false);
    }

    /// <summary>Deletes votes for the specified shows.</summary>
    public virtual async Task DeleteVotesAsync(string userId, IEnumerable<int> showIds)
    {
        ArgumentNullException.ThrowIfNull(showIds);
        IBatchWrite<UserVoteRecord> batch = db.CreateBatchWrite<UserVoteRecord>();
        foreach (int id in showIds)
        {
            batch.AddDeleteKey($"USER#{userId}", $"VOTE#SHOW#{id}");
        }
        await batch.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>Returns all votes cast by a user.</summary>
    public virtual Task<List<UserVoteRecord>> GetVotesForUserAsync(string userId)
    {
        return db.QueryAsync<UserVoteRecord>(
                $"USER#{userId}",
                QueryOperator.BeginsWith,
                ["VOTE#SHOW#"],
                new QueryConfig())
          .GetRemainingAsync();
    }

    // ── Users ────────────────────────────────────────────────────────────────

    /// <summary>Creates or replaces a user record.</summary>
    public virtual async Task UpsertUserAsync(UserRecord user)
    {
        await db.SaveAsync(user).ConfigureAwait(false);
    }

    /// <summary>Updates the display name for a user and their group member record.</summary>
    public virtual async Task UpdateDisplayNameAsync(string userId, string displayName)
    {
        UserRecord? user = await GetUserAsync(userId).ConfigureAwait(false);
        if (user == null)
        {
            return;
        }
        user.DisplayName = displayName;
        await db.SaveAsync(user).ConfigureAwait(false);

        if (user.GroupId != null)
        {
            GroupMemberRecord? member = await db.LoadAsync<GroupMemberRecord>($"GROUP#{user.GroupId}", $"MEMBER#{userId}").ConfigureAwait(false);
            if (member != null)
            {
                member.DisplayName = displayName;
                await db.SaveAsync(member).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Returns a user profile by ID, or <see langword="null"/> if not found.</summary>
    public virtual async Task<UserRecord?> GetUserAsync(string userId)
    {
        return await db.LoadAsync<UserRecord>($"USER#{userId}", "PROFILE").ConfigureAwait(false);
    }

    /// <summary>Deletes all data associated with a user (votes, group membership, profile).</summary>
    public virtual async Task DeleteUserDataAsync(string userId)
    {
        UserRecord? user = await GetUserAsync(userId).ConfigureAwait(false);

        List<Task> deleteTasks = [];

        List<UserVoteRecord> votes = await GetVotesForUserAsync(userId).ConfigureAwait(false);
        if (votes.Count > 0)
        {
            IBatchWrite<UserVoteRecord> voteBatch = db.CreateBatchWrite<UserVoteRecord>();
            foreach (UserVoteRecord vote in votes)
            {
                voteBatch.AddDeleteItem(vote);
            }
            deleteTasks.Add(voteBatch.ExecuteAsync());
        }

        if (user?.GroupId != null)
        {
            deleteTasks.Add(db.DeleteAsync<GroupMemberRecord>($"GROUP#{user.GroupId}", $"MEMBER#{userId}"));
        }

        if (user != null)
        {
            deleteTasks.Add(db.DeleteAsync(user));
        }

        await Task.WhenAll(deleteTasks).ConfigureAwait(false);
    }

    // ── Groups ───────────────────────────────────────────────────────────────

    /// <summary>Saves a new group and its associated invite code record.</summary>
    public virtual async Task CreateGroupAsync(GroupRecord group)
    {
        ArgumentNullException.ThrowIfNull(group);
        await db.SaveAsync(group).ConfigureAwait(false);
        await db.SaveAsync(new InviteCodeRecord
        {
            Pk = $"INVITE#{group.InviteCode}",
            GroupId = group.GroupId
        }).ConfigureAwait(false);
    }

    /// <summary>Returns a group by ID, or <see langword="null"/> if not found.</summary>
    public virtual async Task<GroupRecord?> GetGroupAsync(string groupId)
    {
        return await db.LoadAsync<GroupRecord>($"GROUP#{groupId}", "METADATA").ConfigureAwait(false);
    }

    /// <summary>Looks up a group by invite code, or returns <see langword="null"/> if not found.</summary>
    public virtual async Task<GroupRecord?> GetGroupByInviteCodeAsync(string inviteCode)
    {
        InviteCodeRecord? code = await db.LoadAsync<InviteCodeRecord>($"INVITE#{inviteCode}", "METADATA").ConfigureAwait(false);
        return code == null ? null : await GetGroupAsync(code.GroupId).ConfigureAwait(false);
    }

    /// <summary>Returns all members of a group.</summary>
    public virtual Task<List<GroupMemberRecord>> GetGroupMembersAsync(string groupId)
    {
        return db.QueryAsync<GroupMemberRecord>(
                $"GROUP#{groupId}",
                QueryOperator.BeginsWith,
                ["MEMBER#"],
                new QueryConfig())
          .GetRemainingAsync();
    }

    /// <summary>Adds a user to a group and updates their profile with the group ID.</summary>
    public virtual async Task JoinGroupAsync(string groupId, string userId, string displayName, string email)
    {
        await db.SaveAsync(new GroupMemberRecord
        {
            Pk = $"GROUP#{groupId}",
            Sk = $"MEMBER#{userId}",
            UserId = userId,
            DisplayName = displayName,
            Email = email,
            JoinedAt = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
        }).ConfigureAwait(false);

        UserRecord user = await GetUserAsync(userId).ConfigureAwait(false) ?? new UserRecord
        {
            Pk = $"USER#{userId}",
            Email = email,
            DisplayName = displayName
        };
        user.GroupId = groupId;
        await db.SaveAsync(user).ConfigureAwait(false);
    }

    // ── Availability ──────────────────────────────────────────────────────────

    /// <summary>Returns the availability record for a user, or <see langword="null"/> if not set.</summary>
    public virtual async Task<UserAvailabilityRecord?> GetAvailabilityAsync(string userId)
    {
        return await db.LoadAsync<UserAvailabilityRecord>($"USER#{userId}", "AVAILABILITY").ConfigureAwait(false);
    }

    /// <summary>Saves the availability windows for a user.</summary>
    public virtual async Task SaveAvailabilityAsync(string userId, Collection<AvailabilityWindowData> windows)
    {
        ArgumentNullException.ThrowIfNull(windows);
        UserAvailabilityRecord record = new()
        {
            Pk = $"USER#{userId}",
            Sk = "AVAILABILITY"
        };
        foreach (AvailabilityWindowData window in windows)
        {
            record.Windows.Add(window);
        }
        await db.SaveAsync(record).ConfigureAwait(false);
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static ShowRecord ToShowRecord(Show show)
    {
        return new ShowRecord
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
            Price = show.Price.ToString("F2", CultureInfo.InvariantCulture),
            Fee = show.Fee.ToString("F2", CultureInfo.InvariantCulture),
            FirstShowDate = show.FirstShowDate == DateOnly.MinValue
                ? null
                : show.FirstShowDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
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
    }

    private static ShowTimeRecord ToShowTimeRecord(ShowTime st)
    {
        return new ShowTimeRecord
        {
            Pk = $"SHOW#{st.ShowId}",
            Sk = $"SHOWTIME#{st.DateTime.ToString("O", CultureInfo.InvariantCulture)}",
            DateTime = st.DateTime.ToString("O", CultureInfo.InvariantCulture),
            PerformanceTime = st.PerformanceTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            PerformanceDate = st.PerformanceDate,
            PresentationFormat = st.PresentationFormat,
            Reserved = st.Reserved
        };
    }
}
