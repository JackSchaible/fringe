namespace Fringe.API;

/// <summary>Represents a single availability window.</summary>
internal record AvailabilityWindowDto(string Start, string End);

/// <summary>DTO for a user's availability windows.</summary>
internal record UserAvailabilityDto(IReadOnlyList<AvailabilityWindowDto> Windows);

/// <summary>DTO for the computed group schedule response.</summary>
internal record ScheduleResponseDto(
    IReadOnlyList<ScheduleItemDto> Items,
    IReadOnlyList<AlternateProposalDto> AlternateProposals,
    IReadOnlyList<MissedShowDto> MissedShows,
    bool HasVotes,
    string TravelMode
);

/// <summary>DTO for a show that could not be scheduled.</summary>
internal record MissedShowDto(
    ShowDto Show,
    bool ConflictsWithScheduled,
    IReadOnlyList<string> BlockedByMembers,
    TransferConflictDto? TransferConflict
);

/// <summary>
/// DTO for why a show was missed specifically because the group couldn't feasibly transfer
/// between venues (FA-35) — a concise, storage-detail-free summary of the venue pair and timing
/// that made a showtime infeasible, not a dump of the underlying matrix/override record.
/// </summary>
internal record TransferConflictDto(
    string? OriginVenueName,
    string? DestinationVenueName,
    string OriginShowTitle,
    string DestinationShowTitle,
    int AvailableGapMinutes,
    int RequiredGapMinutes,
    string TravelMode,
    string AppliedRule
);

/// <summary>DTO for an alternate schedule proposal that excludes one member's constraints.</summary>
internal record AlternateProposalDto(
    string Description,
    string ExcludedMemberName,
    IReadOnlyList<ScheduleItemDto> Items
);

/// <summary>DTO for a show returned from the API.</summary>
internal record ShowDto(
    int ShowId,
    string Title,
    string? Description,
    string? PlainTextDescription,
    Uri? ImageUrl,
    string? Tag,
    string Price,
    string Fee,
    int LengthInMinutes,
    VenueDto? Venue,
    ContentRatingDto? ContentRating,
    IReadOnlyList<string> ShowTimes
);

/// <summary>DTO for a venue.</summary>
internal record VenueDto(string Name, string Address, string Phone);

/// <summary>DTO for a content rating.</summary>
internal record ContentRatingDto(string Name, string Code, string? Description);

/// <summary>DTO for a user's vote on a show.</summary>
internal record VoteDto(int ShowId, int Rank);

/// <summary>DTO for a group.</summary>
internal record GroupDto(string GroupId, string Name, string InviteCode, IReadOnlyList<GroupMemberDto> Members);

/// <summary>DTO for a group member.</summary>
internal record GroupMemberDto(string UserId, string? DisplayName, string? Email, int VoteCount);

/// <summary>DTO for a scheduled show item.</summary>
internal record ScheduleItemDto(ShowDto Show, string ShowTime, int GroupScore);

/// <summary>DTO for a user profile.</summary>
internal record UserDto(string UserId, string? Email, string? DisplayName, string? GroupId);

/// <summary>Request to create a group.</summary>
internal record CreateGroupRequest(string Name);

/// <summary>Request to join a group by invite code.</summary>
internal record JoinGroupRequest(string InviteCode);

/// <summary>Request to upsert the current user's profile.</summary>
internal record UpsertUserRequest(string Email, string DisplayName);

/// <summary>Request to update the current user's display name.</summary>
internal record UpdateDisplayNameRequest(string DisplayName);

/// <summary>Request body for captcha verification.</summary>
internal record VerifyRequest(string Token);
