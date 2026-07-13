namespace Fringe.API;

public record AvailabilityWindowDto(string Start, string End);

public record UserAvailabilityDto(List<AvailabilityWindowDto> Windows);

public record ScheduleResponseDto(
    List<ScheduleItemDto> Items,
    List<AlternateProposalDto> AlternateProposals
);

public record AlternateProposalDto(
    string Description,
    string ExcludedMemberName,
    List<ScheduleItemDto> Items
);

public record ShowDto(
    int ShowId,
    string Title,
    string? Description,
    string? PlainTextDescription,
    string? ImageUrl,
    string? Tag,
    string Price,
    string Fee,
    int LengthInMinutes,
    VenueDto? Venue,
    ContentRatingDto? ContentRating,
    List<string> ShowTimes
);

public record VenueDto(string Name, string Address, string Phone);

public record ContentRatingDto(string Name, string Code, string? Description);

public record VoteDto(int ShowId, int Rank);

public record GroupDto(string GroupId, string Name, string InviteCode, List<GroupMemberDto> Members);

public record GroupMemberDto(string UserId, string? DisplayName, string? Email, int VoteCount);

public record ScheduleItemDto(ShowDto Show, string ShowTime, int GroupScore);

public record UserDto(string UserId, string Email, string DisplayName, string? GroupId);

public record CreateGroupRequest(string Name);

public record JoinGroupRequest(string InviteCode);

public record UpsertUserRequest(string Email, string DisplayName);
