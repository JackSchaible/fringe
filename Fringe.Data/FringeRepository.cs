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

    // ── Venues ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves or updates a batch of canonical venues. Imports own only the festival-provided
    /// fields (name, address, phone, postal code); a separate enrichment process owns
    /// everything else on <see cref="VenueRecord"/> (e.g. <see cref="VenueRecord.Latitude"/>/
    /// <see cref="VenueRecord.Longitude"/>). To avoid clobbering enrichment-owned attributes
    /// via DynamoDB's whole-item PutItem semantics, an existing record is mutated in place
    /// rather than replaced wholesale, and venues whose festival-owned fields are unchanged
    /// are skipped entirely so shows changing doesn't cause churn on unrelated venues.
    /// </summary>
    public virtual async Task SaveVenuesAsync(IEnumerable<Venue> venues)
    {
        foreach (Venue venue in venues.DistinctBy(v => v.VenueNumber))
        {
            VenueRecord? existing = await GetVenueAsync(venue.VenueNumber).ConfigureAwait(false);
            if (existing != null && !FestivalFieldsChanged(existing, venue))
            {
                continue;
            }

            VenueRecord record = existing ?? new VenueRecord
            {
                Pk = $"VENUE#{venue.VenueNumber}",
                Sk = "METADATA",
                EntityType = "VENUE"
            };
            record.VenueNumber = venue.VenueNumber;
            record.Name = venue.Name;
            record.Address = venue.Address;
            record.Phone = venue.Phone;
            record.PostalCode = venue.PostalCode;

            await db.SaveAsync(record).ConfigureAwait(false);
        }
    }

    private static bool FestivalFieldsChanged(VenueRecord existing, Venue venue)
    {
        return !string.Equals(existing.Name, venue.Name, StringComparison.Ordinal)
            || !string.Equals(existing.Address, venue.Address, StringComparison.Ordinal)
            || !string.Equals(existing.Phone, venue.Phone, StringComparison.Ordinal)
            || !string.Equals(existing.PostalCode, venue.PostalCode, StringComparison.Ordinal);
    }

    /// <summary>Returns all venues from the entity-type GSI.</summary>
    public virtual Task<List<VenueRecord>> GetAllVenuesAsync()
    {
        return db.FromQueryAsync<VenueRecord>(new QueryOperationConfig
        {
            IndexName = "entity-type-index",
            KeyExpression = new Expression
            {
                ExpressionStatement = "entityType = :et",
                ExpressionAttributeValues = { [":et"] = new Primitive("VENUE") }
            }
        }).GetRemainingAsync();
    }

    /// <summary>Returns a single venue by number, or <see langword="null"/> if not found.</summary>
    public virtual async Task<VenueRecord?> GetVenueAsync(int venueNumber)
    {
        return await db.LoadAsync<VenueRecord>($"VENUE#{venueNumber}", "METADATA").ConfigureAwait(false);
    }

    /// <summary>
    /// The <see cref="VenueRecord.CoordinateSource"/> value for a human-confirmed coordinate
    /// override. Once set, automatic geocoding must never overwrite it.
    /// </summary>
    public const string ManualCoordinateSource = "Manual";

    /// <summary>
    /// Returns venues eligible for (re-)geocoding: those with no coordinates yet, and those
    /// whose routing-relevant address fields (see <see cref="VenueAddressHasher"/>) have
    /// changed since they were last geocoded. Venues with a manually confirmed coordinate
    /// are never eligible, and an unchanged venue is never returned, so re-importing shows
    /// or unrelated venue fields (name, phone) does not trigger geocoding.
    /// </summary>
    public virtual async Task<List<VenueRecord>> GetVenuesNeedingGeocodingAsync()
    {
        List<VenueRecord> venues = await GetAllVenuesAsync().ConfigureAwait(false);
        return [.. venues.Where(NeedsGeocoding)];
    }

    private static bool NeedsGeocoding(VenueRecord venue)
    {
        if (string.Equals(venue.CoordinateSource, ManualCoordinateSource, StringComparison.Ordinal))
        {
            return false;
        }
        if (venue.Latitude == null || venue.Longitude == null)
        {
            return true;
        }
        string currentHash = VenueAddressHasher.ComputeHash(venue.VenueNumber, venue.Address, venue.PostalCode);
        return !string.Equals(venue.AddressHash, currentHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sets a venue's coordinates and marks them as sourced from <paramref name="coordinateSource"/>
    /// (a geocoding provider name, or <see cref="ManualCoordinateSource"/> for a human override).
    /// The address hash is recomputed from the venue's current address fields at save time, so it
    /// always reflects what was actually geocoded. Returns <see langword="false"/> without writing
    /// if the venue doesn't exist, or if it already carries a manual override and
    /// <paramref name="coordinateSource"/> is not itself <see cref="ManualCoordinateSource"/> —
    /// automatic geocoding must never overwrite a manually confirmed coordinate.
    /// </summary>
    public virtual async Task<bool> UpdateVenueCoordinatesAsync(int venueNumber, double latitude, double longitude, string coordinateSource, DateTime enrichedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(coordinateSource);

        VenueRecord? existing = await GetVenueAsync(venueNumber).ConfigureAwait(false);
        if (existing == null)
        {
            return false;
        }
        if (string.Equals(existing.CoordinateSource, ManualCoordinateSource, StringComparison.Ordinal)
            && !string.Equals(coordinateSource, ManualCoordinateSource, StringComparison.Ordinal))
        {
            return false;
        }

        existing.Latitude = latitude;
        existing.Longitude = longitude;
        existing.AddressHash = VenueAddressHasher.ComputeHash(existing.VenueNumber, existing.Address, existing.PostalCode);
        existing.CoordinateSource = coordinateSource;
        existing.EnrichedAt = enrichedAt.ToString("O", CultureInfo.InvariantCulture);
        await db.SaveAsync(existing).ConfigureAwait(false);
        return true;
    }

    // ── Transfer Matrix ─────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a fully validated transfer-matrix version's metadata and pair records under
    /// <c>TRANSFER_MATRIX#&lt;inputHash&gt;</c>. Does not touch the active pointer — a version
    /// only becomes visible to readers once <see cref="SetActiveTransferMatrixAsync"/> is called,
    /// so a caller can validate before promoting without ever exposing partial data.
    /// </summary>
    public virtual async Task SaveTransferMatrixAsync(TransferMatrixVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        string pk = $"TRANSFER_MATRIX#{version.InputHash}";

        await db.SaveAsync(new TransferMatrixMetadataRecord
        {
            Pk = pk,
            Sk = "METADATA",
            InputHash = version.InputHash,
            VenueCount = version.VenueCount,
            PairCount = version.Pairs.Count,
            GeneratedAt = version.GeneratedAt.ToString("O", CultureInfo.InvariantCulture),
            Source = version.Source
        }).ConfigureAwait(false);

        IBatchWrite<TransferMatrixPairRecord> batch = db.CreateBatchWrite<TransferMatrixPairRecord>();
        foreach (TransferPair pair in version.Pairs)
        {
            batch.AddPutItem(new TransferMatrixPairRecord
            {
                Pk = pk,
                Sk = $"FROM#{pair.FromVenueNumber}#TO#{pair.ToVenueNumber}",
                FromVenueNumber = pair.FromVenueNumber,
                ToVenueNumber = pair.ToVenueNumber,
                WalkingDurationSeconds = pair.WalkingDurationSeconds,
                WalkingDistanceMeters = pair.WalkingDistanceMeters,
                CyclingDurationSeconds = pair.CyclingDurationSeconds,
                CyclingDistanceMeters = pair.CyclingDistanceMeters,
                DrivingDurationSeconds = pair.DrivingDurationSeconds,
                DrivingDistanceMeters = pair.DrivingDistanceMeters,
                Source = pair.Source
            });
        }
        await batch.ExecuteAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Returns every directional pair for a matrix version in a single partition query — the
    /// complete active matrix can always be loaded in one round trip.
    /// </summary>
    public virtual async Task<List<TransferMatrixPairRecord>> GetTransferMatrixPairsAsync(string inputHash)
    {
        List<TransferMatrixPairRecord> items = await db.FromQueryAsync<TransferMatrixPairRecord>(new QueryOperationConfig
        {
            KeyExpression = new Expression
            {
                ExpressionStatement = "pk = :pk",
                ExpressionAttributeValues = { [":pk"] = new Primitive($"TRANSFER_MATRIX#{inputHash}") }
            }
        }).GetRemainingAsync().ConfigureAwait(false);

        // The partition also contains one METADATA item, which deserializes into this type with
        // default/garbage pair fields — excluded here so callers only ever see real pairs.
        return [.. items.Where(i => i.Sk.StartsWith("FROM#", StringComparison.Ordinal))];
    }

    /// <summary>Returns a matrix version's metadata, or <see langword="null"/> if that version doesn't exist.</summary>
    public virtual async Task<TransferMatrixMetadataRecord?> GetTransferMatrixMetadataAsync(string inputHash)
    {
        return await db.LoadAsync<TransferMatrixMetadataRecord>($"TRANSFER_MATRIX#{inputHash}", "METADATA").ConfigureAwait(false);
    }

    /// <summary>Returns the active transfer-matrix pointer, or <see langword="null"/> if no version has ever been published.</summary>
    public virtual async Task<ActiveTransferMatrixRecord?> GetActiveTransferMatrixPointerAsync()
    {
        return await db.LoadAsync<ActiveTransferMatrixRecord>("CONFIG", "ACTIVE_TRANSFER_MATRIX").ConfigureAwait(false);
    }

    /// <summary>
    /// Publishes a matrix version as active. This is the entire "promotion" — a single item write —
    /// and callers must only invoke it after <see cref="SaveTransferMatrixAsync"/> has fully
    /// succeeded for that version, so a reader can never observe a partially written matrix as active.
    /// </summary>
    public virtual async Task SetActiveTransferMatrixAsync(string inputHash, DateTime promotedAt)
    {
        await db.SaveAsync(new ActiveTransferMatrixRecord
        {
            InputHash = inputHash,
            PromotedAt = promotedAt.ToString("O", CultureInfo.InvariantCulture)
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Marks a superseded matrix version's metadata and pair records with a DynamoDB TTL so they
    /// become eligible for automatic cleanup at <paramref name="expiresAt"/>, without deleting them
    /// immediately — keeping a recently retired version around briefly is a cheap safety net.
    /// </summary>
    public virtual async Task MarkTransferMatrixStaleAsync(string inputHash, DateTime expiresAt)
    {
        long ttl = ((DateTimeOffset)DateTime.SpecifyKind(expiresAt, DateTimeKind.Utc)).ToUnixTimeSeconds();

        TransferMatrixMetadataRecord? metadata = await GetTransferMatrixMetadataAsync(inputHash).ConfigureAwait(false);
        if (metadata != null)
        {
            metadata.Ttl = ttl;
            await db.SaveAsync(metadata).ConfigureAwait(false);
        }

        List<TransferMatrixPairRecord> pairs = await GetTransferMatrixPairsAsync(inputHash).ConfigureAwait(false);
        IBatchWrite<TransferMatrixPairRecord> batch = db.CreateBatchWrite<TransferMatrixPairRecord>();
        foreach (TransferMatrixPairRecord pair in pairs)
        {
            pair.Ttl = ttl;
            batch.AddPutItem(pair);
        }
        await batch.ExecuteAsync().ConfigureAwait(false);
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
